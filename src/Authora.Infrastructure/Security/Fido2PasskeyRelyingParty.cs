// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Authora.Core.Abstractions;
using Authora.Core.Application.Mfa;
using Authora.Core.Application.OAuth;
using Authora.Core.Domain.Entities;

using Fido2NetLib;
using Fido2NetLib.Exceptions;
using Fido2NetLib.Objects;
using Fido2NetLib.Serialization;

using Microsoft.Extensions.Options;

namespace Authora.Infrastructure.Security;

/// <summary>
///     The WebAuthn relying party, implemented with Fido2NetLib (WebAuthn Level 2).
/// </summary>
/// <remarks>
///     <para>
///         The relying party Id is the host of the Issuer, the only accepted origin is that of the Issuer (scheme, host
///         and port), and the name the authenticator shows is <see cref="MfaOptions.IssuerName"/>. No attestation is
///         requested and no metadata service is used: Authora does not restrict which authenticators Users choose.
///     </para>
///     <para>
///         The transports of a registration are read here and removed before the response is handed to the library,
///         so a value a newer browser reports cannot break the registration.
///     </para>
/// </remarks>
/// <param name="oauthOptions">The OAuth settings with the Issuer.</param>
/// <param name="mfaOptions">The MFA settings with the name shown in authenticators.</param>
public sealed class Fido2PasskeyRelyingParty(IOptions<OAuthOptions> oauthOptions, IOptions<MfaOptions> mfaOptions) : IPasskeyRelyingParty
{
    // A response is a few kilobytes even with the longest credential Id; anything much larger is not parsed.
    private const int MaxResponseLength = 64 * 1024;

    private static readonly AuthenticatorSelection Selection = new()
    {
        ResidentKey = ResidentKeyRequirement.Preferred,
        UserVerification = UserVerificationRequirement.Preferred
    };

    /// <inheritdoc/>
    public string CreateRegistrationOptions(PasskeyRegistrationRequest request)
    {
        return BuildCreationOptions(CreateConfiguration(), request).ToJson();
    }

    /// <inheritdoc/>
    public string CreateAuthenticationOptions(byte[] challenge, IReadOnlyList<PasskeyDescriptor> allowedCredentials)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(allowedCredentials);

