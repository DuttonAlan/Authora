// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Authora.Core.Application.OAuth;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Authora.Api.IntegrationTests;

/// <summary>
///     Answers the requests of the Api to external identity providers instead of the network. Each test registers a
///     <see cref="FakeOidcProvider"/> with an Issuer of its own, so the cached discovery documents never mix.
/// </summary>
public sealed class FakeOidcProviderHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, FakeOidcProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Creates a provider with a new Issuer.
    /// </summary>
    /// <param name="clientId">The Client Id of Authora at the provider.</param>
    /// <param name="clientSecret">The Client Secret of Authora at the provider, or null for a Public Client.</param>
    /// <returns>The provider.</returns>
    public FakeOidcProvider Create(string clientId = "authora-client", string? clientSecret = "provider-secret")
    {
        var provider = new FakeOidcProvider($"https://{Guid.NewGuid():N}.idp.example", clientId, clientSecret);
        _providers[new Uri(provider.Issuer).Host] = provider;

        return provider;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_providers.TryGetValue(request.RequestUri!.Host, out var provider))
        {
            throw new HttpRequestException($"No fake provider at {request.RequestUri.Host}.");
        }

        return await provider.HandleAsync(request, cancellationToken);
    }
}

/// <summary>
///     An OpenID Connect provider for one test: discovery, key set, and a token endpoint that redeems the codes
///     <see cref="Authorize"/> issued, with PKCE and Client authentication checked like a real provider.
/// </summary>
public sealed class FakeOidcProvider
{
    private readonly ConcurrentDictionary<string, IssuedCode> _codes = new(StringComparer.Ordinal);

    private readonly ConcurrentQueue<TokenRequest> _tokenRequests = new();

    internal FakeOidcProvider(string issuer, string clientId, string? clientSecret)
    {
        Issuer = issuer;
        ClientId = clientId;
        ClientSecret = clientSecret;
        SigningKey = CreateKey(Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    ///     The Issuer of the provider.
    /// </summary>
    public string Issuer { get; }

    /// <summary>
    ///     The Client Id of Authora at the provider.
    /// </summary>
    public string ClientId { get; }

    /// <summary>
    ///     The Client Secret of Authora at the provider, or null.
    /// </summary>
    public string? ClientSecret { get; }

    /// <summary>
    ///     The key the provider publishes and signs ID Tokens with.
    /// </summary>
    public RsaSecurityKey SigningKey { get; }

    /// <summary>
    ///     The token endpoint authentication methods the discovery document lists, or null for none.
    /// </summary>
    public IReadOnlyList<string>? TokenEndpointAuthMethods { get; set; }

    /// <summary>
    ///     The Issuer the discovery document names; the real Issuer by default.
    /// </summary>
    public string? DiscoveryIssuer { get; set; }

    /// <summary>
    ///     Changes the claims of the next ID Tokens, e.g. to issue one with a wrong <c>aud</c>.
    /// </summary>
    public Action<Dictionary<string, object>>? ChangeClaims { get; set; }

    /// <summary>
    ///     The key that signs the next ID Tokens instead of <see cref="SigningKey"/>, e.g. one that is not published.
    /// </summary>
    public SecurityKey? OtherSigningKey { get; set; }

    /// <summary>
    ///     The token requests received so far.
    /// </summary>
    public IReadOnlyList<TokenRequest> TokenRequests => [.. _tokenRequests];

    /// <summary>
    ///     The authorization endpoint.
    /// </summary>
    public string AuthorizationEndpoint => HttpAuthorizationEndpoint ? $"http://{new Uri(Issuer).Host}/authorize" : $"{Issuer}/authorize";

    /// <summary>
    ///     Whether the discovery document names an authorization endpoint with plain HTTP.
    /// </summary>
    public bool HttpAuthorizationEndpoint { get; set; }

    /// <summary>
    ///     Bytes of padding added to the key set, e.g. to make it larger than Authora accepts.
    /// </summary>
    public int KeySetPadding { get; set; }

    /// <summary>
    ///     Signs in an account at the provider for an authorization request, like the provider's own pages do.
    /// </summary>
    /// <param name="authorizationUrl">The URL Authora sent the browser to.</param>
    /// <param name="subject">The Subject of the account.</param>
    /// <param name="email">The Email of the account.</param>
    /// <returns>The parameters of the authorization response: <c>code</c> and <c>state</c>.</returns>
    public Dictionary<string, string> Authorize(Uri authorizationUrl, string subject, string? email = null)
    {
        ArgumentNullException.ThrowIfNull(authorizationUrl);

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(authorizationUrl.Query);

        Assert.Equal(AuthorizationEndpoint, authorizationUrl.GetLeftPart(UriPartial.Path));
        Assert.Equal("code", query["response_type"].Single());
        Assert.Equal(ClientId, query["client_id"].Single());
        Assert.Equal("S256", query["code_challenge_method"].Single());
        Assert.Contains("openid", query["scope"].Single()!.Split(' '));

        var code = Guid.NewGuid().ToString("N");
        _codes[code] = new IssuedCode(
            subject,
            email,
            query["nonce"].Single()!,
            query["code_challenge"].Single()!,
            query["redirect_uri"].Single()!);

        return new Dictionary<string, string>
        {
            ["code"] = code,
            ["state"] = query["state"].Single()!
        };
    }

    /// <summary>
    ///     Creates an RSA signing key.
    /// </summary>
    /// <param name="keyId">The <c>kid</c> of the key.</param>
    /// <returns>The key.</returns>
    public static RsaSecurityKey CreateKey(string keyId)
    {
        using var rsa = RSA.Create(2048);

        return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true)) { KeyId = keyId };
    }

