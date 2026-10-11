// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;

using Authora.Core.Application.Account.Sessions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Authora.Api.Authentication;

/// <summary>
///     Authenticates browser requests by the Session cookie, and sends Users who are not signed in to the sign-in page.
/// </summary>
/// <remarks>
///     The Session is checked on the server for every request, so a revoked Session or a User who can no longer sign
///     in is rejected right away. An invalid cookie is deleted.
/// </remarks>
/// <param name="options">The options of the scheme.</param>
/// <param name="logger">The LoggerFactory of the handler.</param>
/// <param name="encoder">The UrlEncoder of the handler.</param>
/// <param name="validateSessionService">The Service to validate the Session token.</param>
public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ValidateSessionService validateSessionService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    ///     Name of the authentication scheme.
    /// </summary>
    public const string SchemeName = "AuthoraSession";

    /// <summary>
    ///     Path of the sign-in page.
    /// </summary>
    public const string SignInPath = "/account/login";

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sessionToken = SessionCookie.Read(Request);

        if (string.IsNullOrEmpty(sessionToken))
        {
            return AuthenticateResult.NoResult();
        }

        var session = await validateSessionService.ExecuteAsync(sessionToken, Context.RequestAborted);

        if (session is null)
        {
            SessionCookie.Delete(Response);
            return AuthenticateResult.NoResult();
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(SessionClaimTypes.Subject, session.UserId.ToString()),
                new Claim(SessionClaimTypes.SessionId, session.SessionId.ToString()),
                new Claim(SessionClaimTypes.Email, session.Email),
                new Claim(SessionClaimTypes.Name, $"{session.FirstName} {session.LastName}"),
                new Claim(
                    SessionClaimTypes.AuthenticationTime,
                    new DateTimeOffset(session.AuthenticatedAt, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    ClaimValueTypes.Integer64)
            ],
            SchemeName,
            SessionClaimTypes.Name,
            roleType: null);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <inheritdoc/>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Only local URLs are used, so the sign-in page cannot be turned into an open redirect.
        var returnUrl = properties.RedirectUri ?? $"{Request.PathBase}{Request.Path}{Request.QueryString}";

        Response.Redirect($"{Request.PathBase}{SignInPath}?returnUrl={Uri.EscapeDataString(returnUrl)}");

        return Task.CompletedTask;
    }
}
