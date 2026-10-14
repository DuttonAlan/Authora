// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Groups.Get;

/// <summary>
///     The Service to get a Group.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to get a Group.</param>
public sealed class GetGroupService(IGroupRepository groupRepository)
{
    /// <summary>
    ///     Gets a Group based on the Id, including its Roles.
    /// </summary>
    /// <param name="id">Id of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Group, or null if the Group does not exist.</returns>
    public async Task<GetGroupResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetGroupById(id, cancellationToken);

        if (group is null)
        {
            return null;
        }

        var roles = group.Roles
            .OrderBy(role => role.Name, StringComparer.Ordinal)
            .Select(role => new GroupRoleResponse(role.Id, role.Name, role.DisplayName))
            .ToList();

        return new GetGroupResponse(
            group.Id,
            group.Name,
            group.DisplayName,
            group.Description,
            group.CreatedAt,
            group.ModifiedAt,
            roles);
    }
}
