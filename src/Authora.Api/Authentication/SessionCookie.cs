// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.Authentication;

/// <summary>
///     The cookie that holds the Session token of a signed-in User.
/// </summary>
/// <remarks>
///     The <c>__Host-</c> prefix makes browsers require <c>Secure</c> and <c>Path=/</c> and reject a <c>Domain</c>,
///     so the cookie cannot be set or overwritten by a subdomain. <c>HttpOnly</c> keeps it away from scripts.
///     <c>SameSite=Lax</c> still sends it when a Client redirects the User to /oauth/authorize.
///     It has no expiry, so it ends when the browser closes; the server enforces the Session lifetimes.
/// </remarks>
internal static class SessionCookie
{
    /// <summary>
    ///     Name of the cookie.
    /// </summary>
    public const string Name = "__Host-authora_session";

    /// <summary>
    ///     Reads the Session token from the request.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <returns>The Session token, or null if there is no cookie.</returns>
    public static string? Read(HttpRequest request)
    {
        return request.Cookies[Name];
    }

    /// <summary>
    ///     Sets the cookie with a new Session token.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="sessionToken">The plain-text Session token.</param>
    public static void Append(HttpResponse response, string sessionToken)
    {
        response.Cookies.Append(Name, sessionToken, CreateOptions());
    }

    /// <summary>
    ///     Deletes the cookie.
    /// </summary>
    /// <param name="response">The current response.</param>
    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(Name, CreateOptions());
    }

    private static CookieOptions CreateOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true
        };
    }
}
