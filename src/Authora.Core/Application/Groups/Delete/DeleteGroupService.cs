// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups.Delete;

/// <summary>
///     The Service to delete a Group.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to delete a Group.</param>
/// <param name="logger">The Logger to record the deletion.</param>
public sealed class DeleteGroupService(
    IGroupRepository groupRepository,
    ILogger<DeleteGroupService> logger)
{
    /// <summary>
    ///     Deletes a Group based on the Id. All memberships end, so the members lose the Roles of the Group
    ///     unless they have them directly or through another Group. Does nothing if the Group does not exist.
    /// </summary>
    /// <param name="id">Id of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Group is deleted.</returns>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetGroupById(id, cancellationToken);

        if (group is null)
        {
            return;
        }

        var memberCount = await groupRepository.CountMembers(group.Id, cancellationToken);

        await groupRepository.DeleteGroup(group, cancellationToken);

        logger.GroupDeleted(group.Id, group.Name, memberCount);
    }
}