        return AssertionOptions.Create(
            CreateConfiguration(),
            challenge,
            [.. allowedCredentials.Select(ToDescriptor)],
            UserVerificationRequirement.Preferred,
            new AuthenticationExtensionsClientInputs()).ToJson();
    }

    /// <inheritdoc/>
    public PasskeyClientResponse? ReadResponse(string? responseJson)
    {
        if (string.IsNullOrEmpty(responseJson) || responseJson.Length > MaxResponseLength)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("rawId", out var rawId) || rawId.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object
                || !response.TryGetProperty("clientDataJSON", out var clientDataJson) || clientDataJson.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            using var clientData = JsonDocument.Parse(Base64Url.DecodeFromChars(clientDataJson.GetString()));

            if (clientData.RootElement.ValueKind != JsonValueKind.Object
                || !clientData.RootElement.TryGetProperty("challenge", out var challenge)
                || challenge.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var credentialId = Base64Url.DecodeFromChars(rawId.GetString());
            var challengeBytes = Base64Url.DecodeFromChars(challenge.GetString());

            if (credentialId.Length is 0 or > Passkey.CredentialIdMaxLength || challengeBytes.Length != PasskeyChallenge.Length)
            {
                return null;
            }

            return new PasskeyClientResponse(challengeBytes, credentialId);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<PasskeyRegistrationVerification> VerifyRegistrationAsync(
        string responseJson,
        PasskeyRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseJson);
        ArgumentNullException.ThrowIfNull(request);

        var configuration = CreateConfiguration();

        try
        {
            var node = JsonNode.Parse(responseJson)?.AsObject();

            if (node?["response"] is not JsonObject response)
            {
                return PasskeyRegistrationVerification.Failed("Malformed response");
            }

            var transports = ReadTransports(response);
            response.Remove("transports");

            var raw = node.Deserialize<AuthenticatorAttestationRawResponse>();

            if (raw is null)
            {
                return PasskeyRegistrationVerification.Failed("Malformed response");
            }

            var credential = await new Fido2(configuration).MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = raw,
                    OriginalOptions = BuildCreationOptions(configuration, request),

                    // Checked by the caller under the lock of the User, with a clear message.
                    IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true)
                },
                cancellationToken);

            return new PasskeyRegistrationVerification(
                new PasskeyCredential(
                    credential.Id,
                    credential.PublicKey,
                    credential.SignCount,
                    credential.AaGuid,
                    transports,
                    credential.IsBackupEligible,
                    credential.IsBackedUp),
                null);
        }
        catch (Fido2VerificationException exception)
        {
            return PasskeyRegistrationVerification.Failed(Describe(exception.Code));
        }
        catch (Exception exception) when (IsMalformed(exception))
        {
            return PasskeyRegistrationVerification.Failed("Malformed response");
        }
    }

    /// <inheritdoc/>
    public async Task<PasskeyAuthenticationVerification> VerifyAuthenticationAsync(
        string responseJson,
        PasskeyAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(responseJson);
        ArgumentNullException.ThrowIfNull(request);

        var configuration = CreateConfiguration();

        try
        {
            var raw = JsonSerializer.Deserialize(responseJson, FidoModelSerializerContext.Default.AuthenticatorAssertionRawResponse);

            if (raw is null)
            {
                return PasskeyAuthenticationVerification.Failed("Malformed response");
            }

            var options = AssertionOptions.Create(
                configuration,
                request.Challenge,
                [new PublicKeyCredentialDescriptor(request.CredentialId)],
                UserVerificationRequirement.Preferred,
                new AuthenticationExtensionsClientInputs());

            var result = await new Fido2(configuration).MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = raw,
                    OriginalOptions = options,
                    StoredPublicKey = request.PublicKey,
                    StoredSignatureCounter = request.SignCount,

                    // The response may name a user handle; it must then be that of the User the passkey belongs to.
                    IsUserHandleOwnerOfCredentialIdCallback = (parameters, _) => Task.FromResult(
                        parameters.CredentialId.AsSpan().SequenceEqual(request.CredentialId)
                        && (parameters.UserHandle is null || parameters.UserHandle.AsSpan().SequenceEqual(request.UserHandle)))
                },
                cancellationToken);

            return new PasskeyAuthenticationVerification(true, result.SignCount, result.IsBackedUp, null);
        }
        catch (Fido2VerificationException exception)
        {
            return PasskeyAuthenticationVerification.Failed(
                Describe(exception.Code),
                signCountDidNotIncrease: exception.Code == Fido2ErrorCode.InvalidSignCount);
        }
        catch (Exception exception) when (IsMalformed(exception))
        {
            return PasskeyAuthenticationVerification.Failed("Malformed response");
        }
    }

    private static CredentialCreateOptions BuildCreationOptions(Fido2Configuration configuration, PasskeyRegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = new Fido2User
        {
            Id = request.UserHandle,
            Name = request.UserName,
            DisplayName = request.UserDisplayName
        };

        return CredentialCreateOptions.Create(
            configuration,
            request.Challenge,
            user,
            Selection,
            AttestationConveyancePreference.None,
            [.. request.ExcludeCredentials.Select(ToDescriptor)],
            new AuthenticationExtensionsClientInputs(),
            PubKeyCredParam.Defaults);
    }

    private static PublicKeyCredentialDescriptor ToDescriptor(PasskeyDescriptor descriptor)
    {
        var transports = descriptor.Transports
            .Select(ParseTransport)
            .OfType<AuthenticatorTransport>()
            .Distinct()
            .ToArray();

        return new PublicKeyCredentialDescriptor(
            PublicKeyCredentialType.PublicKey,
            descriptor.CredentialId,
            transports.Length == 0 ? null! : transports);
    }

    private static AuthenticatorTransport? ParseTransport(string transport) => transport switch
    {
        "usb" => AuthenticatorTransport.Usb,
        "nfc" => AuthenticatorTransport.Nfc,
        "ble" => AuthenticatorTransport.Ble,
        "smart-card" => AuthenticatorTransport.SmartCard,
        "hybrid" => AuthenticatorTransport.Hybrid,
        "internal" => AuthenticatorTransport.Internal,
        _ => null
    };

    private static List<string> ReadTransports(JsonObject response)
    {
        if (response["transports"] is not JsonArray transports)
        {
            return [];
        }

        return [.. transports
            .Where(transport => transport?.GetValueKind() == JsonValueKind.String)
            .Select(transport => transport!.GetValue<string>())
            .Where(transport => ParseTransport(transport) is not null)
            .Distinct(StringComparer.Ordinal)];
    }

    private static bool IsMalformed(Exception exception)
    {
        return exception is JsonException
            or FormatException
            or ArgumentException
            or InvalidOperationException
            or CborContentException
            or CryptographicException;
    }

    private static string Describe(Fido2ErrorCode code) => code switch
    {
        Fido2ErrorCode.InvalidSignature => "Invalid signature",
        Fido2ErrorCode.InvalidSignCount => "The signature counter did not increase",
        Fido2ErrorCode.InvalidRpidHash => "Created for another relying party",
        Fido2ErrorCode.InvalidAuthenticatorResponseOrigin or Fido2ErrorCode.MissingAuthenticatorResponseOrigin => "Wrong origin",
        Fido2ErrorCode.InvalidAuthenticatorResponseChallenge or Fido2ErrorCode.MissingAuthenticatorResponseChallenge => "Wrong challenge",
        Fido2ErrorCode.UserPresentFlagNotSet => "The User was not present",
        Fido2ErrorCode.CrossOriginRequestNotAllowed or Fido2ErrorCode.InvalidAuthenticatorResponseTopOrigin => "Cross-origin request",
        Fido2ErrorCode.UnimplementedAlgorithm or Fido2ErrorCode.CredentialAlgorithmRequirementNotMet => "Unsupported algorithm",
        _ => $"Rejected by the WebAuthn verification ({code})"
    };

    private Fido2Configuration CreateConfiguration()
    {
        var issuer = new Uri(oauthOptions.Value.Issuer);

        return new Fido2Configuration
        {
            RPID = issuer.Host,
            RPName = mfaOptions.Value.IssuerName,
            Origins = new HashSet<string>(StringComparer.Ordinal) { issuer.GetLeftPart(UriPartial.Authority) },
            Timeout = (uint)PasskeyChallenge.Lifetime.TotalMilliseconds
        };
    }
}
