// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Account;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Email;
using Authora.Core.Application.OAuth;
using Authora.Core.Domain.Entities;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Users;

/// <summary>
///     Prepares and sends the emails with a link to confirm the address of a User.
/// </summary>
/// <param name="emailTokenIssuer">Issues the one-time token of the link.</param>
/// <param name="emailSender">Tells whether email delivery is configured.</param>
/// <param name="emailQueue">Sends the links that are sent by themselves in the background.</param>
/// <param name="options">The email verification settings.</param>
/// <param name="oauthOptions">The Issuer the link is built from.</param>
/// <param name="auditLog">The audit log to record the links sent by themselves.</param>
/// <param name="logger">The Logger to record the links.</param>
public sealed class EmailVerificationSender(
    EmailTokenIssuer emailTokenIssuer,
    IEmailSender emailSender,
    IEmailQueue emailQueue,
    IOptions<EmailVerificationOptions> options,
    IOptions<OAuthOptions> oauthOptions,
    IAuditLog auditLog,
    ILogger<EmailVerificationSender> logger)
{
    /// <summary>
    ///     Whether email delivery is configured, so links can be sent.
    /// </summary>
    public bool IsAvailable => emailSender.IsEnabled;

    /// <summary>
    ///     Whether a link is sent by itself to a User whose address is new: email delivery is configured,
    ///     <see cref="EmailVerificationOptions.SendAutomatically"/> is set, the User can sign in and the address is not
    ///     verified.
    /// </summary>
    /// <param name="user">The User, with the new address.</param>
    /// <returns><see langword="true"/> if <see cref="PrepareAsync"/> and <see cref="QueueAsync"/> should be called.</returns>
    public bool ShouldSendAutomatically(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return IsAvailable && options.Value.SendAutomatically && user.CanSignIn && !user.IsEmailVerified;
    }

    /// <summary>
    ///     Issues a new token for the User's current address and builds the email with the link. Earlier verification
    ///     links of the User stop working.
    /// </summary>
    /// <remarks>
    ///     Joins the transaction of the caller, if there is one, and saves the pending changes of the request, so call it
    ///     after the User was saved.
    /// </remarks>
    /// <param name="user">The User, with their current address.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>The email to send.</returns>
    public async Task<EmailMessage> PrepareAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var lifetime = options.Value.LinkLifetime;
        var token = await emailTokenIssuer.IssueAsync(user, EmailTokenPurpose.EmailVerification, lifetime, cancellationToken);

        return EmailTemplates.EmailVerification(user, EmailVerificationLink.Create(oauthOptions.Value.Issuer, token), lifetime);
    }

    /// <summary>
    ///     Queues a prepared email for delivery in the background and records it, so a slow or failing mail server does
    ///     not fail the change that caused it.
    /// </summary>
    /// <param name="user">The User the email is sent to.</param>
    /// <param name="message">The email from <see cref="PrepareAsync"/>.</param>
    /// <param name="reason">Why the link is sent, for the audit log, e.g. <c>the User was created</c>.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the email is queued and recorded.</returns>
    public async Task QueueAsync(User user, EmailMessage message, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(message);

        var entry = AuditEntry.ForUser(AuditEventTypes.Users.EmailVerificationSent, user.Id, $"Sent automatically, because {reason}");

        if (!emailQueue.TryEnqueue(message))
        {
            logger.EmailVerificationNotQueued(user.Id);

            await auditLog.RecordNowAsync(
                entry with { Details = $"Not sent, because the email queue is full ({reason})", Outcome = AuditOutcome.Failed },
                cancellationToken);

            return;
        }

        await auditLog.RecordNowAsync(entry, cancellationToken);

        logger.EmailVerificationQueued(user.Id);
    }
}
