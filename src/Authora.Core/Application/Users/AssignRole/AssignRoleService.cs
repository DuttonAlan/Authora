// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.AssignRole;

/// <summary>
///     The Service to assign a Role to a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="roleRepository">An Instance of a IRoleRepository to get the Role.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the assignment.</param>
public sealed class AssignRoleService(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IAuditLog auditLog,
    ILogger<AssignRoleService> logger)
{
    /// <summary>
    ///     Assigns a Role to a User. Does nothing if the Role is already assigned.
    /// </summary>
    /// <param name="userId">Id of the User.</param>
    /// <param name="roleId">Id of the Role.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Role is assigned.</returns>
    /// <exception cref="NotFoundException">Thrown when the User or the Role does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    public async Task ExecuteAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        if (user.Roles.Any(role => role.Id == roleId))
        {
            return;
        }

        var role = await roleRepository.GetRoleById(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        user.AssignRole(role);
        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Users.RoleAssigned, user.Id, $"Role '{role.Name}' ({role.Id})"));

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.RoleAssigned(user.Id, role.Id, role.Name);
    }
}
