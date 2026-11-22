// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Api.Authentication;
using Authora.Core.Application.Authorization;

using Microsoft.AspNetCore.Authorization;

namespace Authora.Api.Authorization;

/// <summary>
///     Authorization policies of the admin API and the account pages.
/// </summary>
/// <remarks>
///     Every admin controller requires <see cref="AdminApi"/>, and every action additionally the policy of its
///     Permission, named after the Permission (e.g. <c>[Authorize(PermissionNames.Users.Create)]</c>).
///     An action without a Permission therefore still needs an Access Token with the admin Scope.
/// </remarks>
internal static class AuthorizationPolicies
{
    /// <summary>
    ///     Requires an Access Token with the <see cref="AdminApiScope"/>, and, if MFA is required for administrators, a
    ///     multi-factor sign-in (<see cref="AdministratorMfaRequirement"/>). The Session cookie is not accepted.
    /// </summary>
    public const string AdminApi = "admin-api";

    /// <summary>
    ///     For the account pages: requires a Session in which the User authenticated recently, see
    ///     <see cref="RecentAuthenticationRequirement"/>. Other Users are asked to sign in again. Use it for sensitive
    ///     changes, e.g. enrolling or removing a second factor or regenerating recovery codes.
    /// </summary>
    public const string RecentAuthentication = "recent-authentication";

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
            .RequireClaim(AccessTokenClaimTypes.Scope, AdminApiScope.Name)
            .AddRequirements(new AdministratorMfaRequirement()));

        options.AddPolicy(RecentAuthentication, policy => policy
            .AddAuthenticationSchemes(SessionAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .AddRequirements(new RecentAuthenticationRequirement()));

        foreach (var permissionName in PermissionNames.All)
        {
            options.AddPolicy(permissionName, policy => policy
                .AddAuthenticationSchemes(AccessTokenAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissionName)));
        }
    }
}
