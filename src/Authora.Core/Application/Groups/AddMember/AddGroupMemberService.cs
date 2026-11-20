// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.AddMember;

/// <summary>
///     The Service to add a User to a Group.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="groupRepository">An Instance of a IGroupRepository to get the Group.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the Group.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the new membership.</param>
public sealed class AddGroupMemberService(
    IUserRepository userRepository,
    IGroupRepository groupRepository,
    PrivilegeGuard privilegeGuard,
    IAuditLog auditLog,
    ILogger<AddGroupMemberService> logger)
{
    /// <summary>
    ///     Adds a User to a Group, so the User has all Roles of the Group. Does nothing if the User is already a member.
    /// </summary>
    /// <remarks>
    ///     A caller who is not an administrator can only add Users to a Group whose Roles' Permissions they all hold,
    ///     and only Users whose Permissions they all hold.
    /// </remarks>
    /// <param name="groupId">Id of the Group.</param>
    /// <param name="userId">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is a member.</returns>
    /// <exception cref="NotFoundException">Thrown when the Group or the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not grant the Roles of the Group or act on the User.</exception>
    public async Task ExecuteAsync(
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // The lock also serializes concurrent additions of the same member, so the second one is a no-op.
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var group = await groupRepository.GetGroupById(groupId, cancellationToken)
            ?? throw new NotFoundException($"Group '{groupId}' was not found.");

        var user = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        if (user.Groups.Any(x => x.Id == group.Id))
        {
            return;
        }

        var entry = AuditEntry.ForGroup(AuditEventTypes.Groups.MemberAdded, group.Id, $"User {user.Id}");

        await privilegeGuard.EnsureCanGrantRolesOfGroupAsync(group, entry, cancellationToken);
        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

        user.JoinGroup(group);
        auditLog.Record(entry);

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.MemberAdded(group.Id, group.Name, user.Id);
    }
}
