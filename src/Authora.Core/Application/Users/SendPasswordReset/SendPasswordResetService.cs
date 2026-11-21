// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;
using Authora.Core.Application.OAuth;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Users.SendPasswordReset;

/// <summary>
///     The Service for an administrator to send a User a link to choose a new password.
/// </summary>
/// <remarks>
///     Unlike setting the password, the administrator never learns the new password, and the current one keeps working
///     until the User uses the link. The email is sent right away, so a delivery failure is reported to the caller.
///     Works whether or not <see cref="PasswordResetOptions.SelfServiceEnabled"/> is set, and is not limited by
///     <see cref="PasswordResetOptions.MaxEmailsPerHour"/>.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="emailTokenIssuer">Issues the one-time token of the link.</param>
/// <param name="emailSender">Sends the email.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="options">The password reset settings with the lifetime of the link.</param>
/// <param name="oauthOptions">The Issuer the link is built from.</param>
/// <param name="auditLog">The audit log to record the email.</param>
/// <param name="logger">The Logger to record the email.</param>
public sealed class SendPasswordResetService(
    IUserRepository userRepository,
    EmailTokenIssuer emailTokenIssuer,
    IEmailSender emailSender,
    PrivilegeGuard privilegeGuard,
    IOptions<PasswordResetOptions> options,
    IOptions<OAuthOptions> oauthOptions,
    IAuditLog auditLog,
    ILogger<SendPasswordResetService> logger)
{
    /// <summary>
    ///     Sends the User a reset link. Earlier links of the User stop working.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the mail server accepted the email.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when email delivery is not configured, or the User is deleted or deactivated.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    /// <exception cref="EmailDeliveryException">Thrown when the mail server could not be reached or rejected the email.</exception>
    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!emailSender.IsEnabled)
        {
            throw new ConflictException($"Email delivery is not configured ('{EmailOptions.SectionName}:{nameof(EmailOptions.Host)}'), so no reset link can be sent.");
        }

        var lifetime = options.Value.LinkLifetime;
        var entry = AuditEntry.ForUser(AuditEventTypes.Users.PasswordResetSent, id);
        Domain.Entities.User user;
        string token;

        await using (var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken))
        {
            user = await userRepository.GetUserById(id, cancellationToken)
                ?? throw new NotFoundException($"User '{id}' was not found.");

            DeletedUserGuard.EnsureNotDeleted(user);

            if (!user.CanSignIn)
            {
                throw new ConflictException("The User is deactivated. Activate the User first.");
            }

            await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

            // Joins the transaction, so the token is only saved if the check passed.
            token = await emailTokenIssuer.IssueAsync(user, EmailTokenPurpose.PasswordReset, lifetime, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await emailSender.SendAsync(
                EmailTemplates.PasswordReset(user, PasswordResetLink.Create(oauthOptions.Value.Issuer, token), lifetime),
                cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // The SMTP sender logged the reason. The link was never delivered, so it is not recorded as sent.
            await auditLog.RecordNowAsync(entry with { Details = "The email could not be sent", Outcome = AuditOutcome.Failed }, cancellationToken);

            throw;
        }

        // Recorded once the mail server accepted the email.
        await auditLog.RecordNowAsync(entry, cancellationToken);

        logger.PasswordResetSent(user.Id);
    }
}
