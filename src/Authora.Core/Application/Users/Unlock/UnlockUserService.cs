// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.Unlock;

/// <summary>
///     The Service to unlock a User that is locked out after too many failed login attempts.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to unlock a User.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the unlock.</param>
public sealed class UnlockUserService(
    IUserRepository userRepository,
    IAuditLog auditLog,
    ILogger<UnlockUserService> logger)
{
    /// <summary>
    ///     Resets the failed login attempts and ends the lockout of a User.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the User is unlocked.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        if (user.FailedLoginCount == 0 && user.LockoutEndAt is null)
        {
            return;
        }

        user.Unlock();
        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Users.Unlocked, user.Id));

        await userRepository.SaveChangesAsync(cancellationToken);

        logger.UserUnlocked(user.Id);
    }
}
