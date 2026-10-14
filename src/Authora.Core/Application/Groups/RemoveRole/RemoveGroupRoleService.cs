// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.RemoveRole;

/// <summary>
///     The Service to remove a Role from a Group.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to change the Group.</param>
/// <param name="logger">The Logger to record the removal.</param>
public sealed class RemoveGroupRoleService(
    IGroupRepository groupRepository,
    ILogger<RemoveGroupRoleService> logger)
{
    /// <summary>
    ///     Removes a Role from a Group. The members lose the Role unless they have it directly or through another
    ///     Group. Does nothing if the Role is not assigned.
    /// </summary>
    /// <param name="groupId">Id of the Group.</param>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Role is removed.</returns>
    /// <exception cref="NotFoundException">Thrown when the Group does not exist.</exception>
    public async Task ExecuteAsync(
        Guid groupId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetGroupById(groupId, cancellationToken)
            ?? throw new NotFoundException($"Group '{groupId}' was not found.");

        var role = group.Roles.FirstOrDefault(role => role.Id == roleId);

        if (role is null)
        {
            return;
        }

        group.RemoveRole(role.Id);

        await groupRepository.SaveChangesAsync(cancellationToken);

        logger.GroupRoleRemoved(group.Id, group.Name, role.Id, role.Name);
    }
}
