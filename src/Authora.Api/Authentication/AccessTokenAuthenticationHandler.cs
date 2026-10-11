// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Claims;
using System.Text.Encodings.Web;

using Authora.Core.Application.Authorization;
using Authora.Core.Application.OAuth.Introspect;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Authora.Api.Authentication;

/// <summary>
///     Authenticates API requests by an Access Token issued by Authora, sent as <c>Authorization: Bearer</c>
///     (RFC 6750).
/// </summary>
/// <remarks>
///     The token is checked in the database on every request, like the introspection endpoint does, so a revoked
///     token, a disabled Client or a User who can no longer sign in is rejected right away. Cookies are ignored,
///     so the API cannot be called through cross-site requests of a signed-in browser.
/// </remarks>
/// <param name="options">The options of the scheme.</param>
/// <param name="logger">The LoggerFactory of the handler.</param>
/// <param name="encoder">The UrlEncoder of the handler.</param>
/// <param name="accessTokenValidator">The validator of the Access Token.</param>
public sealed class AccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AccessTokenValidator accessTokenValidator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    ///     Name of the authentication scheme.
    /// </summary>
    public const string SchemeName = "AuthoraBearer";

    private const string BearerPrefix = "Bearer ";

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authorization = Request.Headers.Authorization;

        if (authorization is null || !authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization[BearerPrefix.Length..].Trim();

        if (token.Length == 0)
        {
            return AuthenticateResult.Fail("The Access Token is missing.");
        }

        var accessToken = await accessTokenValidator.ValidateAsync(token, Context.RequestAborted);

        if (accessToken is null)
        {
            return AuthenticateResult.Fail("The Access Token is not active.");
        }

        var claims = new List<Claim>
        {
            new(AccessTokenClaimTypes.Subject, accessToken.UserId.ToString()),
            new(AccessTokenClaimTypes.ClientId, accessToken.ClientId.ToString())
        };

        claims.AddRange(accessToken.Scopes.Select(scope => new Claim(AccessTokenClaimTypes.Scope, scope)));

        var identity = new ClaimsIdentity(claims, SchemeName, AccessTokenClaimTypes.Subject, roleType: null);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <inheritdoc/>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var result = await HandleAuthenticateOnceSafeAsync();

        Response.StatusCode = StatusCodes.Status401Unauthorized;

        // RFC 6750, Section 3: no error code if no token was sent, "invalid_token" if it was rejected.
        Response.Headers.Append(
            HeaderNames.WWWAuthenticate,
            result.Failure is null ? "Bearer" : "Bearer error=\"invalid_token\"");
    }

    /// <inheritdoc/>
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        var result = await HandleAuthenticateOnceSafeAsync();

        Response.StatusCode = StatusCodes.Status403Forbidden;

        // A missing Scope can be fixed by the Client with a new token, a missing Permission only by an administrator.
        if (result.Principal?.HasClaim(AccessTokenClaimTypes.Scope, AdminApiScope.Name) == false)
        {
            Response.Headers.Append(
                HeaderNames.WWWAuthenticate,
                $"Bearer error=\"insufficient_scope\", scope=\"{AdminApiScope.Name}\"");
        }
    }
}
