// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Authora.Api.AdminUi;
using Authora.Api.Authentication;
using Authora.Api.RateLimiting;
using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.AdminUi;
using Authora.Core.Application.AdminUi.GetSettings;
using Authora.Core.Application.OAuth;
using Authora.Core.Application.OAuth.ClientAuthentication;
using Authora.Core.Application.OAuth.Revoke;
using Authora.Core.Application.OAuth.Token;
using Authora.Core.Domain.Enums;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Authora.Api.Controllers;

/// <summary>
///     The backend for frontend of the admin interface (US-033, TD-014): runs the Authorization Code flow with PKCE
///     for its Public Client on the server and keeps the tokens in an encrypted <c>HttpOnly</c> cookie, so no script
///     in the browser ever sees a token. The admin API gets the Access Token from the cookie, see
///     <see cref="AdminUiTokenMiddleware"/>.
/// </summary>
/// <remarks>
///     These endpoints are opened by the browser of the admin interface, not called by Clients, so they are not part
///     of the API description. The <c>state</c> and the PKCE <c>code_verifier</c> stay on the server, so a script on
///     the admin interface that starts its own authorization request cannot redeem the code: it is sent to
///     <see cref="AdminUiOptions.CallbackPath"/>, which only redeems codes of sign-ins it started itself.
/// </remarks>
/// <param name="getAdminUiSettingsService">The Service to find the Client of the admin interface.</param>
/// <param name="issueTokenService">The Service to exchange the code and the Refresh Token.</param>
/// <param name="revokeTokenService">The Service to revoke the Grant on sign-out.</param>
/// <param name="cookies">Reads and writes the encrypted cookies.</param>
/// <param name="secretGenerator">Generates the <c>state</c> and the <c>code_verifier</c>.</param>
/// <param name="oauthOptions">The OAuth settings with the Issuer.</param>
/// <param name="validateSessionService">The Service to check the Session the tokens are bound to.</param>
/// <param name="timeProvider">The clock for the issue time of the tokens.</param>
/// <param name="logger">The Logger to record failed sign-ins.</param>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed partial class AdminUiController(
    GetAdminUiSettingsService getAdminUiSettingsService,
    IssueTokenService issueTokenService,
    RevokeTokenService revokeTokenService,
    AdminUiCookies cookies,
    ISecretGenerator secretGenerator,
    IOptions<OAuthOptions> oauthOptions,
    ValidateSessionService validateSessionService,
    TimeProvider timeProvider,
    ILogger<AdminUiController> logger) : ControllerBase
{
    /// <summary>
    ///     The error the callback sends to the admin interface when its Client is disabled or missing.
    /// </summary>
    public const string UnavailableError = "unavailable";

    /// <summary>
    ///     The error the callback sends to the admin interface when the User denied the request.
    /// </summary>
    public const string AccessDeniedError = "access_denied";

    /// <summary>
    ///     The error the callback sends to the admin interface when the response does not belong to its sign-in.
    /// </summary>
    public const string InvalidResponseError = "invalid_response";

    /// <summary>
    ///     The error the callback sends to the admin interface when the code could not be exchanged.
    /// </summary>
    public const string SignInFailedError = "sign_in_failed";

    /// <summary>
    ///     How long after the tokens were issued a refresh is skipped. Another tab of the admin interface has just
    ///     refreshed them, and using the Refresh Token again would count as reuse and revoke the Grant.
    /// </summary>
    private static readonly TimeSpan RefreshSkipWindow = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Starts signing in: sends the browser to the authorization endpoint with a new <c>state</c> and PKCE
    ///     challenge. The admin interface is a first-party Client (TD-012), so a signed-in User comes right back.
    /// </summary>
    /// <param name="returnTo" example="#/users">The view to show after signing in.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>302 Found to the authorization endpoint, or to the admin interface with an error.</returns>
    /// <response code="302">Redirects to the authorization endpoint, or to the admin interface with <c>error=unavailable</c> if its Client is disabled or missing.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet(AdminUiOptions.Path + "/signin")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> StartSignIn([FromQuery] string? returnTo, CancellationToken cancellationToken)
    {
        var settings = await getAdminUiSettingsService.ExecuteAsync(cancellationToken);

        if (settings is null)
        {
            return RedirectToAdminUi(string.Empty, UnavailableError);
        }

        var signIn = new AdminUiSignIn(
            secretGenerator.Generate(),
            secretGenerator.Generate(),
            returnTo is not null && ReturnToPattern().IsMatch(returnTo) ? returnTo : string.Empty);

        cookies.AppendSignIn(Response, signIn);

        var location = QueryHelpers.AddQueryString($"{Request.PathBase}/oauth/authorize", new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.ClientId.ToString(),
            ["redirect_uri"] = settings.RedirectUri,
            ["scope"] = settings.Scope,
            ["state"] = signIn.State,
            ["code_challenge"] = Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(signIn.CodeVerifier))),
            ["code_challenge_method"] = Pkce.MethodS256
        });

        return Redirect(location);
    }

    /// <summary>
    ///     Completes signing in: checks that the authorization response belongs to the sign-in of this browser,
    ///     exchanges the code on the server and stores the tokens in the encrypted cookie.
    /// </summary>
    /// <param name="code">The Authorization Code.</param>
    /// <param name="state">The <c>state</c> of the sign-in.</param>
    /// <param name="iss">The Issuer of the response (RFC 9207).</param>
    /// <param name="error">The error of the authorization endpoint, or null.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>302 Found to the admin interface.</returns>
    /// <response code="302">Redirects to the view the sign-in started from, or to the admin interface with an <c>error</c>.</response>
    /// <response code="429">Responds if the IP address sent too many token requests.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpGet(AdminUiOptions.CallbackPath)]
    [EnableRateLimiting(RateLimitPolicies.Token)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> CompleteSignIn(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? iss,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        // Taken in any case, so a response can be used at most once.
        var signIn = cookies.TakeSignIn(HttpContext);

        // The state ties the response to the sign-in of this browser (CSRF), the iss to Authora (RFC 9207). A code
        // that does not belong to a sign-in of this browser is never redeemed.
        if (signIn is null
            || state is null
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(signIn.State))
            || iss != oauthOptions.Value.Issuer)
        {
            logger.AdminUiSignInFailed(InvalidResponseError);
            return RedirectToAdminUi(string.Empty, InvalidResponseError);
        }

        if (error is not null || string.IsNullOrEmpty(code))
        {
            logger.AdminUiSignInFailed(error ?? "no code");
            return RedirectToAdminUi(signIn.ReturnTo, error == OAuthErrors.AccessDenied ? AccessDeniedError : SignInFailedError);
        }

        var settings = await getAdminUiSettingsService.ExecuteAsync(cancellationToken);

        if (settings is null)
        {
            return RedirectToAdminUi(signIn.ReturnTo, UnavailableError);
        }

        // The code was just issued for the Session of this browser. The tokens are bound to it, so they are not
        // refreshed once it ends, see RefreshSession.
        var session = await GetSessionAsync(cancellationToken);

        if (session is null)
        {
            logger.AdminUiSignInFailed("no session");
            return RedirectToAdminUi(signIn.ReturnTo, SignInFailedError);
        }

        TokenResponse tokens;

        try
        {
            tokens = await issueTokenService.ExecuteAsync(
                new TokenRequest(
                    GrantTypeNames.AuthorizationCode,
                    code,
                    settings.RedirectUri,
                    signIn.CodeVerifier,
                    RefreshToken: null,
                    Scope: null,
                    new ClientCredentials(settings.ClientId.ToString(), ClientSecret: null)),
                cancellationToken);
        }
        catch (OAuthException exception)
        {
            logger.AdminUiSignInFailed(exception.Error);
            return RedirectToAdminUi(signIn.ReturnTo, SignInFailedError);
        }

        var now = timeProvider.GetUtcNow();
        cookies.AppendTokens(Response, new AdminUiTokens(tokens.AccessToken, tokens.RefreshToken, now, session.SessionId));

        return RedirectToAdminUi(signIn.ReturnTo, error: null);
    }

    /// <summary>
    ///     Exchanges the Refresh Token of the cookie for new tokens, e.g. after the admin API answered 401.
    /// </summary>
    /// <remarks>
    ///     Skipped if the tokens were issued in the last seconds, because another tab of the admin interface has just
    ///     refreshed them. The admin interface refreshes in one tab at a time (Web Locks), so the other tabs then find
    ///     the new cookie.
    /// </remarks>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content with the new cookie, or 401 Unauthorized.</returns>
    /// <response code="204">Responds with no content once the cookie holds valid tokens.</response>
    /// <response code="400">Responds with problem details if the header <c>X-Authora-Admin: 1</c> is missing.</response>
    /// <response code="401">Responds with no content and deletes the cookie if the User must sign in again.</response>
    /// <response code="429">Responds if the IP address sent too many token requests.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost(AdminUiOptions.Path + "/session/refresh")]
    [EnableRateLimiting(RateLimitPolicies.Token)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> RefreshSession(CancellationToken cancellationToken)
    {
        if (!HasAdminUiHeader())
        {
            return MissingHeader();
        }

        if (!cookies.TryReadTokens(Request, out var current))
        {
            return Unauthorized();
        }

        var now = timeProvider.GetUtcNow();

        if (now - current.IssuedAt < RefreshSkipWindow)
        {
            return NoContent();
        }

        var settings = await getAdminUiSettingsService.ExecuteAsync(cancellationToken);

        if (current.RefreshToken is null || settings is null)
        {
            AdminUiCookies.DeleteTokens(Response);
            return Unauthorized();
        }

        // The admin interface stays signed in only as long as the Session it signed in with: once that Session expired,
        // went idle, was signed out or its User can no longer sign in, signing in again needs the password. The
        // Access Token in the cookie stays usable until it expires, like any other Access Token.
        var session = await GetSessionAsync(cancellationToken);

        if (session is null || session.SessionId != current.SessionId)
        {
            AdminUiCookies.DeleteTokens(Response);
            await RevokeAsync(settings, current, cancellationToken);

            return Unauthorized();
        }

        try
        {
            var tokens = await issueTokenService.ExecuteAsync(
                new TokenRequest(
                    GrantTypeNames.RefreshToken,
                    Code: null,
                    RedirectUri: null,
                    CodeVerifier: null,
                    current.RefreshToken,
                    Scope: null,
                    new ClientCredentials(settings.ClientId.ToString(), ClientSecret: null)),
                cancellationToken);

            cookies.AppendTokens(Response, new AdminUiTokens(tokens.AccessToken, tokens.RefreshToken, now, current.SessionId));

            return NoContent();
        }
        catch (OAuthException exception)
        {
            logger.AdminUiRefreshFailed(exception.Error);

            AdminUiCookies.DeleteTokens(Response);
            return Unauthorized();
        }
    }

    /// <summary>
    ///     Signs out of the admin interface: revokes the Grant and deletes the cookie. The admin interface then opens
    ///     the sign-out page of Authora, which ends the Session.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>204 No Content.</returns>
    /// <response code="204">Responds with no content once the cookie is deleted, also if there was none.</response>
    /// <response code="400">Responds with problem details if the header <c>X-Authora-Admin: 1</c> is missing.</response>
    /// <response code="500">Responds with problem details if an unexpected error occurred.</response>
    [HttpPost(AdminUiOptions.Path + "/session/signout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<IActionResult> EndSession(CancellationToken cancellationToken)
    {
        if (!HasAdminUiHeader())
        {
            return MissingHeader();
        }

        AdminUiCookies.DeleteTokens(Response);

        var settings = await getAdminUiSettingsService.ExecuteAsync(cancellationToken);

        if (settings is not null && cookies.TryReadTokens(Request, out var tokens))
        {
            await RevokeAsync(settings, tokens, cancellationToken);
        }

        return NoContent();
    }

    /// <summary>
    ///     Revokes the tokens of the cookie. Revoking the Refresh Token revokes the whole Grant, including the Access Token.
    /// </summary>
    private async Task RevokeAsync(AdminUiSettingsResponse settings, AdminUiTokens tokens, CancellationToken cancellationToken)
    {
        var (token, hint) = tokens.RefreshToken is null
            ? (tokens.AccessToken, TokenTypeHints.AccessToken)
            : (tokens.RefreshToken, TokenTypeHints.RefreshToken);

        await revokeTokenService.ExecuteAsync(
            new ClientCredentials(settings.ClientId.ToString(), ClientSecret: null),
            token,
            hint,
            cancellationToken);
    }

    /// <summary>
    ///     Gets the valid Session of the Session cookie this browser sent, or null.
    /// </summary>
    private async Task<AuthenticatedSession?> GetSessionAsync(CancellationToken cancellationToken)
    {
        var sessionToken = SessionCookie.Read(Request);

        return sessionToken is null ? null : await validateSessionService.ExecuteAsync(sessionToken, cancellationToken);
    }

    /// <summary>
    ///     Whether the request carries the header of the admin interface, which a cross-site request cannot send
    ///     without a CORS preflight.
    /// </summary>
    private bool HasAdminUiHeader()
    {
        return Request.Headers[AdminUiTokenMiddleware.HeaderName] == "1";
    }

    private ObjectResult MissingHeader()
    {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            detail: $"The header '{AdminUiTokenMiddleware.HeaderName}: 1' is required.");
    }

    private RedirectResult RedirectToAdminUi(string returnTo, string? error)
    {
        var path = $"{Request.PathBase}{AdminUiOptions.Path}/";

        if (error is not null)
        {
            path = QueryHelpers.AddQueryString(path, "error", error);
        }

        return Redirect($"{path}{returnTo}");
    }

    // A view of the admin interface (hash routing), e.g. "#/users/{id}?page=2". Anything else could send the
    // browser elsewhere after signing in.
    [GeneratedRegex("^#/[A-Za-z0-9/?=&%._~-]{0,500}$")]
    private static partial Regex ReturnToPattern();
}
