// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.AssignRole;

/// <summary>
///     The Service to assign a Role to a Group.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to change the Group.</param>
/// <param name="roleRepository">An Instance of a IRoleRepository to get the Role.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the Role.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the assignment.</param>
public sealed class AssignGroupRoleService(
    IGroupRepository groupRepository,
    IRoleRepository roleRepository,
    PrivilegeGuard privilegeGuard,
    IAuditLog auditLog,
    ILogger<AssignGroupRoleService> logger)
{
    /// <summary>
    ///     Assigns a Role to a Group, so every member has it. Does nothing if the Role is already assigned.
    /// </summary>
    /// <remarks>
    ///     A caller who is not an administrator can only assign Roles whose Permissions they all hold.
    /// </remarks>
    /// <param name="groupId">Id of the Group.</param>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Role is assigned.</returns>
    /// <exception cref="NotFoundException">Thrown when the Group or the Role does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the Role is the administrator Role.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller does not hold all Permissions of the Role.</exception>
    public async Task ExecuteAsync(
        Guid groupId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        // The lock also serializes concurrent assignments of the same Role, so the second one is a no-op.
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var group = await groupRepository.GetGroupById(groupId, cancellationToken)
            ?? throw new NotFoundException($"Group '{groupId}' was not found.");

        if (group.Roles.Any(role => role.Id == roleId))
        {
            return;
        }

        var role = await roleRepository.GetRoleById(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        // The last-administrator check only counts Users who have the administrator Role directly.
        if (role.IsAdministrator)
        {
            throw new ConflictException("The administrator Role can only be assigned to Users directly, not to a Group.");
        }

        var entry = AuditEntry.ForGroup(AuditEventTypes.Groups.RoleAssigned, group.Id, $"Role '{role.Name}' ({role.Id})");

        await privilegeGuard.EnsureCanAssignRoleAsync(role, entry, cancellationToken);

        group.AssignRole(role);
        auditLog.Record(entry);

        await groupRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.GroupRoleAssigned(group.Id, group.Name, role.Id, role.Name);
    }
}
