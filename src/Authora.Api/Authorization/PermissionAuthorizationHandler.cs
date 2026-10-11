// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.Authorization;

using Microsoft.AspNetCore.Authorization;

namespace Authora.Api.Authorization;

/// <summary>
///     Checks a <see cref="PermissionRequirement"/> against the Permissions of the User in the database.
/// </summary>
/// <param name="permissionEvaluator">The evaluator of the Permissions of the User.</param>
public sealed class PermissionAuthorizationHandler(PermissionEvaluator permissionEvaluator)
    : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (!Guid.TryParse(context.User.FindFirst(AccessTokenClaimTypes.Subject)?.Value, out var userId))
        {
            return;
        }

        var cancellationToken = context.Resource is HttpContext httpContext ? httpContext.RequestAborted : default;

        if (await permissionEvaluator.HasPermissionAsync(userId, requirement.PermissionName, cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
