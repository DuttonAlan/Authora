// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.OAuth;
using Authora.Api.Oidc;
using Authora.Core.Abstractions;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.Oidc;
using Authora.Core.Application.Oidc.Jwks;
using Authora.Core.Application.Oidc.UserInfo;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the OpenID Connect endpoints: discovery, JWKS and UserInfo.
/// </summary>
/// <param name="options">The OAuth settings with the Issuer.</param>
/// <param name="signingKeyProvider">The provider of the published keys.</param>
/// <param name="getUserInfoService">The Service behind the UserInfo endpoint.</param>
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class OidcController(
    IOptions<OAuthOptions> options,
    ISigningKeyProvider signingKeyProvider,
    GetUserInfoService getUserInfoService) : ControllerBase
{
    /// <summary>
    ///     How long Clients may cache the discovery document and the JWKS. A new key must be published at least this
    ///     long before it signs, see <see cref="OidcOptions.AdditionalKeyPaths"/>.
    /// </summary>
    public const int MetadataCacheDurationSeconds = 3600;

    private const string BearerPrefix = "Bearer ";

    /// <summary>
    ///     The discovery document (OpenID Connect Discovery 1.0, Section 4), at the path the specification requires.
    /// </summary>
    /// <returns>200 OK with the OpenID Provider Metadata.</returns>
    [HttpGet("/.well-known/openid-configuration")]
    [EnableCors(OAuthCorsPolicyProvider.MetadataPolicyName)]
    [ResponseCache(Duration = MetadataCacheDurationSeconds, Location = ResponseCacheLocation.Any)]
    public IActionResult Discovery()
    {
        return Ok(DiscoveryDocument.Create(options.Value.Issuer));
    }

    /// <summary>
    ///     The JWKS endpoint (RFC 7517). Publishes the public part of the signing key and of the keys kept for rotation.
    /// </summary>
    /// <returns>200 OK with the JSON Web Key Set.</returns>
    [HttpGet("/.well-known/jwks.json")]
    [EnableCors(OAuthCorsPolicyProvider.MetadataPolicyName)]
    [ResponseCache(Duration = MetadataCacheDurationSeconds, Location = ResponseCacheLocation.Any)]
    public IActionResult Jwks()
    {
        return Ok(JsonWebKeySet.From(signingKeyProvider.PublishedKeys));
    }

    /// <summary>
    ///     The UserInfo endpoint (OpenID Connect Core 1.0, Section 5.3). Returns the claims of the User of the Access
    ///     Token sent as <c>Authorization: Bearer</c> (RFC 6750, Section 2.1).
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the claims, 401 for a missing or inactive token, or 403 without the Scope <c>openid</c>.</returns>
    [HttpGet("/oauth/userinfo")]
    [HttpPost("/oauth/userinfo")]
    [EnableCors(OAuthCorsPolicyProvider.UserInfoPolicyName)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> UserInfo(CancellationToken cancellationToken)
    {
        string? authorization = Request.Headers.Authorization;

        if (authorization is null || !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // RFC 6750, Section 3.1: no error code if the request has no token at all.
            Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
            return Unauthorized();
        }

        var token = authorization[BearerPrefix.Length..].Trim();

        try
        {
            if (token.Length == 0)
            {
                throw new OAuthException(OAuthErrors.InvalidToken, "The Access Token is missing.");
            }

            return Ok(await getUserInfoService.ExecuteAsync(token, cancellationToken));
        }
        catch (OAuthException exception)
        {
            var challenge = $"Bearer error=\"{exception.Error}\", error_description=\"{exception.Message}\"";

            if (exception.Error == OAuthErrors.InsufficientScope)
            {
                Response.Headers.Append(HeaderNames.WWWAuthenticate, $"{challenge}, scope=\"{OidcScopes.OpenId}\"");
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            Response.Headers.Append(HeaderNames.WWWAuthenticate, challenge);
            return Unauthorized();
        }
    }
}
