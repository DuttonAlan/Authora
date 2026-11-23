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

namespace Authora.Core.Application.Account.ResetPassword;

/// <summary>
///     The Service for a User to set a new password with the one-time token of a reset link.
/// </summary>
/// <remarks>
///     The token is used up even if saving fails afterwards, so a link works at most once. The new password rotates the
///     Security Stamp: every Session and every Grant (with its Access and Refresh Tokens) of the User ends. A lockout
///     ends too, since the User proved that they receive the email of the account.
/// </remarks>
/// <param name="emailTokenRedeemer">Checks and uses up the token.</param>
/// <param name="userRepository">An Instance of a IUserRepository to save the User.</param>
/// <param name="sessionRevoker">Revokes the Sessions of the User and their Grants and notifies their Clients.</param>
/// <param name="passwordHasher">The hasher for the new password.</param>
/// <param name="emailQueue">Sends the notification about the change in the background.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="auditLog">The audit log to record the reset.</param>
/// <param name="logger">The Logger to record the reset.</param>
public sealed class ResetPasswordService(
    EmailTokenRedeemer emailTokenRedeemer,
    IUserRepository userRepository,
    SessionRevoker sessionRevoker,
    IPasswordHasher passwordHasher,
    IEmailQueue emailQueue,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<ResetPasswordService> logger)
{
    /// <summary>
    ///     Checks whether a link still works, without using it up, so the page can show the form only then.
    /// </summary>
    /// <param name="token">The one-time token from the link.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if a password can be set with the link right now.</returns>
    public async Task<bool> IsValidLinkAsync(string? token, CancellationToken cancellationToken = default)
    {
        var user = await emailTokenRedeemer.CheckAsync(token, EmailTokenPurpose.PasswordReset, cancellationToken);

        return user is { CanSignIn: true };
    }

    /// <summary>
    ///     Uses up the token and sets the new password.
    /// </summary>
    /// <param name="request">The token and the new password.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the password was set, or that the link does not work.</returns>
    /// <exception cref="DomainValidationException">Thrown when the new password does not follow the <see cref="PasswordPolicy"/>. The token is not used up then.</exception>
    /// <exception cref="ConflictException">Thrown when the User was changed by another request at the same time. The token is used up then.</exception>
    public async Task<ResetPasswordResult> ExecuteAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Checked first, so a typo in the password does not use up the link.
        PasswordPolicy.ThrowIfInvalid(request.NewPassword, nameof(request));

        var user = await emailTokenRedeemer.RedeemAsync(request.Token, EmailTokenPurpose.PasswordReset, cancellationToken);

        if (user is null)
        {
            logger.PasswordResetRejected("unknown, expired, used or replaced link");

            var rejected = new AuditEntry(AuditEventTypes.Account.PasswordReset, Details: "Unknown, expired, used or replaced link");

            await auditLog.RecordNowAsync(rejected with { Outcome = AuditOutcome.Failed }, cancellationToken);

            return ResetPasswordResult.InvalidLink;
        }

        if (!user.CanSignIn)
        {
            logger.PasswordResetRejected("the User cannot sign in");

            var rejected = AuditEntry.ForUser(AuditEventTypes.Account.PasswordReset, user.Id, "The User cannot sign in");

            await auditLog.RecordNowAsync(rejected with { Outcome = AuditOutcome.Failed }, cancellationToken);

            return ResetPasswordResult.InvalidLink;
        }

        user.SetPasswordHash(passwordHasher.Hash(request.NewPassword));
        user.Unlock();

        await using var transaction = await sessionRevoker.BeginAsync(cancellationToken);
        await sessionRevoker.RevokeSessionsOfUserAsync(user.Id, exceptSessionId: null, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Account.PasswordReset, user.Id));

        // Throws a ConflictException if the User was changed since it was loaded, e.g. by a concurrent password change;
        // the revocation of the Sessions is rolled back then.
        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.PasswordReset(user.Id);

        // The password is set either way; a lost notification is only logged by the queue.
        _ = emailQueue.TryEnqueue(EmailTemplates.PasswordWasReset(user, timeProvider.GetUtcNow().UtcDateTime));

        return ResetPasswordResult.Reset;
    }
}
