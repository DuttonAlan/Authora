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
///     The Service for a User who forgot their password to ask for a reset link by email.
/// </summary>
/// <remarks>
///     The caller learns nothing about whether an account exists: the result is the same for every address, and the
///     email is sent in the background through <see cref="IEmailQueue"/>, so the response time does not depend on the
///     mail server. Each address gets at most <see cref="PasswordResetOptions.MaxEmailsPerHour"/> links per hour, and
///     only the newest link works.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to find the User.</param>
/// <param name="emailTokenRepository">The repository to count the links already sent.</param>
/// <param name="emailTokenIssuer">Issues the one-time token of the link.</param>
/// <param name="emailSender">Tells whether email delivery is configured.</param>
/// <param name="emailQueue">Sends the email in the background.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="options">The password reset settings.</param>
/// <param name="oauthOptions">The Issuer the link is built from.</param>
/// <param name="auditLog">The audit log to record the request.</param>
/// <param name="logger">The Logger to record the request.</param>
public sealed class RequestPasswordResetService(
    IUserRepository userRepository,
    IEmailTokenRepository emailTokenRepository,
    EmailTokenIssuer emailTokenIssuer,
    IEmailSender emailSender,
    IEmailQueue emailQueue,
    TimeProvider timeProvider,
    IOptions<PasswordResetOptions> options,
    IOptions<OAuthOptions> oauthOptions,
    IAuditLog auditLog,
    ILogger<RequestPasswordResetService> logger)
{
    /// <summary>
    ///     Whether Users can ask for a reset link: email delivery is configured and
    ///     <see cref="PasswordResetOptions.SelfServiceEnabled"/> is set.
    /// </summary>
    public bool IsAvailable => emailSender.IsEnabled && options.Value.SelfServiceEnabled;

    /// <summary>
    ///     Sends a reset link to the address if an account that can sign in has it, otherwise does nothing.
    /// </summary>
    /// <param name="request">The request with the address.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>A Task that completes once the link is queued, or once it is clear that none is sent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the reset is not <see cref="IsAvailable"/>. The caller checks this first.</exception>
    public async Task ExecuteAsync(RequestPasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAvailable)
        {
            throw new InvalidOperationException("The password reset by email is not available.");
        }

        var user = string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > User.EmailMaxLength
            ? null
            : await userRepository.GetUserByNormalizedEmail(User.Normalize(request.Email), cancellationToken);

        if (user is null)
        {
            logger.PasswordResetRequestedForUnknownUser();

            // The address is not recorded: whoever typed it may not have an account.
            var unknown = new AuditEntry(AuditEventTypes.Account.PasswordResetRequested, Details: "Unknown Email");

            await auditLog.RecordNowAsync(unknown with { Outcome = AuditOutcome.Failed }, cancellationToken);

            return;
        }

        if (!user.CanSignIn)
        {
            await RecordNotSentAsync(user.Id, "The User cannot sign in", cancellationToken);

            return;
        }

        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Not atomic: concurrent requests may send a link or two more. The rate limit per IP address bounds that.
        var sentLastHour = await emailTokenRepository.CountEmailTokensIssuedSince(
            user.Id,
            EmailTokenPurpose.PasswordReset,
            now - TimeSpan.FromHours(1),
            cancellationToken);

        if (sentLastHour >= settings.MaxEmailsPerHour)
        {
            await RecordNotSentAsync(user.Id, "Too many reset links for the address in the last hour", cancellationToken);

            return;
        }

        // Staged in the request's DbContext, so it is saved together with the token.
        auditLog.Record(AuditEntry.ForUser(
            AuditEventTypes.Account.PasswordResetRequested,
            user.Id,
            $"Link valid until {now + settings.LinkLifetime:O}"));

        var token = await emailTokenIssuer.IssueAsync(user, EmailTokenPurpose.PasswordReset, settings.LinkLifetime, cancellationToken);
        var link = PasswordResetLink.Create(oauthOptions.Value.Issuer, token);

        if (!emailQueue.TryEnqueue(EmailTemplates.PasswordReset(user, link, settings.LinkLifetime)))
        {
            logger.PasswordResetNotSent(user.Id, "the email queue is full");

            return;
        }

        logger.PasswordResetRequested(user.Id);
    }

    private async Task RecordNotSentAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        logger.PasswordResetNotSent(userId, reason);

        var entry = AuditEntry.ForUser(AuditEventTypes.Account.PasswordResetRequested, userId, reason);

        await auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);
    }
}