    internal async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path == "/.well-known/openid-configuration")
        {
            return Json(HttpStatusCode.OK, Discovery());
        }

        if (path == "/jwks")
        {
            return Json(HttpStatusCode.OK, KeySet());
        }

        if (path == "/token" && request.Method == HttpMethod.Post)
        {
            return await TokenAsync(request, cancellationToken);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private Dictionary<string, object> Discovery()
    {
        var discovery = new Dictionary<string, object>
        {
            ["issuer"] = DiscoveryIssuer ?? Issuer,
            ["authorization_endpoint"] = AuthorizationEndpoint,
            ["token_endpoint"] = $"{Issuer}/token",
            ["jwks_uri"] = $"{Issuer}/jwks",
            ["response_types_supported"] = new[] { "code" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" }
        };

        if (TokenEndpointAuthMethods is not null)
        {
            discovery["token_endpoint_auth_methods_supported"] = TokenEndpointAuthMethods;
        }

        return discovery;
    }

    private Dictionary<string, object> KeySet()
    {
        var parameters = SigningKey.Parameters;
        var keySet = new Dictionary<string, object>
        {
            ["keys"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["kty"] = "RSA",
                    ["use"] = "sig",
                    ["alg"] = "RS256",
                    ["kid"] = SigningKey.KeyId,
                    ["n"] = Base64Url.EncodeToString(parameters.Modulus),
                    ["e"] = Base64Url.EncodeToString(parameters.Exponent)
                }
            }
        };

        if (KeySetPadding > 0)
        {
            keySet["padding"] = new string('x', KeySetPadding);
        }

        return keySet;
    }

    private async Task<HttpResponseMessage> TokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(body)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.Ordinal);

        _tokenRequests.Enqueue(new TokenRequest(form, request.Headers.Authorization));

        if (!IsAuthenticated(form, request.Headers.Authorization))
        {
            return Json(HttpStatusCode.Unauthorized, new Dictionary<string, object> { ["error"] = "invalid_client" });
        }

        if (form.GetValueOrDefault("grant_type") != "authorization_code"
            || !_codes.TryRemove(form.GetValueOrDefault("code") ?? string.Empty, out var issued)
            || form.GetValueOrDefault("redirect_uri") != issued.RedirectUri
            || !Pkce.Verify(form.GetValueOrDefault("code_verifier") ?? string.Empty, issued.CodeChallenge))
        {
            return Json(HttpStatusCode.BadRequest, new Dictionary<string, object> { ["error"] = "invalid_grant" });
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["sub"] = issued.Subject,
            ["aud"] = ClientId,
            ["iat"] = now,
            ["exp"] = now + 300,
            ["nonce"] = issued.Nonce
        };

        if (issued.Email is not null)
        {
            claims["email"] = issued.Email;
            claims["email_verified"] = true;
        }

        ChangeClaims?.Invoke(claims);

        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        var idToken = handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            SigningCredentials = new SigningCredentials(OtherSigningKey ?? SigningKey, SecurityAlgorithms.RsaSha256)
        });

        return Json(HttpStatusCode.OK, new Dictionary<string, object>
        {
            ["access_token"] = Guid.NewGuid().ToString("N"),
            ["token_type"] = "Bearer",
            ["expires_in"] = 3600,
            ["id_token"] = idToken
        });
    }

    private bool IsAuthenticated(Dictionary<string, string> form, AuthenticationHeaderValue? authorization)
    {
        if (ClientSecret is null)
        {
            return form.GetValueOrDefault("client_id") == ClientId && !form.ContainsKey("client_secret");
        }

        if (authorization is { Scheme: "Basic", Parameter: { } parameter })
        {
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(parameter)).Split(':', 2);

            return Uri.UnescapeDataString(credentials[0]) == ClientId && Uri.UnescapeDataString(credentials[1].Replace('+', ' ')) == ClientSecret;
        }

        return form.GetValueOrDefault("client_id") == ClientId && form.GetValueOrDefault("client_secret") == ClientSecret;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object value)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }

    private sealed record IssuedCode(string Subject, string? Email, string Nonce, string CodeChallenge, string RedirectUri);
}

/// <summary>
///     A request the Api sent to the token endpoint of a fake provider.
/// </summary>
/// <param name="Form">The form parameters.</param>
/// <param name="Authorization">The Authorization header, or null.</param>
public sealed record TokenRequest(IReadOnlyDictionary<string, string> Form, AuthenticationHeaderValue? Authorization);
