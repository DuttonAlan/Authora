// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Authorization;

/// <summary>
///     Checks whether a User has a Permission through one of their Roles (US-032).
/// </summary>
/// <remarks>
///     The Permissions are loaded on every check, so a removed Role or Permission takes effect on the next request.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User with their Permissions.</param>
public sealed class PermissionEvaluator(IUserRepository userRepository)
{
    /// <summary>
    ///     Checks whether a User has a Permission.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="permissionName">The Name of the Permission, see <see cref="PermissionNames"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the User exists, can sign in and has the Permission.</returns>
    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permissionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(permissionName);

        var user = await userRepository.GetUserWithPermissionsById(userId, cancellationToken);

        if (user is null || !user.CanSignIn)
        {
            return false;
        }

        // The administrator Role always has all Permissions, also those added since the last seeding.
        return user.Roles.Any(role => role.IsAdministrator
            || role.Permissions.Any(permission => permission.Name == permissionName));
    }
}
