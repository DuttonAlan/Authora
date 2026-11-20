// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Roles.GrantPermission;

/// <summary>
///     The Service to grant a Permission to a Role.
/// </summary>
/// <param name="roleRepository">An Instance of a IRoleRepository to change the Role.</param>
/// <param name="permissionRepository">An Instance of a IPermissionRepository to get the Permission.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds the Permission.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the grant.</param>
public sealed class GrantPermissionService(
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    PrivilegeGuard privilegeGuard,
    IAuditLog auditLog,
    ILogger<GrantPermissionService> logger)
{
    /// <summary>
    ///     Grants a Permission to a Role. Does nothing if the Permission is already granted.
    /// </summary>
    /// <remarks>
    ///     A caller who is not an administrator can only grant Permissions they hold themselves.
    /// </remarks>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="permissionId">Id of the Permission.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Permission is granted.</returns>
    /// <exception cref="NotFoundException">Thrown when the Role or the Permission does not exist.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller does not hold the Permission.</exception>
    public async Task ExecuteAsync(
        Guid roleId,
        Guid permissionId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var role = await roleRepository.GetRoleById(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        if (role.Permissions.Any(permission => permission.Id == permissionId))
        {
            return;
        }

        var permission = await permissionRepository.GetPermissionById(permissionId, cancellationToken)
            ?? throw new NotFoundException($"Permission '{permissionId}' was not found.");

        var entry = AuditEntry.ForRole(AuditEventTypes.Roles.PermissionGranted, role.Id, $"Permission '{permission.Name}'");

        await privilegeGuard.EnsureCanGrantPermissionAsync(permission, entry, cancellationToken);

        role.GrantPermission(permission);
        auditLog.Record(entry);

        await roleRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.PermissionGranted(role.Id, role.Name, permission.Id, permission.Name);
    }
}
