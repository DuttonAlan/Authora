// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Mfa.Remove;

/// <summary>
///     The Service for a signed-in User to remove their authenticator app and recovery codes, after entering a current
///     code or a recovery code. Afterwards the password alone signs them in again.
/// </summary>
/// <remarks>
///     A wrong code counts as a failed sign-in attempt, so someone who took over a Session cannot guess codes without
///     locking the User out. The removal rotates the Security Stamp: every other Session and every Grant of the User
///     ends, only the Session it was removed in stays valid.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to keep the current Session valid.</param>
/// <param name="mfaRepository">An Instance of a IMfaRepository to remove the authenticator and the codes.</param>
/// <param name="verifier">Checks the code.</param>
/// <param name="timeProvider">The TimeProvider for the lockout and the Session checks.</param>
/// <param name="options">The sign-in settings with the lockout and the idle timeout.</param>
/// <param name="auditLog">The audit log to record the removal and failed attempts.</param>
/// <param name="logger">The Logger to record the removal.</param>
public sealed class RemoveTotpService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    IMfaRepository mfaRepository,
    MfaVerifier verifier,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    IAuditLog auditLog,
    ILogger<RemoveTotpService> logger)
{
    /// <summary>
    ///     Checks the code and removes the authenticator app and the recovery codes.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session the User removes it in. It stays valid.</param>
    /// <param name="code">A code of the authenticator app or a recovery code.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether it was removed, or why not.</returns>
    /// <exception cref="ConflictException">Thrown when the User was changed by another request at the same time.</exception>
    public async Task<RemoveTotpResult> ExecuteAsync(
        Guid userId,
        Guid sessionId,
        string? code,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await userRepository.GetUserById(userId, cancellationToken);
        var session = await sessionRepository.GetSessionById(sessionId, cancellationToken);

        // Checked again, although the page only runs with a valid Session: it may have ended since.
        if (user is null
            || !user.CanSignIn
            || session is null
            || session.UserId != user.Id
            || session.SecurityStamp != user.SecurityStamp
            || !session.IsActive(now, options.Value.SessionIdleTimeout))
        {
            return RemoveTotpResult.SessionEnded;
        }

        if (!await mfaRepository.HasConfirmedTotpAuthenticator(user.Id, cancellationToken))
        {
            return RemoveTotpResult.NotEnrolled;
        }

        if (user.IsLockedOut(now))
        {
            logger.TotpRemovalFailed(user.Id, "the User is locked out");
            await RecordFailureAsync(user.Id, session.Id, "The User is locked out", cancellationToken);

            return RemoveTotpResult.LockedOut;
        }

        if (!await verifier.VerifyAsync(user.Id, code, cancellationToken))
        {
            return await RecordWrongCodeAsync(user.Id, session.Id, cancellationToken);
        }

        // Loaded after verifying, which may have used up a recovery code in the database.
        var authenticator = await mfaRepository.GetTotpAuthenticator(user.Id, cancellationToken);

        if (authenticator is not null)
        {
            mfaRepository.RemoveTotpAuthenticator(authenticator);
        }

        mfaRepository.RemoveRecoveryCodes(await mfaRepository.GetRecoveryCodes(user.Id, cancellationToken));

        user.RotateSecurityStamp();
        session.RenewSecurityStamp(user);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Mfa.Removed, user.Id, $"Session {session.Id}"));

        // User, Session, authenticator and codes share the request's DbContext, so all are saved together with the
        // audit event. Throws a ConflictException if the User was changed since it was loaded.
        await userRepository.SaveChangesAsync(cancellationToken);

        logger.TotpRemoved(user.Id, session.Id);

        return RemoveTotpResult.Removed;
    }

    private async Task<RemoveTotpResult> RecordWrongCodeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        // Read again: the verification happened in between.
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Counted like a failed sign-in, atomically in the database.
        var lockoutEndAt = await userRepository.RecordFailedSignIn(
            userId,
            now,
            options.Value.MaxFailedAttempts,
            options.Value.LockoutDuration,
            cancellationToken);

        logger.TotpRemovalFailed(userId, "wrong code");
        await RecordFailureAsync(userId, sessionId, "Wrong code", cancellationToken);

        if (lockoutEndAt > now)
        {
            logger.LockedOut(userId, lockoutEndAt.Value);

            await auditLog.RecordNowAsync(
                AuditEntry.ForUser(AuditEventTypes.Account.LockedOut, userId, $"Until {lockoutEndAt.Value:O}"),
                cancellationToken);

            return RemoveTotpResult.LockedOut;
        }

        return RemoveTotpResult.WrongCode;
    }

    private Task RecordFailureAsync(Guid userId, Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var entry = AuditEntry.ForUser(AuditEventTypes.Mfa.Removed, userId, $"{reason}, Session {sessionId}");

        return auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);
    }
}
