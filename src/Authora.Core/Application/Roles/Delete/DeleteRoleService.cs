// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Roles.Delete;

/// <summary>
///     The Service to delete a Role.
/// </summary>
/// <param name="roleRepository">An Instance of a IRoleRepository to delete a Role.</param>
/// <param name="logger">The Logger to record the deletion.</param>
public sealed class DeleteRoleService(
    IRoleRepository roleRepository,
    ILogger<DeleteRoleService> logger)
{
    /// <summary>
    ///     Deletes a Role based on the Id. The Role is also removed from all Users and Groups that have it.
    ///     Does nothing if the Role does not exist.
    /// </summary>
    /// <param name="id">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Role is deleted.</returns>
    /// <exception cref="ConflictException">Thrown when the Role is a System Role.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetRoleById(id, cancellationToken);

        if (role is null)
        {
            return;
        }

        if (role.IsSystem)
        {
            throw new ConflictException("A System Role cannot be deleted.");
        }

        var userCount = await roleRepository.CountUsersWithRole(role.Id, cancellationToken);

        await roleRepository.DeleteRole(role, cancellationToken);

        logger.RoleDeleted(role.Id, role.Name, userCount);
    }
}
