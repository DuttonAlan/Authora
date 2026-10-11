// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.OAuth;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.Introspect;
using Authora.Core.Application.OAuth.Revoke;
using Authora.Core.Application.OAuth.Token;

using Microsoft.AspNetCore.Mvc;

namespace Authora.Api.Controllers;

/// <summary>
///     Provides the OAuth 2.1 endpoints that Clients and resource servers call directly:
///     token (RFC 6749), revocation (RFC 7009) and introspection (RFC 7662).
/// </summary>
/// <remarks>
///     The endpoints take <c>application/x-www-form-urlencoded</c> bodies and answer errors in the OAuth format
///     (<c>{"error": "...", "error_description": "..."}</c>) instead of problem details, as the RFCs require.
///     Responses are never cached, because they contain tokens.
/// </remarks>
/// <param name="issueTokenService">The Service to issue tokens.</param>
/// <param name="revokeTokenService">The Service to revoke tokens.</param>
/// <param name="introspectTokenService">The Service to introspect tokens.</param>
[Route("oauth")]
[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OAuthController(
    IssueTokenService issueTokenService,
    RevokeTokenService revokeTokenService,
    IntrospectTokenService introspectTokenService) : ControllerBase
{
    /// <summary>
    ///     The token endpoint. Exchanges an Authorization Code (with PKCE) or a Refresh Token for new tokens.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the tokens, or an OAuth error.</returns>
    [HttpPost("token")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        try
        {
            var form = await OAuthForm.ReadAsync(Request, cancellationToken);

            var response = await issueTokenService.ExecuteAsync(
                new TokenRequest(
                    form.Get("grant_type"),
                    form.Get("code"),
                    form.Get("redirect_uri"),
                    form.Get("code_verifier"),
                    form.Get("refresh_token"),
                    form.Get("scope"),
                    form.GetClientCredentials()),
                cancellationToken);

            return Ok(response);
        }
        catch (OAuthException exception)
        {
            return OAuthError(exception);
        }
    }

    /// <summary>
    ///     The revocation endpoint. Revokes an Access Token, or a Refresh Token together with its whole Grant.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK, also for unknown tokens, or an OAuth error.</returns>
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        try
        {
            var form = await OAuthForm.ReadAsync(Request, cancellationToken);

            await revokeTokenService.ExecuteAsync(
                form.GetClientCredentials(),
                form.Get("token"),
                form.Get("token_type_hint"),
                cancellationToken);

            return Ok();
        }
        catch (OAuthException exception)
        {
            return OAuthError(exception);
        }
    }

    /// <summary>
    ///     The introspection endpoint. Tells an authenticated Confidential Client, e.g. a resource server, whether a
    ///     token is active and what it grants.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>200 OK with the introspection response, or an OAuth error.</returns>
    [HttpPost("introspect")]
    public async Task<IActionResult> Introspect(CancellationToken cancellationToken)
    {
        try
        {
            var form = await OAuthForm.ReadAsync(Request, cancellationToken);

            var response = await introspectTokenService.ExecuteAsync(
                form.GetClientCredentials(),
                form.Get("token"),
                form.Get("token_type_hint"),
                cancellationToken);

            return Ok(response);
        }
        catch (OAuthException exception)
        {
            return OAuthError(exception);
        }
    }

    /// <summary>
    ///     Writes an OAuth error response (RFC 6749, Section 5.2). A failed Client authentication is answered with
    ///     401 and a <c>WWW-Authenticate</c> header, all other errors with 400.
    /// </summary>
    private ObjectResult OAuthError(OAuthException exception)
    {
        var statusCode = StatusCodes.Status400BadRequest;

        if (exception.Error == OAuthErrors.InvalidClient)
        {
            statusCode = StatusCodes.Status401Unauthorized;
            Response.Headers.WWWAuthenticate = "Basic realm=\"authora\", charset=\"UTF-8\"";
        }

        return new ObjectResult(new OAuthErrorResponse(exception.Error, exception.Message)) { StatusCode = statusCode };
    }
}
