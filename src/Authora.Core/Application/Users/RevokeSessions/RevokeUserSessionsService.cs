// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.RevokeSessions;

/// <summary>
///     The Service to sign a User out everywhere.
/// </summary>
/// <param name="userRepository">An Instance of a IUserRepository to revoke the Sessions of a User.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the revocation.</param>
public sealed class RevokeUserSessionsService(
    IUserRepository userRepository,
    PrivilegeGuard privilegeGuard,
    IAuditLog auditLog,
    ILogger<RevokeUserSessionsService> logger)
{
    /// <summary>
    ///     Rotates the Security Stamp of a User, which invalidates all of its Sessions and Refresh Tokens.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the Sessions are revoked.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted. Its Sessions were already revoked on deletion.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.SessionsRevoked, user.Id);

        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

        user.RotateSecurityStamp();
        auditLog.Record(entry);

        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.UserSessionsRevoked(user.Id);
    }
}
