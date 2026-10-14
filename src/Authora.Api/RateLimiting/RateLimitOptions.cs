// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Api.RateLimiting;

/// <summary>
///     Settings of the rate limits per client IP address (US-036), bound from the section <see cref="SectionName"/>.
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
    ///     Requests to <c>/oauth/token</c> and <c>/oauth/revoke</c> per minute and IP address, against guessing
    ///     Client Secrets and flooding. Defaults to 300, so a server-side Client that refreshes tokens for many Users
    ///     is not throttled. <c>/oauth/introspect</c> is not limited, because resource servers call it for every
    ///     request.
    /// </summary>
    public int TokenPermitsPerMinute { get; set; } = 300;
}
