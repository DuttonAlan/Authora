// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;

using Authora.Core.Application.ExternalSignIn;

using Microsoft.AspNetCore.DataProtection;

namespace Authora.Api.Authentication;

/// <summary>
///     The cookie that holds an account at an external identity provider that waits to be linked to the User with its
///     Email (<see cref="PendingExternalLink"/>) until the browser signs in to that User, encrypted with ASP.NET Core
///     Data Protection, so the browser can neither read nor change it.
/// </summary>
/// <remarks>
///     It lasts <see cref="ExternalSignInOptions.PendingLinkLifetime"/>, enforced by the encryption as well, and is
///     deleted once the link is completed or refused, so abandoning the sign-in links nothing. <c>SameSite=Lax</c>,
///     because it is set on the redirect back from the provider; <c>HttpOnly</c>, <c>Secure</c> and the
///     <c>__Host-</c> prefix like the Session cookie. A new pending link in the same browser replaces the previous one.
/// </remarks>
/// <param name="dataProtectionProvider">Creates the protector of the cookie.</param>
/// <param name="timeProvider">The clock for the lifetime.</param>
public sealed class ExternalLinkCookie(IDataProtectionProvider dataProtectionProvider, TimeProvider timeProvider)
{
    /// <summary>
    ///     Name of the cookie.
    /// </summary>
    public const string Name = "__Host-authora_external_link";

    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector("Authora.ExternalSignIn.PendingLink.v1")
        .ToTimeLimitedDataProtector();

    /// <summary>
    ///     Sets the cookie of a pending link.
    /// </summary>
    /// <param name="response">The current response.</param>
    /// <param name="pending">The pending link.</param>
    public void Append(HttpResponse response, PendingExternalLink pending)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(pending);

        var expiresAt = pending.CreatedAt.Add(ExternalSignInOptions.PendingLinkLifetime);
        var value = _protector.Protect(JsonSerializer.Serialize(pending), new DateTimeOffset(expiresAt, TimeSpan.Zero));

        var options = CreateOptions();
        options.MaxAge = expiresAt - timeProvider.GetUtcNow().UtcDateTime;

        response.Cookies.Append(Name, value, options);
    }

    /// <summary>
    ///     Reads the cookie without deleting it.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <returns>The pending link, or null if there is none, it expired or was not issued by Authora.</returns>
    public PendingExternalLink? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var value = request.Cookies[Name];

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PendingExternalLink>(_protector.Unprotect(value));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            // Tampered with, expired, or encrypted with a key that no longer exists: nothing is linked.
            return null;
        }
    }

    /// <summary>
    ///     Deletes the cookie.
    /// </summary>
    /// <param name="response">The current response.</param>
    public static void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

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
