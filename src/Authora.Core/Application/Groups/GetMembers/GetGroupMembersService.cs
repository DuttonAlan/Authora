// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Paging;

namespace Authora.Core.Application.Groups.GetMembers;

/// <summary>
///     The Service to get the members of a Group.
/// </summary>
/// <param name="groupRepository">An Instance of a IGroupRepository to get the members.</param>
public sealed class GetGroupMembersService(IGroupRepository groupRepository)
{
    /// <summary>
    ///     Gets one page of the members of a Group, ordered by their Email. Deleted Users are not returned.
    /// </summary>
    /// <param name="id">Id of the Group.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The members on the page and the total number of members.</returns>
    /// <exception cref="NotFoundException">Thrown when the Group does not exist.</exception>
    public async Task<PagedResponse<GetGroupMembersResponse>> ExecuteAsync(
        Guid id,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        _ = await groupRepository.GetGroupById(id, cancellationToken)
            ?? throw new NotFoundException($"Group '{id}' was not found.");

        var members = await groupRepository.GetMembers(id, page, cancellationToken);

        var items = members.Items
            .Select(user => new GetGroupMembersResponse(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.State))
            .ToList();

        return new PagedResponse<GetGroupMembersResponse>(items, members.Total);
    }
}
