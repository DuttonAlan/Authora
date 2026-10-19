// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.Authorization;

using Microsoft.AspNetCore.Authorization;

namespace Authora.Api.Authorization;

/// <summary>
///     Authorization policies of the admin API.
/// </summary>
/// <remarks>
///     Every admin controller requires <see cref="AdminApi"/>, and every action additionally the policy of its
///     Permission, named after the Permission (e.g. <c>[Authorize(PermissionNames.Users.Create)]</c>).
///     An action without a Permission therefore still needs an Access Token with the admin Scope.
/// </remarks>
internal static class AuthorizationPolicies
{
    /// <summary>
    ///     Requires an Access Token with the <see cref="AdminApiScope"/>. The Session cookie is not accepted.
    /// </summary>
    public const string AdminApi = "admin-api";

    /// <summary>
    ///     Registers the policies.
    /// </summary>
    /// <param name="options">The authorization options.</param>
    public static void AddAuthoraPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(AdminApi, policy => policy
            .AddAuthenticationSchemes(AccessTokenAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(AccessTokenClaimTypes.Scope, AdminApiScope.Name));

        foreach (var permissionName in PermissionNames.All)
        {
            options.AddPolicy(permissionName, policy => policy
                .AddAuthenticationSchemes(AccessTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissionName)));
        }
    }
}
