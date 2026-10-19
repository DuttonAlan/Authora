// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;

namespace Authora.Api.AdminUi;

/// <summary>
///     The cookies of the backend for frontend of the admin interface, encrypted with ASP.NET Core Data
///     Protection, so the browser can neither read nor change them.
/// </summary>
/// <remarks>
///     <list type="bullet">
///         <item>
///             <see cref="TokensName"/> holds the Access and Refresh Token. <c>HttpOnly</c> keeps it away from
///             scripts, so a script that runs on the admin interface cannot take the tokens elsewhere.
///             <c>SameSite=Strict</c> keeps it out of cross-site requests. It has no expiry, so it ends when the
///             browser closes, like the Session cookie.
///         </item>
///         <item>
///             <see cref="SignInName"/> holds the <c>state</c> and the PKCE <c>code_verifier</c> of a running sign-in
///             for at most <see cref="SignInLifetime"/>. <c>SameSite=Lax</c>, so it is still sent if the sign-in ends
///             with a redirect from another site.
///         </item>
///     </list>
///     The <c>__Host-</c> prefix makes browsers require <c>Secure</c> and <c>Path=/</c> and reject a <c>Domain</c>.
/// </remarks>
/// <param name="dataProtectionProvider">Creates the protectors of the cookies.</param>
/// <param name="timeProvider">The clock for the lifetime of the sign-in cookie.</param>
public sealed class AdminUiCookies(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
{
    /// <summary>
    ///     Name of the cookie with the tokens.
    /// </summary>
    public const string TokensName = "__Host-authora_admin";

    /// <summary>
    ///     Name of the cookie of a running sign-in.
    /// </summary>
    public const string SignInName = "__Host-authora_admin_signin";

    /// <summary>
    ///     How long a sign-in may take until the authorization response arrives.
    /// </summary>
    public static readonly TimeSpan SignInLifetime = TimeSpan.FromMinutes(10);

    private readonly IDataProtector _tokensProtector = dataProtectionProvider.CreateProtector("Authora.AdminUi.Tokens.v1");

    private readonly ITimeLimitedDataProtector _signInProtector = dataProtectionProvider
        .CreateProtector("Authora.AdminUi.SignIn.v1")
        .ToTimeLimitedDataProtector();

    /// <summary>
    ///     Reads the tokens from the request.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="tokens">The tokens, if the cookie exists and was issued by Authora.</param>
    /// <returns>True if the tokens could be read.</returns>
    public bool TryReadTokens(HttpRequest request, [NotNullWhen(true)] out AdminUiTokens? tokens)
    {
        tokens = Unprotect<AdminUiTokens>(request.Cookies[TokensName], _tokensProtector.Unprotect);

        return tokens is not null;
    }

    /// <summary>
    ///     Sets the cookie with new tokens.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="tokens">The tokens.</param>
    public void AppendTokens(HttpResponse response, AdminUiTokens tokens)
    {
        var value = _tokensProtector.Protect(JsonSerializer.Serialize(tokens));

        response.Cookies.Append(TokensName, value, CreateOptions(SameSiteMode.Strict));
    }

    /// <summary>
    ///     Deletes the cookie with the tokens.
    /// </summary>
    /// <param name="response">The current response.</param>
    public static void DeleteTokens(HttpResponse response)
    {
        response.Cookies.Delete(TokensName, CreateOptions(SameSiteMode.Strict));
    }

    /// <summary>
    ///     Reads and deletes the cookie of a running sign-in. A sign-in cookie can only be used once.
    /// </summary>
    /// <param name="context">The current request and response.</param>
    /// <returns>The sign-in, or null if there is none, it expired or was not issued by Authora.</returns>
    public AdminUiSignIn? TakeSignIn(HttpContext context)
    {
        var signIn = Unprotect<AdminUiSignIn>(context.Request.Cookies[SignInName], _signInProtector.Unprotect);

        context.Response.Cookies.Delete(SignInName, CreateOptions(SameSiteMode.Lax));

        return signIn;
    }

    /// <summary>
    ///     Sets the cookie of a new sign-in.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="signIn">The sign-in.</param>
    public void AppendSignIn(HttpResponse response, AdminUiSignIn signIn)
    {
        var value = _signInProtector.Protect(
            JsonSerializer.Serialize(signIn),
            timeProvider.GetUtcNow().Add(SignInLifetime));

        var options = CreateOptions(SameSiteMode.Lax);
        options.MaxAge = SignInLifetime;

        response.Cookies.Append(SignInName, value, options);
    }

    private static T? Unprotect<T>(string? value, Func<string, string> unprotect)
        where T : class
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(unprotect(value));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            // Tampered with, expired, or encrypted with a key that no longer exists: the User signs in again.
            return null;
        }
    }

    private static CookieOptions CreateOptions(SameSiteMode sameSite)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = sameSite,
            Path = "/",
            IsEssential = true
        };
    }
}
