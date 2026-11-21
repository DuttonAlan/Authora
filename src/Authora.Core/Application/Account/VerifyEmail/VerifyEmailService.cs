// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Abstractions;
using Authora.Core.Application.Audit;
using Authora.Core.Application.Email;
using Authora.Core.Application.Exceptions;
using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Account.VerifyEmail;

/// <summary>
///     The Service for a User to confirm their Email with the one-time token of a verification link.
/// </summary>
/// <remarks>
///     The link proves that whoever opens it receives email at the address, so no sign-in is needed. The token only
///     works while the User still has the address it was sent to, and is used up even if saving fails afterwards.
/// </remarks>
/// <param name="emailTokenRedeemer">Checks and uses up the token.</param>
/// <param name="userRepository">An Instance of a IUserRepository to save the User.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="auditLog">The audit log to record the verification.</param>
/// <param name="logger">The Logger to record the verification.</param>
public sealed class VerifyEmailService(
    EmailTokenRedeemer emailTokenRedeemer,
    IUserRepository userRepository,
    TimeProvider timeProvider,
    IAuditLog auditLog,
    ILogger<VerifyEmailService> logger)
{
    /// <summary>
    ///     Checks whether a link still works, without using it up, so the page can offer the confirmation only then.
    /// </summary>
    /// <param name="token">The one-time token from the link.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns><see langword="true"/> if the address can be confirmed with the link right now.</returns>
    public async Task<bool> IsValidLinkAsync(string? token, CancellationToken cancellationToken = default)
    {
        var user = await emailTokenRedeemer.CheckAsync(token, EmailTokenPurpose.EmailVerification, cancellationToken);

        return user is { CanSignIn: true };
    }

    /// <summary>
    ///     Uses up the token and marks the Email of its User as verified.
    /// </summary>
    /// <param name="token">The one-time token from the link.</param>
    /// <param name="cancellationToken">CancellationToken to cancel the request.</param>
    /// <returns>Whether the Email was verified, or that the link does not work.</returns>
    /// <exception cref="ConflictException">Thrown when the User was changed by another request at the same time. The token is used up then.</exception>
    public async Task<VerifyEmailResult> ExecuteAsync(string? token, CancellationToken cancellationToken = default)
    {
        var user = await emailTokenRedeemer.RedeemAsync(token, EmailTokenPurpose.EmailVerification, cancellationToken);

        if (user is null)
        {
            logger.EmailVerificationRejected("unknown, expired, used or replaced link");

            var rejected = new AuditEntry(AuditEventTypes.Account.EmailVerified, Details: "Unknown, expired, used or replaced link");

            await auditLog.RecordNowAsync(rejected with { Outcome = AuditOutcome.Failed }, cancellationToken);

            return VerifyEmailResult.InvalidLink;
        }

        if (!user.CanSignIn)
        {
            logger.EmailVerificationRejected("the User cannot sign in");

            var rejected = AuditEntry.ForUser(AuditEventTypes.Account.EmailVerified, user.Id, "The User cannot sign in");

            await auditLog.RecordNowAsync(rejected with { Outcome = AuditOutcome.Failed }, cancellationToken);

            return VerifyEmailResult.InvalidLink;
        }

        user.MarkEmailVerified(timeProvider.GetUtcNow().UtcDateTime);

        auditLog.Record(AuditEntry.ForUser(AuditEventTypes.Account.EmailVerified, user.Id));

        // Throws a ConflictException if the User was changed since it was loaded, e.g. by a concurrent change of the Email.
        await userRepository.SaveChangesAsync(cancellationToken);

        logger.EmailVerified(user.Id);

        return VerifyEmailResult.Verified;
    }
}
