// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Roles.RevokePermission;

/// <summary>
///     The Service to revoke a Permission from a Role.
/// </summary>
/// <param name="roleRepository">An Instance of a IRoleRepository to change the Role.</param>
/// <param name="logger">The Logger to record the revocation.</param>
public sealed class RevokePermissionService(
    IRoleRepository roleRepository,
    ILogger<RevokePermissionService> logger)
{
    /// <summary>
    ///     Revokes a Permission from a Role. Does nothing if the Permission is not granted.
    /// </summary>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="permissionId">Id of the Permission.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Permission is revoked.</returns>
    /// <exception cref="NotFoundException">Thrown when the Role does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the Role is the administrator Role.</exception>
    public async Task ExecuteAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetRoleById(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        var permission = role.Permissions.FirstOrDefault(permission => permission.Id == permissionId);

        if (permission is null)
        {
            return;
        }

        role.RevokePermission(permission.Id);

        await roleRepository.SaveChangesAsync(cancellationToken);

        logger.PermissionRevoked(role.Id, role.Name, permission.Id, permission.Name);
    }
}
