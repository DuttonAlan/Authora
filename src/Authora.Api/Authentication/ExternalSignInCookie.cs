// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;

using Authora.Core.Application.ExternalSignIn;

using Microsoft.AspNetCore.DataProtection;

namespace Authora.Api.Authentication;

/// <summary>
///     The cookie that holds a running sign-in or link through an external identity provider
///     (<see cref="ExternalSignInFlow"/>: <c>state</c>, <c>nonce</c>, PKCE code verifier, provider, return URL),
///     encrypted with ASP.NET Core Data Protection, so the browser can neither read nor change it.
/// </summary>
/// <remarks>
///     It lasts <see cref="ExternalSignInOptions.FlowLifetime"/>, enforced by the encryption as well, and is deleted when
///     the response of the provider arrives, so it can be used once. <c>SameSite=Lax</c>, so it is still sent with the
///     redirect back from the provider; <c>HttpOnly</c>, <c>Secure</c> and the <c>__Host-</c> prefix like the Session
///     cookie. A new flow in the same browser replaces a running one.
/// </remarks>
/// <param name="dataProtectionProvider">Creates the protector of the cookie.</param>
/// <param name="timeProvider">The clock for the lifetime.</param>
public sealed class ExternalSignInCookie(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
{
    /// <summary>
    ///     Name of the cookie.
    /// </summary>
    public const string Name = "__Host-authora_external_sign_in";

    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector("Authora.ExternalSignIn.Flow.v1")
        .ToTimeLimitedDataProtector();

    /// <summary>
    ///     Sets the cookie of a new flow.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="flow">The flow.</param>
    public void Append(HttpResponse response, ExternalSignInFlow flow)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(flow);

        var value = _protector.Protect(
            JsonSerializer.Serialize(flow),
            timeProvider.GetUtcNow().Add(ExternalSignInOptions.FlowLifetime));

        var options = CreateOptions();
        options.MaxAge = ExternalSignInOptions.FlowLifetime;

        response.Cookies.Append(Name, value, options);
    }

    /// <summary>
    ///     Reads and deletes the cookie, so a flow can only be completed once.
    /// </summary>
    /// <param name="context">The current request and response.</param>
    /// <returns>The flow, or null if there is none, it expired or was not issued by Authora.</returns>
    public ExternalSignInFlow? Take(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Request.Cookies[Name];

        context.Response.Cookies.Delete(Name, CreateOptions());

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ExternalSignInFlow>(_protector.Unprotect(value));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            // Tampered with, expired, or encrypted with a key that no longer exists: the User starts again.
            return null;
        }
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
