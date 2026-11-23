// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Passkeys;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.RemovePasskey;

/// <summary>
///     The Service for an administrator to remove a passkey of a User, e.g. one on a lost security key.
/// </summary>
/// <remarks>
///     Rotates the Security Stamp, so all Sessions and Grants of the User end: whoever signed in with the lost passkey is
///     signed out, and the Clients of the Sessions are notified (back-channel logout). The caller must be allowed to act on the User (<see cref="PrivilegeGuard"/>), so only an
///     administrator can change an administrator.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to serialize changes of the User's second factors.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to remove the passkey.</param>
/// <param name="sessionRevoker">Revokes the Sessions of the User and their Grants and notifies their Clients.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="auditLog">The audit log to record the removal.</param>
/// <param name="logger">The Logger to record the removal.</param>
public sealed class RemoveUserPasskeyService(
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    IPasskeyRepository passkeyRepository,
    SessionRevoker sessionRevoker,
    PrivilegeGuard privilegeGuard,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<RemoveUserPasskeyService> logger)
{
    /// <summary>
    ///     Removes a passkey of a User and signs the User out everywhere.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="passkeyId">Id of the passkey.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the removal is saved.</returns>
    /// <exception cref="NotFoundException">Thrown when the User or the passkey does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when the User is deleted, or was changed by another request at the same time.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    public async Task ExecuteAsync(Guid id, Guid passkeyId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken);

        var user = await userRepository.GetUserById(id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' was not found.");

        DeletedUserGuard.EnsureNotDeleted(user);

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.PasskeyRemoved, user.Id, $"Passkey {passkeyId}");

        await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

        // Joins the transaction of the guard, so a registration cannot run at the same time.
        await using var mfaTransaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        var passkey = await passkeyRepository.GetPasskey(user.Id, passkeyId, cancellationToken)
            ?? throw new NotFoundException($"Passkey '{passkeyId}' was not found.");

        passkeyRepository.RemovePasskeys([passkey]);
        user.RotateSecurityStamp();

        await sessionRevoker.RevokeSessionsOfUserAsync(user.Id, exceptSessionId: null, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        auditLog.Record(entry);

        // User and passkey share the request's DbContext, so both are saved together with the audit event.
        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.PasskeyRemovedByAdministrator(user.Id, passkey.Id);
    }
}
