// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Application.Paging;
using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Group operations.
/// </summary>
/// <remarks>
///     Groups are changed by loading them tracked via <see cref="GetGroupById"/>, changing them
///     through their methods and calling <see cref="SaveChangesAsync"/>. Memberships belong to the User,
///     see <see cref="User.JoinGroup(Group)"/>.
/// </remarks>
public interface IGroupRepository
{
    /// <summary>
    ///     Create a new Group.
    /// </summary>
    /// <param name="group">The Group to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created Group.</returns>
    public Task<Guid> CreateGroup(Group group, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a Group by Id, including its Roles.
    /// </summary>
    /// <param name="id">Id of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Group, or null if the Group does not exist.</returns>
    public Task<Group?> GetGroupById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all Groups ordered by their Name, without their Roles.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Groups.</returns>
    public Task<IReadOnlyList<Group>> GetGroups(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get one page of the members of a Group ordered by their Email. Deleted Users are not returned.
    /// </summary>
    /// <param name="groupId">Id of the Group.</param>
    /// <param name="page">The requested page.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The members on the page and the total number of members.</returns>
    public Task<PagedResponse<User>> GetMembers(
        Guid groupId,
        PageRequest page,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a Group with the given Name exists.
    /// </summary>
    /// <param name="name">The Name of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the Name is already used.</returns>
    public Task<bool> NameExists(string name, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the members of a Group, including deleted Users.
    /// </summary>
    /// <param name="groupId">Id of the Group.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of members.</returns>
    public Task<int> CountMembers(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a Group. All memberships end, so the members lose the Roles of the Group.
    /// </summary>
    /// <param name="group">The tracked Group to delete.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Group is deleted.</returns>
    public Task DeleteGroup(Group group, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes made to tracked Groups.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
