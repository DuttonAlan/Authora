// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Email;
using Authora.Core.Application.Users;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Authora.Core.Application.Account.RequestEmailVerification;

/// <summary>
///     The Service for a signed-in User whose Email is not verified to send themselves a new verification link, e.g.
///     after the first one expired or got lost.
/// </summary>
/// <remarks>
///     The link goes to the address of the account, the same as one sent by an administrator, and earlier links stop
///     working. Each User can request at most <see cref="EmailVerificationOptions.MaxEmailsPerHour"/> links per hour;
///     the page limits requests per IP address on top. The email is sent right away, so the User learns about a
///     delivery failure.
/// </remarks>
/// <param name="userRepository">An Instance of a IUserRepository to load the User.</param>
/// <param name="emailTokenRepository">The repository to count the links already sent.</param>
/// <param name="emailVerificationSender">Issues the token and builds the email.</param>
/// <param name="emailSender">Sends the email.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="options">The email verification settings.</param>
/// <param name="auditLog">The audit log to record the request.</param>
/// <param name="logger">The Logger to record the request.</param>
public sealed class RequestEmailVerificationService(
    IUserRepository userRepository,
    IEmailTokenRepository emailTokenRepository,
    EmailVerificationSender emailVerificationSender,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    IOptions<EmailVerificationOptions> options,
    IAuditLog auditLog,
    ILogger<RequestEmailVerificationService> logger)
{
    /// <summary>
    ///     Whether Users can request links: email delivery is configured.
    /// </summary>
    public bool IsAvailable => emailVerificationSender.IsAvailable;

    /// <summary>
    ///     Whether the pages should offer the User a new link: the request <see cref="IsAvailable"/>, the User can sign in
    ///     and their Email is not verified.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if a link can be requested.</returns>
    public async Task<bool> CanRequestAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return false;
        }

        var user = await userRepository.GetUserById(userId, cancellationToken);

        return user is { CanSignIn: true, IsEmailVerified: false };
    }

    /// <summary>
    ///     Sends the User a new verification link, unless their Email is verified or they got too many links in the last
    ///     hour.
    /// </summary>
    /// <param name="userId">Id of the signed-in User.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the link was sent, or why not.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the request is not <see cref="IsAvailable"/>. The caller checks this first.</exception>
    public async Task<RequestEmailVerificationResult> ExecuteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            throw new InvalidOperationException("Email delivery is not configured, so no verification link can be sent.");
        }

        var user = await userRepository.GetUserById(userId, cancellationToken);

        if (user is not { CanSignIn: true })
        {
            // The Session of the User ends with the next request; nothing was sent.
            return RequestEmailVerificationResult.CannotSignIn;
        }

        if (user.IsEmailVerified)
        {
            return RequestEmailVerificationResult.AlreadyVerified;
        }

        var settings = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Counts the links sent automatically or by an administrator as well. Not atomic: concurrent requests of the
        // same User may both pass, which the rate limit per IP address bounds.
        var sentLastHour = await emailTokenRepository.CountEmailTokensIssuedSince(
            user.Id,
            EmailTokenPurpose.EmailVerification,
            now - TimeSpan.FromHours(1),
            cancellationToken);

        if (sentLastHour >= settings.MaxEmailsPerHour)
        {
            await RecordNotSentAsync(user.Id, "Too many verification links in the last hour", cancellationToken);

            return RequestEmailVerificationResult.TooManyLinks;
        }

        var message = await emailVerificationSender.PrepareAsync(user, cancellationToken);

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            // The SMTP sender logged the reason. The link was never delivered, so it is not recorded as sent.
            await RecordNotSentAsync(user.Id, "The email could not be sent", cancellationToken);

            return RequestEmailVerificationResult.DeliveryFailed;
        }

        // Recorded once the mail server accepted the email.
        var sent = AuditEntry.ForUser(
            AuditEventTypes.Account.EmailVerificationRequested,
            user.Id,
            $"Link valid until {now + settings.LinkLifetime:O}");

        await auditLog.RecordNowAsync(sent, cancellationToken);

        logger.EmailVerificationRequested(user.Id);

        return RequestEmailVerificationResult.Sent;
    }

    private async Task RecordNotSentAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        logger.EmailVerificationRequestNotSent(userId, reason);

        var entry = AuditEntry.ForUser(AuditEventTypes.Account.EmailVerificationRequested, userId, reason);

        await auditLog.RecordNowAsync(entry with { Outcome = AuditOutcome.Failed }, cancellationToken);
    }
}
