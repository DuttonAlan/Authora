// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Authora.Api.RateLimiting;

/// <summary>
///     Rate limiting policies of Authora, with the limits of <see cref="RateLimitOptions"/>.
/// </summary>
internal static class RateLimitPolicies
{
    /// <summary>
    ///     Limits sign-in attempts per client IP address, against password guessing across many accounts (US-007).
    ///     The lockout of a single User is handled by the sign-in itself.
    /// </summary>
    public const string SignIn = "sign-in";

    /// <summary>
    ///     Limits requests to the token and revocation endpoints per client IP address (US-036).
    /// </summary>
    public const string Token = "token";

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
            ? PerIpAddress(context, SignIn, GetOptions(context).SignInPermitsPerMinute)
            : RateLimitPartition.GetNoLimiter(string.Empty));

        options.AddPolicy(Token, context => PerIpAddress(context, Token, GetOptions(context).TokenPermitsPerMinute));
    }

    private static RateLimitOptions GetOptions(HttpContext context)
    {
        return context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
    }

    private static RateLimitPartition<string> PerIpAddress(HttpContext context, string policy, int permitsPerMinute)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{policy}:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    }
}
