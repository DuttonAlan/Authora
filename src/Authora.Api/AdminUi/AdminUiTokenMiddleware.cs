// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Net.Http.Headers;

namespace Authora.Api.AdminUi;

/// <summary>
///     Sends the Access Token of the admin interface's cookie to the admin API as a Bearer token (TD-014), so the
///     API keeps accepting only Access Tokens and checks them like those of every other Client.
/// </summary>
/// <remarks>
///     Only for <c>/api/*</c> requests without an Authorization header that carry <see cref="HeaderName"/>.
///     Browsers send a custom header cross-origin only after a CORS preflight, which <c>/api/*</c> never allows, so a
///     signed-in browser cannot be abused through cross-site requests; the cookie is also <c>SameSite=Strict</c>.
///     Requests that a browser marks as not same-origin are ignored as well.
/// </remarks>
/// <param name="next">The next middleware.</param>
/// <param name="cookies">Reads the cookie with the tokens.</param>
internal sealed class AdminUiTokenMiddleware(RequestDelegate next, AdminUiCookies cookies)
{
    /// <summary>
    ///     The header the admin interface sends with every call of the admin API.
    /// </summary>
    public const string HeaderName = "X-Authora-Admin";

    /// <summary>
    ///     Adds the Authorization header if the request comes from the admin interface.
    /// </summary>
    /// <param name="context">The HttpContext of the request.</param>
    /// <returns>A Task that completes once the request is handled.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;

        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !request.Headers.ContainsKey(HeaderNames.Authorization)
            && request.Headers[HeaderName] == "1"
            && IsSameOrigin(request)
            && cookies.TryReadTokens(request, out var tokens))
        {
            request.Headers.Authorization = $"Bearer {tokens.AccessToken}";
        }

        return next(context);
    }

    /// <summary>
    ///     Whether the request is same-origin according to the Fetch Metadata the browser sends, if it sends any.
    /// </summary>
    private static bool IsSameOrigin(HttpRequest request)
    {
        var site = request.Headers["Sec-Fetch-Site"].ToString();

        return site.Length == 0 || site == "same-origin";
    }
}
