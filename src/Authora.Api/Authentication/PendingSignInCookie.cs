// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.Authentication;

/// <summary>
///     The cookie that holds the token of a pending sign-in, between the password and the second factor.
/// </summary>
/// <remarks>
///     Separate from the <see cref="SessionCookie"/>, so a browser that only passed the password is never signed in.
///     Same protection as the Session cookie, but <c>SameSite=Strict</c>: it is only needed on Authora's own pages,
///     after Authora's own redirects. It expires with the pending sign-in.
/// </remarks>
internal static class PendingSignInCookie
{
    /// <summary>
    ///     Name of the cookie.
    /// </summary>
    public const string Name = "__Host-authora_pending_sign_in";

    /// <summary>
    ///     Reads the token of the pending sign-in from the request.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <returns>The token, or null if there is no cookie.</returns>
    public static string? Read(HttpRequest request)
    {
        return request.Cookies[Name];
    }

    /// <summary>
    ///     Sets the cookie with the token of a new pending sign-in.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="challengeToken">The plain-text token.</param>
    /// <param name="lifetime">How long the pending sign-in lasts.</param>
    public static void Append(HttpResponse response, string challengeToken, TimeSpan lifetime)
    {
        var options = CreateOptions();
        options.MaxAge = lifetime;

        response.Cookies.Append(Name, challengeToken, options);
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
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true
        };
    }
}
