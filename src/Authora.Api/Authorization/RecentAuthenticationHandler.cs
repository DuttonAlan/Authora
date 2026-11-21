// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using System.Globalization;

using Authora.Api.Authentication;
using Authora.Core.Application.Account;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Authora.Api.Authorization;

/// <summary>
///     Checks a <see cref="RecentAuthenticationRequirement"/> against the time the User last authenticated in the
///     Session (<c>auth_time</c> of the Session principal).
/// </summary>
/// <remarks>
///     A User who fails it is sent to the sign-in page by <see cref="SessionAuthenticationHandler"/> and back
///     afterwards; signing in again starts a new Session with a new authentication time.
/// </remarks>
/// <param name="options">The sign-in settings with the window.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class RecentAuthenticationHandler(IOptions<SignInOptions> options, TimeProvider timeProvider)
    : AuthorizationHandler<RecentAuthenticationRequirement>
{
    /// <inheritdoc/>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RecentAuthenticationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var value = context.User.FindFirst(SessionClaimTypes.AuthenticationTime)?.Value;

        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            var authenticatedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);

            if (timeProvider.GetUtcNow() - authenticatedAt <= options.Value.RecentAuthenticationWindow)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
