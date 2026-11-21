// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Authorization;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users.SendEmailVerification;

/// <summary>
///     The Service for an administrator to send a User a link to confirm their Email, e.g. after the link sent by itself
///     expired.
/// </summary>
/// <remarks>
///     The email is sent right away, so a delivery failure is reported to the caller. Works whether or not
///     <see cref="Account.EmailVerificationOptions.SendAutomatically"/> is set.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="emailVerificationSender">Issues the token and builds the email.</param>
/// <param name="emailSender">Sends the email.</param>
/// <param name="privilegeGuard">The guard that checks that the caller holds all Permissions of the User.</param>
/// <param name="auditLog">The audit log to record the email.</param>
/// <param name="logger">The Logger to record the email.</param>
public sealed class SendEmailVerificationService(
    IUserRepository userRepository,
    EmailVerificationSender emailVerificationSender,
    IEmailSender emailSender,
    PrivilegeGuard privilegeGuard,
    IAuditLog auditLog,
    ILogger<SendEmailVerificationService> logger)
{
    /// <summary>
    ///     Sends the User a verification link. Earlier verification links of the User stop working.
    /// </summary>
    /// <param name="id">Id of the User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the mail server accepted the email.</returns>
    /// <exception cref="NotFoundException">Thrown when the User does not exist.</exception>
    /// <exception cref="ConflictException">Thrown when email delivery is not configured, the User is deleted or deactivated, or their Email is already verified.</exception>
    /// <exception cref="ForbiddenException">Thrown when the caller may not act on the User.</exception>
    /// <exception cref="EmailDeliveryException">Thrown when the mail server could not be reached or rejected the email.</exception>
    public async Task ExecuteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!emailVerificationSender.IsAvailable)
        {
            throw new ConflictException($"Email delivery is not configured ('{EmailOptions.SectionName}:{nameof(EmailOptions.Host)}'), so no verification link can be sent.");
        }

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.EmailVerificationSent, id);
        Domain.Entities.User user;
        EmailMessage message;

        await using (var transaction = await privilegeGuard.BeginChangeAsync(cancellationToken))
        {
            user = await userRepository.GetUserById(id, cancellationToken)
                ?? throw new NotFoundException($"User '{id}' was not found.");

            DeletedUserGuard.EnsureNotDeleted(user);

            if (!user.CanSignIn)
            {
                throw new ConflictException("The User is deactivated. Activate the User first.");
            }

            if (user.IsEmailVerified)
            {
                throw new ConflictException("The Email of the User is already verified.");
            }

            await privilegeGuard.EnsureCanActOnUserAsync(user.Id, entry, cancellationToken);

            // Joins the transaction, so the token is only saved if the check passed.
            message = await emailVerificationSender.PrepareAsync(user, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // The SMTP sender logged the reason. The link was never delivered, so it is not recorded as sent.
            await auditLog.RecordNowAsync(entry with { Details = "The email could not be sent", Outcome = AuditOutcome.Failed }, cancellationToken);

            throw;
        }

        // Recorded once the mail server accepted the email.
        await auditLog.RecordNowAsync(entry, cancellationToken);

        logger.EmailVerificationSent(user.Id);
    }
}
