// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     A WebAuthn authenticator in software for the tests: it creates one ES256 passkey with the attestation format
///     <c>none</c> and signs challenges with it, producing the JSON the browser script of Authora posts.
/// </summary>
/// <remarks>
///     It acts like a browser at <see cref="Origin"/>: the client data names that origin and the authenticator data the
///     hash of the relying party Id from the options. Tests change <see cref="Origin"/>, <see cref="SignCount"/> and
///     the flags to produce invalid responses.
/// </remarks>
public sealed class SoftwareAuthenticator : IDisposable
{
    private const byte UserPresent = 0x01;
    private const byte UserVerified = 0x04;
    private const byte BackupEligible = 0x08;
    private const byte BackedUp = 0x10;
    private const byte AttestedCredentialData = 0x40;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>
    ///     The AAGUID the authenticator reports.
    /// </summary>
    public static readonly Guid AaGuid = new("0f3c5b2e-8f61-4c1a-9a5e-2d7b6c4e1a90");

    /// <summary>
    ///     The credential Id of its passkey.
    /// </summary>
    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    ///     The origin the browser reports in the client data.
    /// </summary>
    public string Origin { get; set; } = AuthoraApiFactory.Issuer;

    /// <summary>
    ///     The signature counter; increased before every signature if <see cref="Counts"/> is set.
    /// </summary>
    public uint SignCount { get; set; }

    /// <summary>
    ///     Whether the authenticator counts its signatures. Synced passkeys usually do not and always report 0.
    /// </summary>
    public bool Counts { get; init; } = true;

    /// <summary>
    ///     Whether the passkey may be synced (the BE and BS flags).
    /// </summary>
    public bool Synced { get; init; }

    /// <summary>
    ///     The user handle of the options the passkey was created for.
    /// </summary>
    public byte[]? UserHandle { get; private set; }

    /// <summary>
    ///     Creates the passkey for <c>PublicKeyCredentialCreationOptions</c>, like <c>navigator.credentials.create</c>.
    /// </summary>
    /// <param name="optionsJson">The options from the server.</param>
    /// <param name="transports">The transports the browser reports.</param>
    /// <returns>The response as the browser script posts it.</returns>
    public string Create(string optionsJson, params string[] transports)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var root = options.RootElement;

        UserHandle = Base64Url.DecodeFromChars(root.GetProperty("user").GetProperty("id").GetString());

        var clientData = ClientData("webauthn.create", root.GetProperty("challenge").GetString()!);
        var authenticatorData = AuthenticatorData(root.GetProperty("rp").GetProperty("id").GetString()!, AttestedCredentialData, withCredential: true);

        var attestation = new CborWriter(CborConformanceMode.Ctap2Canonical);
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt");
        attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt");
        attestation.WriteStartMap(0);
        attestation.WriteEndMap();
        attestation.WriteTextString("authData");
        attestation.WriteByteString(authenticatorData);
        attestation.WriteEndMap();

        return JsonSerializer.Serialize(new
        {
            id = Base64Url.EncodeToString(CredentialId),
            rawId = Base64Url.EncodeToString(CredentialId),
            type = "public-key",
            response = new
            {
                clientDataJSON = Base64Url.EncodeToString(clientData),
                attestationObject = Base64Url.EncodeToString(attestation.Encode()),
                transports
            },
            clientExtensionResults = new { }
        });
    }

    /// <summary>
    ///     Signs the challenge of <c>PublicKeyCredentialRequestOptions</c>, like <c>navigator.credentials.get</c>.
    /// </summary>
    /// <param name="optionsJson">The options from the server.</param>
    /// <returns>The response as the browser script posts it.</returns>
    public string Get(string optionsJson)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var root = options.RootElement;

        var clientData = ClientData("webauthn.get", root.GetProperty("challenge").GetString()!);
        var authenticatorData = AuthenticatorData(root.GetProperty("rpId").GetString()!, 0, withCredential: false);
        var signature = _key.SignData([.. authenticatorData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return JsonSerializer.Serialize(new
        {
            id = Base64Url.EncodeToString(CredentialId),
            rawId = Base64Url.EncodeToString(CredentialId),
            type = "public-key",
            response = new
            {
                clientDataJSON = Base64Url.EncodeToString(clientData),
                authenticatorData = Base64Url.EncodeToString(authenticatorData),
                signature = Base64Url.EncodeToString(signature),
                userHandle = UserHandle is null ? null : Base64Url.EncodeToString(UserHandle)
            },
            clientExtensionResults = new { }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _key.Dispose();
    }

    private byte[] ClientData(string type, string challenge)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, challenge, origin = Origin, crossOrigin = false }));
    }

    private byte[] AuthenticatorData(string rpId, byte extraFlags, bool withCredential)
    {
        if (Counts)
        {
            SignCount++;
        }

        var flags = (byte)(UserPresent | UserVerified | extraFlags | (Synced ? BackupEligible | BackedUp : 0));

        var data = new List<byte>();
        data.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        data.Add(flags);

        var counter = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(counter, SignCount);
        data.AddRange(counter);

        if (withCredential)
        {
            data.AddRange(AaGuid.ToByteArray(bigEndian: true));

            var length = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)CredentialId.Length);
            data.AddRange(length);
            data.AddRange(CredentialId);
            data.AddRange(CoseKey());
        }

        return [.. data];
    }

    // The public key as a COSE_Key (RFC 9053): EC2, ES256, P-256, x and y.
    private byte[] CoseKey()
    {
        var parameters = _key.ExportParameters(includePrivateParameters: false);

        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        writer.WriteInt32(-7);
        writer.WriteInt32(-1);
        writer.WriteInt32(1);
        writer.WriteInt32(-2);
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3);
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();

        return writer.Encode();
    }
}
