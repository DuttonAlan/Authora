// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Entities;

namespace Authora.Core.Abstractions;

/// <summary>
///     Defines a contract for Role operations.
/// </summary>
/// <remarks>
///     Roles are changed by loading them tracked via <see cref="GetRoleById"/>, changing them
///     through their methods and calling <see cref="SaveChangesAsync"/>.
/// </remarks>
public interface IRoleRepository
{
    /// <summary>
    ///     Create a new Role.
    /// </summary>
    /// <param name="role">The Role to create.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The Id of the created Role.</returns>
    public Task<Guid> CreateRole(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get a Role by Id, including its Permissions.
    /// </summary>
    /// <param name="id">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The tracked Role, or null if the Role does not exist.</returns>
    public Task<Role?> GetRoleById(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all Roles ordered by their Name, without their Permissions.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>All Roles.</returns>
    public Task<IReadOnlyList<Role>> GetRoles(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a Role with the given Name exists.
    /// </summary>
    /// <param name="name">The Name of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>True if the Name is already used.</returns>
    public Task<bool> NameExists(string name, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Counts the Users that have the given Role, including deleted Users.
    /// </summary>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The number of Users with the Role.</returns>
    public Task<int> CountUsersWithRole(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a Role. It is also removed from all Users and Groups, and its Permissions are no longer granted.
    /// </summary>
    /// <param name="role">The tracked Role to delete.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Role is deleted.</returns>
    public Task DeleteRole(Role role, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves all changes made to tracked Roles.
    /// </summary>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the changes are saved.</returns>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
