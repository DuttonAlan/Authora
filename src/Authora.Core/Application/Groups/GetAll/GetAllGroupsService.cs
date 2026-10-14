// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;

namespace Authora.Core.Application.Groups.GetAll;

/// <summary>
///     The Service to get all Groups.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to get all Groups.</param>
public sealed class GetAllGroupsService(IGroupRepository groupRepository)
{
    /// <summary>
    ///     Gets all Groups ordered by their Name.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Groups.</returns>
    public async Task<IReadOnlyList<GetAllGroupsResponse>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var groups = await groupRepository.GetGroups(cancellationToken);

        return groups
            .Select(group => new GetAllGroupsResponse(
                group.Id,
                group.Name,
                group.DisplayName,
                group.Description))
            .ToList();
    }
}
