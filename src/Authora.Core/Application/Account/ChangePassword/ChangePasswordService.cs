// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Domain.Enums;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.ChangePassword;

/// <summary>
///     The Service for a signed-in User to change their own password after entering the current one.
/// </summary>
/// <remarks>
///     A wrong current password counts as a failed sign-in attempt, so someone who took over a Session cannot guess the
///     password without locking the User out. The new password rotates the Security Stamp: every other Session and
///     every Grant (with its Access and Refresh Tokens) of the User ends, only the Session the password was changed in
///     stays valid.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to keep the current Session valid.</param>
/// <param name="sessionRevoker">Revokes the other Sessions of the User and their Grants and notifies their Clients.</param>
/// <param name="passwordHasher">The hasher to verify the current and hash the new password.</param>
/// <param name="emailSender">Sends the notification about the change, if email delivery is configured.</param>
/// <param name="timeProvider">The TimeProvider for the lockout and the Session checks.</param>
/// <param name="options">The sign-in settings with the lockout and the idle timeout.</param>
/// <param name="auditLog">The audit log to record the change and failed attempts.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class ChangePasswordService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    SessionRevoker sessionRevoker,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    IAuditLog auditLog,
    ILogger<ChangePasswordService> logger)
{
    /// <summary>
    ///     Checks the current password and sets the new one.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session the User changes the password in. It stays valid.</param>
    /// <param name="request">The current and the new password.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the password was changed, or why not.</returns>
    /// <exception cref="DomainValidationException">Thrown when the new password does not follow the <see cref="PasswordPolicy"/>.</exception>
    /// <exception cref="ConflictException">Thrown when the User was changed by another request at the same time.</exception>
    public async Task<ChangePasswordResult> ExecuteAsync(
        Guid userId,
        Guid sessionId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CurrentPassword);

        PasswordPolicy.ThrowIfInvalid(request.NewPassword, nameof(request));

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
            return ChangePasswordResult.SessionEnded;
        }

        if (user.IsLockedOut(now))
        {
            logger.PasswordChangeFailed(user.Id, "the User is locked out");
            await RecordFailureAsync(user.Id, session.Id, "The User is locked out", cancellationToken);

            return ChangePasswordResult.LockedOut;
        }

        // A User without a password sets one instead, see AddPasswordService.
        if (user.PasswordHash is not { } passwordHash)
        {
            return ChangePasswordResult.NoPassword;
        }

        if (passwordHasher.Verify(passwordHash, request.CurrentPassword) == PasswordVerification.Failed)
        {
            return await RecordWrongPasswordAsync(user.Id, session.Id, now, cancellationToken);
        }

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        session.RenewSecurityStamp(user);

        // Started after the password was checked, so the failed attempts counted for a wrong one are not rolled back.
        await using var transaction = await sessionRevoker.BeginAsync(cancellationToken);
        await sessionRevoker.RevokeSessionsOfUserAsync(user.Id, session.Id, now, cancellationToken);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Account.PasswordChanged, user.Id, $"Session {session.Id}"));

        // User and Session share the request's DbContext, so both are saved together with the audit event. Throws a
        // ConflictException if the User was changed since it was loaded, e.g. by a concurrent password change; the
        // revocation of the other Sessions is rolled back then.
        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.PasswordChanged(user.Id, session.Id);

        if (emailSender.IsEnabled)
        {
            try
            {
                await emailSender.SendAsync(EmailTemplates.PasswordChanged(user, now), cancellationToken);
            }
            catch (EmailDeliveryException exception)
            {
                // The password is changed either way; a lost notification must not report the change as failed.
                logger.PasswordChangeNotificationFailed(user.Id, exception.Message);
            }
        }

        return ChangePasswordResult.Changed;
    }

    private async Task<ChangePasswordResult> RecordWrongPasswordAsync(
        Guid userId,
        Guid sessionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Counted like a failed sign-in, atomically in the database.
        var lockoutEndAt = await userRepository.RecordFailedSignIn(
            userId,
            now,
            options.Value.MaxFailedAttempts,
            options.Value.LockoutDuration,
            cancellationToken);

        logger.PasswordChangeFailed(userId, "wrong current password");
        await RecordFailureAsync(userId, sessionId, "Wrong current password", cancellationToken);

        if (lockoutEndAt > now)
        {
            logger.LockedOut(userId, lockoutEndAt.Value);

            await auditLog.RecordNowAsync(
                AuditEntry.ForUser(AuditEventTypes.Account.LockedOut, userId, $"Until {lockoutEndAt.Value:O}"),
                cancellationToken);

            return ChangePasswordResult.LockedOut;
        }

        return ChangePasswordResult.WrongPassword;
    }

    private Task RecordFailureAsync(Guid userId, Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var entry = AuditEntry.ForUser(AuditEventTypes.Account.PasswordChanged, userId, $"{reason}, Session {sessionId}");

        return auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);
    }
}
