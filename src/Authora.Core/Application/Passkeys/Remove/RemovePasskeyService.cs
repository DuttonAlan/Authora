// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Passkeys.Remove;

/// <summary>
///     The Service for a signed-in User to remove one of their passkeys.
/// </summary>
/// <remarks>
///     The removal rotates the Security Stamp: every other Session and every Grant of the User ends, so whoever signed
///     in with the passkey elsewhere is signed out. Only the Session it was removed in stays valid.
/// </remarks>
/// <param name="guard">Checks the Session.</param>
/// <param name="userRepository">An Instance of a IUserRepository to save the User.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to serialize changes of the User's second factors.</param>
/// <param name="passkeyRepository">An Instance of a IPasskeyRepository to remove the passkey.</param>
/// <param name="sessionRevoker">Revokes the other Sessions of the User and their Grants and notifies their Clients.</param>
/// <param name="timeProvider">The TimeProvider for the revocation time.</param>
/// <param name="auditLog">The audit log to record the removal.</param>
/// <param name="logger">The Logger to record the removal.</param>
public sealed class RemovePasskeyService(
    PasskeyChangeGuard guard,
    IUserRepository userRepository,
    IMfaRepository mfaRepository,
    IPasskeyRepository passkeyRepository,
    SessionRevoker sessionRevoker,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<RemovePasskeyService> logger)
{
    /// <summary>
    ///     Removes a passkey of the User.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session the User removes it in. It stays valid.</param>
    /// <param name="passkeyId">Id of the passkey.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether it was removed, or why not.</returns>
    /// <exception cref="Exceptions.ConflictException">Thrown when the User was changed by another request at the same time.</exception>
    public async Task<RemovePasskeyResult> ExecuteAsync(
        Guid userId,
        Guid sessionId,
        Guid passkeyId,
        CancellationToken cancellationToken = default)
    {
        var (user, session, needsStepUp) = await guard.CheckAsync(userId, sessionId, cancellationToken);

        if (user is null || session is null)
        {
            return RemovePasskeyResult.SessionEnded;
        }

        if (needsStepUp)
        {
            return RemovePasskeyResult.StepUpRequired;
        }

        await using var transaction = await mfaRepository.BeginMfaChangeAsync(user.Id, cancellationToken);

        var passkey = await passkeyRepository.GetPasskey(user.Id, passkeyId, cancellationToken);

        if (passkey is null)
        {
            return RemovePasskeyResult.NotFound;
        }

        passkeyRepository.RemovePasskeys([passkey]);

        user.RotateSecurityStamp();
        session.RenewSecurityStamp(user);

        // In the transaction of the lock above.
        await sessionRevoker.RevokeSessionsOfUserAsync(user.Id, session.Id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Passkeys.Removed, user.Id, $"Passkey {passkey.Id}, Session {session.Id}"));

        // User, Session and passkey share the request's DbContext, so all are saved together with the audit event.
        // Throws a ConflictException if the User was changed since it was loaded.
        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.PasskeyRemoved(user.Id, passkey.Id, session.Id);

        return RemovePasskeyResult.Removed;
    }
}

/// <summary>
///     The outcome of <see cref="RemovePasskeyService"/>.
/// </summary>
public enum RemovePasskeyResult
{
    /// <summary>
    ///     The passkey is removed; all other Sessions of the User ended.
    /// </summary>
    Removed,

    /// <summary>
    ///     The User has no passkey with this Id, e.g. because it was removed in another tab.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The User has a second factor but did not complete one in the Session, see <see cref="PasskeyChangeGuard"/>.
    /// </summary>
    StepUpRequired,

    /// <summary>
    ///     The Session ended in the meantime; the User must sign in again.
    /// </summary>
    SessionEnded
}
