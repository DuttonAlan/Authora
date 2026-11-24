// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account.Sessions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.Users.SetPassword;
using Authora.Core.Domain.Exceptions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.AddPassword;

/// <summary>
///     The Service for a signed-in User without a password, e.g. one who signed up through an external identity
///     provider, to set their first password.
/// </summary>
/// <remarks>
///     There is no current password to check, so the Session must have authenticated recently
///     (<see cref="SignInOptions.RecentAuthenticationWindow"/>): a browser left signed in cannot be used to add a way
///     to sign in. Like a password change, the new password rotates the Security Stamp: every other Session and every
///     Grant of the User ends, only the Session the password was set in stays valid. Recorded as
///     <see cref="AuditEventTypes.Account.PasswordChanged"/>.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to change the User.</param>
/// <param name="sessionRepository">An Instance of a ISessionRepository to keep the current Session valid.</param>
/// <param name="sessionRevoker">Revokes the other Sessions of the User and their Grants and notifies their Clients.</param>
/// <param name="passwordHasher">The hasher for the new password.</param>
/// <param name="emailSender">Sends the notification about the change, if email delivery is configured.</param>
/// <param name="timeProvider">The TimeProvider for the Session checks.</param>
/// <param name="options">The sign-in settings with the idle timeout and the recent authentication window.</param>
/// <param name="auditLog">The audit log to record the change.</param>
/// <param name="logger">The Logger to record the change.</param>
public sealed class AddPasswordService(
    IUserRepository userRepository,
    ISessionRepository sessionRepository,
    SessionRevoker sessionRevoker,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    IOptions<SignInOptions> options,
    IAuditLog auditLog,
    ILogger<AddPasswordService> logger)
{
    /// <summary>
    ///     Sets the first password of the User.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="sessionId">Id of the Session the User sets the password in. It stays valid.</param>
    /// <param name="password">The new password, following the <see cref="PasswordPolicy"/>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the password was set, or why not.</returns>
    /// <exception cref="DomainValidationException">Thrown when the password does not follow the <see cref="PasswordPolicy"/>.</exception>
    /// <exception cref="ConflictException">Thrown when the User was changed by another request at the same time.</exception>
    public async Task<AddPasswordResult> ExecuteAsync(
        Guid userId,
        Guid sessionId,
        string password,
        CancellationToken cancellationToken = default)
    {
        PasswordPolicy.ThrowIfInvalid(password, nameof(password));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = await userRepository.GetUserById(userId, cancellationToken);
        var session = await sessionRepository.GetSessionById(sessionId, cancellationToken);

        if (user is null
            || !user.CanSignIn
            || session is null
            || session.UserId != user.Id
            || session.SecurityStamp != user.SecurityStamp
            || !session.IsActive(now, options.Value.SessionIdleTimeout))
        {
            return AddPasswordResult.SessionEnded;
        }

        if (user.HasPassword)
        {
            return AddPasswordResult.HasPassword;
        }

        // Checked again, although the page only runs after a recent authentication: it may have passed since.
        if (now - session.AuthenticatedAt > options.Value.RecentAuthenticationWindow)
        {
            return AddPasswordResult.SignInRequired;
        }

        user.SetPasswordHash(passwordHasher.Hash(password));
        session.RenewSecurityStamp(user);

        await using var transaction = await sessionRevoker.BeginAsync(cancellationToken);
        await sessionRevoker.RevokeSessionsOfUserAsync(user.Id, session.Id, now, cancellationToken);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Account.PasswordChanged, user.Id, $"First password set, Session {session.Id}"));

        // User and Session share the request's DbContext, so both are saved together with the audit event. Throws a
        // ConflictException if the User was changed since it was loaded, e.g. by a password reset at the same time.
        await userRepository.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.PasswordAdded(user.Id, session.Id);

        if (emailSender.IsEnabled)
        {
            try
            {
                await emailSender.SendAsync(EmailTemplates.PasswordChanged(user, now), cancellationToken);
            }
            catch (EmailDeliveryException exception)
            {
                // The password is set either way; a lost notification must not report the change as failed.
                logger.PasswordChangeNotificationFailed(user.Id, exception.Message);
            }
        }

        return AddPasswordResult.Added;
    }
}

/// <summary>
///     The result of <see cref="AddPasswordService"/>.
/// </summary>
public enum AddPasswordResult
{
    /// <summary>
    ///     The password was set. The current Session stays valid, all other Sessions and all tokens were revoked.
    /// </summary>
    Added,

    /// <summary>
    ///     The User already has a password; they change it with the current one instead.
    /// </summary>
    HasPassword,

    /// <summary>
    ///     The User did not authenticate recently enough and must sign in again first.
    /// </summary>
    SignInRequired,

    /// <summary>
    ///     The Session is no longer valid, e.g. because it was revoked or the User can no longer sign in.
    /// </summary>
    SessionEnded
}
