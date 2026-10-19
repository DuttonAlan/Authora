// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Text.Json.Serialization;

using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.Authorize;
using Authora.Core.Application.Oidc;
using Authora.Core.Domain.Enums;

namespace Authora.Api.Oidc;

/// <summary>
///     The OpenID Provider Metadata of the discovery endpoint (OpenID Connect Discovery 1.0, Section 3, RFC 8414).
/// </summary>
/// <remarks>
///     Only lists what Authora actually supports. Members whose default would claim more than that, e.g.
///     <c>request_uri_parameter_supported</c> (default <see langword="true"/>), are set explicitly.
/// </remarks>
internal sealed record DiscoveryDocument
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("userinfo_endpoint")]
    public required string UserInfoEndpoint { get; init; }

    [JsonPropertyName("jwks_uri")]
    public required string JwksUri { get; init; }

    [JsonPropertyName("revocation_endpoint")]
    public required string RevocationEndpoint { get; init; }

    [JsonPropertyName("introspection_endpoint")]
    public required string IntrospectionEndpoint { get; init; }

    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string> ScopesSupported { get; } = OidcScopes.All.Select(scope => scope.Name).ToList();

    [JsonPropertyName("response_types_supported")]
    public IReadOnlyList<string> ResponseTypesSupported { get; } = [ValidateAuthorizationRequestService.ResponseTypeCode];

    [JsonPropertyName("response_modes_supported")]
    public IReadOnlyList<string> ResponseModesSupported { get; } = [ValidateAuthorizationRequestService.ResponseModeQuery];

    [JsonPropertyName("grant_types_supported")]
    public IReadOnlyList<string> GrantTypesSupported { get; } = [GrantTypeNames.AuthorizationCode, GrantTypeNames.RefreshToken];

    [JsonPropertyName("subject_types_supported")]
    public IReadOnlyList<string> SubjectTypesSupported { get; } = ["public"];

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public IReadOnlyList<string> IdTokenSigningAlgValuesSupported { get; } = [SigningKey.Algorithm];

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public IReadOnlyList<string> TokenEndpointAuthMethodsSupported { get; } = ["client_secret_basic", "client_secret_post", "none"];

    [JsonPropertyName("revocation_endpoint_auth_methods_supported")]
    public IReadOnlyList<string> RevocationEndpointAuthMethodsSupported { get; } = ["client_secret_basic", "client_secret_post", "none"];

    // Only Confidential Clients may introspect.
    [JsonPropertyName("introspection_endpoint_auth_methods_supported")]
    public IReadOnlyList<string> IntrospectionEndpointAuthMethodsSupported { get; } = ["client_secret_basic", "client_secret_post"];

    [JsonPropertyName("code_challenge_methods_supported")]
    public IReadOnlyList<string> CodeChallengeMethodsSupported { get; } = [Pkce.MethodS256];

    [JsonPropertyName("prompt_values_supported")]
    public IReadOnlyList<string> PromptValuesSupported { get; } = PromptValues.All;

    [JsonPropertyName("claims_supported")]
    public IReadOnlyList<string> ClaimsSupported { get; } =
        ["iss", "aud", "exp", "iat", "auth_time", "nonce", "at_hash", .. IdentityClaims.All];

    [JsonPropertyName("claims_parameter_supported")]
    public bool ClaimsParameterSupported { get; } = false;

    [JsonPropertyName("request_parameter_supported")]
    public bool RequestParameterSupported { get; } = false;

    [JsonPropertyName("request_uri_parameter_supported")]
    public bool RequestUriParameterSupported { get; } = false;

    // RFC 9207: authorization responses contain iss.
    [JsonPropertyName("authorization_response_iss_parameter_supported")]
    public bool AuthorizationResponseIssParameterSupported { get; } = true;

    /// <summary>
    ///     Creates the document for an Issuer. All endpoints are below the Issuer, so they stay consistent with it.
    /// </summary>
    /// <param name="issuer">The Issuer Identifier, without a trailing slash.</param>
    /// <returns>The discovery document.</returns>
    public static DiscoveryDocument Create(string issuer)
    {
        return new DiscoveryDocument
        {
            Issuer = issuer,
            AuthorizationEndpoint = $"{issuer}/oauth/authorize",
            TokenEndpoint = $"{issuer}/oauth/token",
            UserInfoEndpoint = $"{issuer}/oauth/userinfo",
            JwksUri = $"{issuer}/.well-known/jwks.json",
            RevocationEndpoint = $"{issuer}/oauth/revoke",
            IntrospectionEndpoint = $"{issuer}/oauth/introspect"
        };
    }
}
