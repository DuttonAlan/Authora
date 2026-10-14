// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

namespace Authora.Core.Application.Users.GetPermissions;

/// <summary>
///     The Service to get the effective Permissions of a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to get the User.</param>
public sealed class GetEffectivePermissionsService(IUserRepository userRepository)
{
    /// <summary>
    ///     Gets the union of the Permissions of all Roles of a User, assigned directly or through a Group, ordered by
    ///     their Name, together with the Roles each Permission comes from.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The effective Permissions of the User.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    public async Task<IReadOnlyList<GetEffectivePermissionsResponse>> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserWithPermissionsById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        // Resolve orders the Roles by Name, so the Roles of each Permission stay in that order.
        return EffectiveRoles.Resolve(user)
            .SelectMany(role => role.Role.Permissions, (role, permission) => (role.Response, Permission: permission))
            .GroupBy(grant => grant.Permission.Id)
            .Select(grants => new GetEffectivePermissionsResponse(
                grants.Key,
                grants.First().Permission.Name,
                grants.Select(grant => grant.Response).ToList()))
            .OrderBy(permission => permission.Name, StringComparer.Ordinal)
            .ToList();
    }
}
