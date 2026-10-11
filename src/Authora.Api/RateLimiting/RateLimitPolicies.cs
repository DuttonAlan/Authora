// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace Authora.Api.RateLimiting;

/// <summary>
///     Rate limiting policies of Authora.
/// </summary>
internal static class RateLimitPolicies
{
    /// <summary>
    ///     Limits sign-in attempts per client IP address, against password guessing across many accounts (US-007).
    ///     The lockout of a single User is handled by the sign-in itself.
    /// </summary>
    public const string SignIn = "sign-in";

    private const int SignInPermitsPerMinute = 10;

    /// <summary>
    ///     Registers the policies.
    /// </summary>
    /// <param name="options">The rate limiter options.</param>
    /// <remarks>
    ///     Behind a reverse proxy, the forwarded headers middleware must be configured, otherwise all requests share
    ///     the IP address of the proxy.
    /// </remarks>
    public static void AddAuthoraPolicies(this RateLimiterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Only POST submits credentials, so showing the page is not limited.
        options.AddPolicy(SignIn, context => HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = SignInPermitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                })
            : RateLimitPartition.GetNoLimiter(string.Empty));
    }
}
