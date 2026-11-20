// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.Activate;

/// <summary>
///     The Service to activate a User.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to activate a User.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the activation.</param>
public sealed class ActivateUserService(
    IUserRepository userRepository,
    PrivilegeGuard privilegeGuard,
    IAuditLog auditLog,
    ILogger<ActivateUserService> logger)
{
    /// <summary>
    ///     Activates a User, so the User can sign in again. Does nothing if the User is already active.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is activated.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        if (user.State == UserState.Deleted)
        {
            throw new ConflictException("A deleted User cannot be activated.");
        }

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.Activated, user.Id);

        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

        if (user.State == UserState.Active)
        {
            return;
        }

        user.Activate();
        auditLog.Record(entry);

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.UserActivated(user.Id);
    }
}
