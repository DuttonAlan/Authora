// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Authora.Core.Abstractions;
using Authora.Core.Application.ExternalSignIn;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Authora.Infrastructure.ExternalSignIn;

/// <summary>
///     Authora as an OpenID Connect Relying Party of external identity providers (Authorization Code flow with PKCE),
///     built on <c>Microsoft.IdentityModel</c>: the discovery document and the keys of each Issuer are cached by a
///     <see cref="ConfigurationManager{T}"/>, ID Tokens are verified with <see cref="JsonWebTokenHandler"/>.
/// </summary>
/// <remarks>
///     <para>
///         The token request authenticates with <c>client_secret_basic</c>, or <c>client_secret_post</c> if the
///         provider only supports that, or with the Client Id alone (PKCE only) if no secret is set.
///     </para>
///     <para>
///         ID Tokens must be signed with RS256, ES256 or PS256 by a key of the provider's key set, and name the Issuer
///         in <c>iss</c>, the Client Id in <c>aud</c> (and <c>azp</c>, if present or if there are several audiences),
///         the <c>nonce</c> of the request, and an <c>exp</c> and <c>iat</c> within <see cref="ClockSkew"/>.
///     </para>
/// </remarks>
/// <param name="httpClientFactory">Creates the HTTP client <see cref="HttpClientName"/>.</param>
/// <param name="options">The settings with the timeout and whether HTTP is allowed.</param>
/// <param name="timeProvider">The clock for <c>iat</c>.</param>
/// <param name="logger">The Logger to record failed requests.</param>
public sealed class OidcRelyingParty(
    IHttpClientFactory httpClientFactory,
    IOptions<ExternalSignInOptions> options,
    TimeProvider timeProvider,
    ILogger<OidcRelyingParty> logger) : IOidcRelyingParty
{
    /// <summary>
    ///     Name of the HTTP client for requests to providers: <see cref="ExternalSignInOptions.Timeout"/>, no automatic
    ///     redirects, no cookies, the default proxy settings.
    /// </summary>
    public const string HttpClientName = "Authora.ExternalSignIn";

    /// <summary>
    ///     How far the clocks of Authora and a provider may differ for <c>exp</c> and <c>iat</c>.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     The signature algorithms accepted for ID Tokens.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedAlgorithms =
        [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.RsaSsaPssSha256];

    // Largest token response accepted.
    private const int MaxTokenResponseLength = 256 * 1024;

    // Longest error code of a token response that is logged.
    private const int MaxErrorLength = 64;

    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = new(StringComparer.Ordinal);

    private readonly JsonWebTokenHandler _tokenHandler = new() { MapInboundClaims = false };

    /// <summary>
    ///     Checks whether Authora may send a request to a URL of a provider: HTTPS, or HTTP if allowed for development.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="settings">The settings.</param>
    /// <returns>True if the URL may be used.</returns>
    internal static bool IsAllowedUrl(string? url, ExternalSignInOptions settings)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (settings.AllowHttp && uri.Scheme == Uri.UriSchemeHttp));
    }

    /// <inheritdoc/>
    public async Task ValidateIssuerAsync(string issuer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issuer);

        if (!IsAllowedUrl(issuer, options.Value))
        {
            throw new ExternalSignInException("The Issuer does not use HTTPS.");
        }

        OpenIdConnectConfiguration configuration;

        try
        {
            // Fetched without the cache, so a provider that was just set up is checked as it is now.
            configuration = await OpenIdConnectConfigurationRetriever.GetAsync(
                GetDiscoveryUrl(issuer),
                new HttpClientDocumentRetriever(httpClientFactory, options),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw Unreachable(issuer, exception);
        }

        EnsureUsable(issuer, configuration, options.Value);

        if (configuration.ResponseTypesSupported.Count > 0 && !configuration.ResponseTypesSupported.Contains("code"))
        {
            throw new ExternalSignInException("The provider does not support the response type 'code'.");
        }

        if (configuration.SigningKeys.Count == 0)
        {
            throw new ExternalSignInException("The key set of the provider contains no signing key.");
        }

        // The next sign-in uses the document as it is now, e.g. after the Issuer of a provider was changed back.
        _managers.TryRemove(issuer, out _);
    }

    /// <inheritdoc/>
    public async Task<Uri> CreateAuthorizationUrlAsync(ExternalAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuration = await GetConfigurationAsync(request.Issuer, cancellationToken);

        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = request.ClientId,
            ["redirect_uri"] = request.RedirectUri,
            ["scope"] = request.Scopes,
            ["state"] = request.State,
            ["nonce"] = request.Nonce,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var endpoint = configuration.AuthorizationEndpoint;
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var query = string.Join('&', parameters.Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));

        return new Uri($"{endpoint}{separator}{query}");
    }

    /// <inheritdoc/>
    public async Task<ExternalIdentityClaims> RedeemCodeAsync(ExternalCodeRedemption redemption, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(redemption);

        var configuration = await GetConfigurationAsync(redemption.Issuer, cancellationToken);
        var idToken = await RequestIdTokenAsync(configuration, redemption, cancellationToken);
        var result = await ValidateAsync(idToken, configuration, redemption);

        if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // The provider may have rotated its keys since they were cached: load them again, once.
            var manager = GetManager(redemption.Issuer);
            manager.RequestRefresh();

            var refreshed = await GetConfigurationAsync(redemption.Issuer, cancellationToken);

            if (!ReferenceEquals(refreshed, configuration))
            {
                result = await ValidateAsync(idToken, refreshed, redemption);
            }
        }

        if (!result.IsValid)
        {
            throw new ExternalSignInException(DescribeFailure(result.Exception));
        }

        return ReadClaims((JsonWebToken)result.SecurityToken, redemption);
    }

    private static string GetDiscoveryUrl(string issuer)
    {
        return $"{issuer.TrimEnd('/')}/.well-known/openid-configuration";
    }

    private static void EnsureUsable(string issuer, OpenIdConnectConfiguration configuration, ExternalSignInOptions settings)
    {
        // OpenID Connect Discovery 1.0, Section 4.3: the document must name exactly the Issuer it was fetched for.
        if (!string.Equals(configuration.Issuer, issuer, StringComparison.Ordinal))
        {
            throw new ExternalSignInException($"The discovery document names the Issuer '{configuration.Issuer}' instead of '{issuer}'.");
        }

        if (string.IsNullOrEmpty(configuration.AuthorizationEndpoint)
            || string.IsNullOrEmpty(configuration.TokenEndpoint)
            || string.IsNullOrEmpty(configuration.JwksUri))
        {
            throw new ExternalSignInException("The discovery document lacks the authorization endpoint, the token endpoint or the key set.");
        }

        // Checked on every use, not only when the provider is saved: the document may change afterwards. An HTTP
        // authorization endpoint would show the User the provider's sign-in page without TLS.
        if (!IsAllowedUrl(configuration.AuthorizationEndpoint, settings)
            || !IsAllowedUrl(configuration.TokenEndpoint, settings)
            || !IsAllowedUrl(configuration.JwksUri, settings))
        {
            throw new ExternalSignInException("The authorization endpoint, the token endpoint or the key set of the provider does not use HTTPS.");
        }
    }

    private ExternalSignInException Unreachable(string issuer, Exception exception)
    {
        logger.ProviderUnreachable(issuer, exception);

        var reason = exception is ExternalSignInException own
            ? own.Message
            : exception.InnerException is ExternalSignInException inner
                ? inner.Message
                : "The discovery document or the key set could not be fetched or read.";

        return new ExternalSignInException(reason, exception);
    }

    private ConfigurationManager<OpenIdConnectConfiguration> GetManager(string issuer)
    {
        return _managers.GetOrAdd(issuer, key => new ConfigurationManager<OpenIdConnectConfiguration>(
            GetDiscoveryUrl(key),
            new OpenIdConnectConfigurationRetriever(),
            new HttpClientDocumentRetriever(httpClientFactory, options)));
    }

    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string issuer, CancellationToken cancellationToken)
    {
        if (!IsAllowedUrl(issuer, options.Value))
        {
            throw new ExternalSignInException("The Issuer does not use HTTPS.");
        }

        OpenIdConnectConfiguration configuration;

        try
        {
            configuration = await GetManager(issuer).GetConfigurationAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw Unreachable(issuer, exception);
        }

        EnsureUsable(issuer, configuration, options.Value);

        return configuration;
    }

    private async Task<string> RequestIdTokenAsync(
        OpenIdConnectConfiguration configuration,
        ExternalCodeRedemption redemption,
        CancellationToken cancellationToken)
    {
        if (!IsAllowedUrl(configuration.TokenEndpoint, options.Value))
        {
            throw new ExternalSignInException("The token endpoint does not use HTTPS.");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", redemption.Code),
            new("redirect_uri", redemption.RedirectUri),
            new("code_verifier", redemption.CodeVerifier)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint);

        if (redemption.ClientSecret is null)
        {
            form.Add(new("client_id", redemption.ClientId));
        }
        else if (SupportsBasic(configuration))
        {
            // RFC 6749, Section 2.3.1: both values are form-encoded before they are joined and base64-encoded.
            var credentials = $"{FormEncode(redemption.ClientId)}:{FormEncode(redemption.ClientSecret)}";
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));
        }
        else if (configuration.TokenEndpointAuthMethodsSupported.Contains("client_secret_post"))
        {
            form.Add(new("client_id", redemption.ClientId));
            form.Add(new("client_secret", redemption.ClientSecret));
        }
        else
        {
            throw new ExternalSignInException("The token endpoint supports neither client_secret_basic nor client_secret_post.");
        }

        request.Content = new FormUrlEncodedContent(form);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // One deadline for the headers and the body: HttpClient.Timeout ends with the headers of a streamed response.
        using var deadline = BoundedResponse.CreateDeadline(options.Value.Timeout, cancellationToken);
        using var http = httpClientFactory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        string body;

        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.TokenRequestFailed(redemption.Issuer, exception);

            throw new ExternalSignInException("The token endpoint could not be reached.", exception);
        }

        using (response)
        {
            try
            {
                body = await BoundedResponse.ReadAsStringAsync(response.Content, MaxTokenResponseLength, deadline.Token);
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.TokenRequestFailed(redemption.Issuer, exception);

                throw new ExternalSignInException($"The token response could not be read within {options.Value.Timeout}.", exception);
            }

            JsonElement json;

            try
            {
                json = JsonSerializer.Deserialize<JsonElement>(body);
            }
            catch (JsonException exception)
            {
                throw new ExternalSignInException($"The token endpoint returned {(int)response.StatusCode} without JSON.", exception);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = json.ValueKind == JsonValueKind.Object
                    && json.TryGetProperty("error", out var value)
                    && value.ValueKind == JsonValueKind.String
                        ? Sanitize(value.GetString()!)
                        : "none";

                throw new ExternalSignInException($"The token endpoint returned {(int)response.StatusCode} with the error '{error}'.");
            }

            if (json.ValueKind != JsonValueKind.Object
                || !json.TryGetProperty("id_token", out var idToken)
                || idToken.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(idToken.GetString()))
            {
                throw new ExternalSignInException("The token response contains no ID Token.");
            }

            return idToken.GetString()!;
        }
    }

    private static bool SupportsBasic(OpenIdConnectConfiguration configuration)
    {
        // Without the list, client_secret_basic is the default (OpenID Connect Discovery 1.0, Section 3).
        return configuration.TokenEndpointAuthMethodsSupported.Count == 0
            || configuration.TokenEndpointAuthMethodsSupported.Contains("client_secret_basic");
    }

    private static string FormEncode(string value)
    {
        return Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);
    }

    private async Task<TokenValidationResult> ValidateAsync(
        string idToken,
        OpenIdConnectConfiguration configuration,
        ExternalCodeRedemption redemption)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = redemption.Issuer,
            ValidAudience = redemption.ClientId,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidAlgorithms = AllowedAlgorithms,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = ClockSkew
        };

        return await _tokenHandler.ValidateTokenAsync(idToken, parameters);
    }

    private ExternalIdentityClaims ReadClaims(JsonWebToken token, ExternalCodeRedemption redemption)
    {
        if (!token.TryGetPayloadValue<string>("nonce", out var nonce)
            || nonce is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(nonce), Encoding.UTF8.GetBytes(redemption.Nonce)))
        {
            throw new ExternalSignInException("The ID Token does not contain the nonce of the request.");
        }

        // OpenID Connect Core 1.0, Section 3.1.3.7: with several audiences azp is required, and it must be Authora.
        var hasAzp = token.TryGetPayloadValue<string>("azp", out var azp);

        if ((hasAzp && !string.Equals(azp, redemption.ClientId, StringComparison.Ordinal))
            || (!hasAzp && token.Audiences.Count() > 1))
        {
            throw new ExternalSignInException("The ID Token was issued to another Client (azp).");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (!token.TryGetPayloadValue<long>("iat", out _) || token.IssuedAt == DateTime.MinValue)
        {
            throw new ExternalSignInException("The ID Token has no iat.");
        }

        // Issued during the flow: neither in the future nor before the User left for the provider.
        if (token.IssuedAt > now + ClockSkew || token.IssuedAt < now - ExternalSignInOptions.FlowLifetime - ClockSkew)
        {
            throw new ExternalSignInException("The iat of the ID Token is outside the time of the sign-in.");
        }

        var subject = token.Subject;

        if (string.IsNullOrEmpty(subject) || subject.Length > Core.Domain.Entities.ExternalIdentity.SubjectMaxLength)
        {
            throw new ExternalSignInException("The ID Token has no valid sub.");
        }

        token.TryGetPayloadValue<string>("email", out var email);

        // Some providers send the flag as a string.
        var emailVerified = (token.TryGetPayloadValue<bool>("email_verified", out var verified) && verified)
            || (token.TryGetPayloadValue<string>("email_verified", out var verifiedText)
                && string.Equals(verifiedText, "true", StringComparison.OrdinalIgnoreCase));

        return new ExternalIdentityClaims(subject, email, emailVerified);
    }

    private static string DescribeFailure(Exception? exception)
    {
        return exception switch
        {
            SecurityTokenInvalidIssuerException => "The ID Token was issued by another Issuer (iss).",
            SecurityTokenInvalidAudienceException => "The ID Token was issued to another Client (aud).",
            SecurityTokenExpiredException => "The ID Token has expired.",
            SecurityTokenNotYetValidException => "The ID Token is not yet valid.",
            SecurityTokenNoExpirationException => "The ID Token has no exp.",
            SecurityTokenInvalidAlgorithmException => "The ID Token is signed with an algorithm that is not allowed.",
            SecurityTokenSignatureKeyNotFoundException => "The ID Token is signed with an unknown key.",
            SecurityTokenInvalidSignatureException => "The signature of the ID Token is invalid.",
            SecurityTokenMalformedException => "The ID Token is malformed.",
            _ => $"The ID Token is invalid ({exception?.GetType().Name ?? "unknown reason"})."
        };
    }

    // The error code comes from the provider, so only harmless characters reach the logs and the audit log.
    private static string Sanitize(string value)
    {
        var characters = value
            .Take(MaxErrorLength)
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' ? character : '?')
            .ToArray();

        return new string(characters);
    }
}
