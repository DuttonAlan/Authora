// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.RemoveRole;

/// <summary>
///     The Service to remove a Role from a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="logger">The Logger to record the removal.</param>
public sealed class RemoveRoleService(
    IUserRepository userRepository,
    ILogger<RemoveRoleService> logger)
{
    /// <summary>
    ///     Removes a Role from a User. Does nothing if the Role is not assigned.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Role is removed.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the administrator Role would be removed from the last administrator.</exception>
    public async Task ExecuteAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        var role = user.Roles.FirstOrDefault(role => role.Id == roleId);

        if (role is null)
        {
            return;
        }

        if (role.IsAdministrator)
        {
            await AdministratorGuard.EnsureNotLastAdministratorAsync(userRepository, user, cancellationToken);
        }

        user.RemoveRole(role.Id);

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.RoleRemoved(user.Id, role.Id, role.Name);
    }
}
