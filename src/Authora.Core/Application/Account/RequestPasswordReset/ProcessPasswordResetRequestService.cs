// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Email;
using Authora.Core.Application.OAuth;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.RequestPasswordReset;

/// <summary>
///     Handles a request for a password reset link in the background, after
///     <see cref="RequestPasswordResetService"/> queued it: sends a link if an account that can sign in has the address.
/// </summary>
/// <remarks>
///     Each address gets at most <see cref="PasswordResetOptions.MaxEmailsPerHour"/> links per hour, and only the newest
///     link works. The audit events carry the actor of the request that queued it.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to find the User.</param>
/// <param name="emailTokenRepository">The repository to count the links already sent.</param>
/// <param name="emailTokenIssuer">Issues the one-time token of the link.</param>
/// <param name="emailSender">Sends the email.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="options">The password reset settings.</param>
/// <param name="oauthOptions">The Issuer the link is built from.</param>
/// <param name="auditLog">The audit log to record the request.</param>
/// <param name="logger">The Logger to record the request.</param>
public sealed class ProcessPasswordResetRequestService(
    IUserRepository userRepository,
    IEmailTokenRepository emailTokenRepository,
    EmailTokenIssuer emailTokenIssuer,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    IOptions<PasswordResetOptions> options,
    IOptions<OAuthOptions> oauthOptions,
    IAuditLog auditLog,
    ILogger<ProcessPasswordResetRequestService> logger)
{
    /// <summary>
    ///     Sends a reset link to the address if an account that can sign in has it, otherwise only records the request.
    /// </summary>
    /// <param name="request">The queued request.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the processing.</param>
    /// <returns>A Task that completes once the mail server accepted the email, or once it is clear that none is sent.</returns>
    public async Task ExecuteAsync(QueuedPasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > User.EmailMaxLength
            ? null
            : await userRepository.GetUserByNormalizedEmail(User.Normalize(request.Email), cancellationToken);

        if (user is null)
        {
            logger.PasswordResetRequestedForUnknownUser();

            // The address is not recorded: whoever typed it may not have an account.
            var unknown = new AuditEntry(AuditEventTypes.Account.PasswordResetRequested, Details: "Unknown Email");

            await RecordAsync(unknown with { Outcome = AuditOutcome.Failed }, request, cancellationToken);

            return;
        }

        if (!user.CanSignIn)
        {
            await RecordNotSentAsync(user.Id, "The User cannot sign in", request, cancellationToken);

            return;
        }

        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Not atomic: another server instance or an administrator may send a link at the same time. The queue of one
        // instance is processed one request after another, and the rate limit per IP address bounds the rest.
        var sentLastHour = await emailTokenRepository.CountEmailTokensIssuedSince(
            user.Id,
            EmailTokenPurpose.PasswordReset,
            now - TimeSpan.FromHours(1),
            cancellationToken);

        if (sentLastHour >= settings.MaxEmailsPerHour)
        {
            await RecordNotSentAsync(user.Id, "Too many reset links for the address in the last hour", request, cancellationToken);

            return;
        }

        var token = await emailTokenIssuer.IssueAsync(user, EmailTokenPurpose.PasswordReset, settings.LinkLifetime, cancellationToken);
        var link = PasswordResetLink.Create(oauthOptions.Value.Issuer, token);

        try
        {
            await emailSender.SendAsync(EmailTemplates.PasswordReset(user, link, settings.LinkLifetime), cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // The SMTP sender logged the reason. The link was never delivered, so it is not recorded as sent.
            await RecordNotSentAsync(user.Id, "The email could not be sent", request, cancellationToken);

            return;
        }

        // Recorded once the mail server accepted the email.
        var sent = AuditEntry.ForUser(
            AuditEventTypes.Account.PasswordResetRequested,
            user.Id,
            $"Link valid until {now + settings.LinkLifetime:O}");

        await RecordAsync(sent, request, cancellationToken);

        logger.PasswordResetRequested(user.Id);
    }

    private async Task RecordNotSentAsync(Guid userId, string reason, QueuedPasswordResetRequest request, CancellationToken cancellationToken)
    {
        logger.PasswordResetNotSent(userId, reason);

        var entry = AuditEntry.ForUser(AuditEventTypes.Account.PasswordResetRequested, userId, reason);

        await RecordAsync(entry with { Outcome = AuditOutcome.Failed }, request, cancellationToken);
    }

    private Task RecordAsync(AuditEntry entry, QueuedPasswordResetRequest request, CancellationToken cancellationToken)
    {
        return auditLog.RecordNowAsync(entry with { Actor = request.Actor }, cancellationToken);
    }
}
