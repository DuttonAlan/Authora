// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.RateLimiting;

/// <summary>
///     Settings of the rate limits per client IP address, bound from the section <see cref="SectionName"/>.
/// </summary>
/// <remarks>
///     Behind a reverse proxy, configure <c>Authora:ForwardedHeaders</c>, otherwise all requests share the IP address
///     of the proxy and one limit.
/// </remarks>
public sealed class RateLimitOptions
{
    /// <summary>
    ///     The configuration section of the settings.
    /// </summary>
    public const string SectionName = "Authora:RateLimiting";

    /// <summary>
    ///     Largest allowed value of each limit.
    /// </summary>
    public const int MaxPermitsPerMinute = 100_000;

    /// <summary>
    ///     Sign-in attempts per minute and IP address, against password guessing across many accounts.
    ///     Defaults to 10.
    /// </summary>
    public int SignInPermitsPerMinute { get; set; } = 10;

    /// <summary>
    ///     Requests to <c>/oauth/token</c>, <c>/oauth/revoke</c> and <c>/oauth/introspect</c> together per minute and
    ///     IP address, against guessing Client Secrets and flooding. Defaults to 300, so a server-side Client that
    ///     refreshes tokens for many Users is not throttled. A resource server that introspects a token for every
    ///     request should cache the result or get a higher limit.
    /// </summary>
    public int TokenPermitsPerMinute { get; set; } = 300;

    /// <summary>
    ///     Submissions of the "Forgot password?" form and the reset form together per minute and IP address, against
    ///     probing addresses, flooding mailboxes and guessing reset links. Defaults to 5. Each address also gets at
    ///     most <c>Authora:PasswordReset:MaxEmailsPerHour</c> links.
    /// </summary>
    public int PasswordResetPermitsPerMinute { get; set; } = 5;

    /// <summary>
    ///     Requests of signed-in Users for a new email verification link per minute and IP address, against flooding
    ///     mailboxes. Defaults to 5. Each User also gets at most <c>Authora:EmailVerification:MaxEmailsPerHour</c> links.
    /// </summary>
    public int EmailVerificationPermitsPerMinute { get; set; } = 5;
}
