// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Account;

/// <summary>
///     Log messages for sign-in and Sessions. Passwords, Session tokens and Emails are never logged.
/// </summary>
internal static partial class AccountLog
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "User {UserId} signed in with Session {SessionId}.")]
    public static partial void SignedIn(this ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning, Message = "Sign-in failed for an unknown Email.")]
    public static partial void SignInFailedUnknownUser(this ILogger logger);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning, Message = "Sign-in of User {UserId} failed: {Reason}.")]
    public static partial void SignInFailed(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning, Message = "User {UserId} was locked out until {LockoutEndAt} after too many failed sign-in attempts.")]
    public static partial void LockedOut(this ILogger logger, Guid userId, DateTime lockoutEndAt);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Information, Message = "User {UserId} signed out of Session {SessionId}.")]
    public static partial void SignedOut(this ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(EventId = 5006, Level = LogLevel.Information, Message = "Session {SessionId} of User {UserId} was revoked, because the User can no longer sign in or signed out everywhere.")]
    public static partial void SessionInvalidated(this ILogger logger, Guid sessionId, Guid userId);

    [LoggerMessage(EventId = 5007, Level = LogLevel.Information, Message = "Sign-in attempt {Attempt} was discarded, because the User was changed by another request at the same time.")]
    public static partial void SignInRetried(this ILogger logger, int attempt);

    [LoggerMessage(EventId = 5008, Level = LogLevel.Information, Message = "User {UserId} signed out of Session {SessionId} at the request of Client {ClientId}.")]
    public static partial void SignedOutByClient(this ILogger logger, Guid userId, Guid sessionId, Guid clientId);

    [LoggerMessage(EventId = 5009, Level = LogLevel.Information, Message = "User {UserId} changed their password in Session {SessionId}. All other Sessions and all tokens of the User were revoked.")]
    public static partial void PasswordChanged(this ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(EventId = 5010, Level = LogLevel.Warning, Message = "Password change of User {UserId} failed: {Reason}.")]
    public static partial void PasswordChangeFailed(this ILogger logger, Guid userId, string reason);

    // Only the message: the inner SMTP error can quote addresses, see SmtpEmailSender.
    [LoggerMessage(EventId = 5011, Level = LogLevel.Warning, Message = "The notification about the password change of User {UserId} could not be sent: {Reason}")]
    public static partial void PasswordChangeNotificationFailed(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 5012, Level = LogLevel.Information, Message = "A password reset link was sent to User {UserId}.")]
    public static partial void PasswordResetRequested(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 5013, Level = LogLevel.Warning, Message = "A password reset was requested for an unknown Email. No link was sent.")]
    public static partial void PasswordResetRequestedForUnknownUser(this ILogger logger);

    [LoggerMessage(EventId = 5014, Level = LogLevel.Warning, Message = "No password reset link was sent to User {UserId}: {Reason}.")]
    public static partial void PasswordResetNotSent(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 5015, Level = LogLevel.Information, Message = "User {UserId} set a new password with a reset link. All Sessions and tokens of the User were revoked.")]
    public static partial void PasswordReset(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 5016, Level = LogLevel.Warning, Message = "A password reset link was not accepted: {Reason}.")]
    public static partial void PasswordResetRejected(this ILogger logger, string reason);

    [LoggerMessage(EventId = 5017, Level = LogLevel.Information, Message = "User {UserId} confirmed their Email with a verification link.")]
    public static partial void EmailVerified(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 5018, Level = LogLevel.Warning, Message = "An email verification link was not accepted: {Reason}.")]
    public static partial void EmailVerificationRejected(this ILogger logger, string reason);

    [LoggerMessage(EventId = 5019, Level = LogLevel.Information, Message = "User {UserId} entered the correct password; sign-in {ChallengeId} waits for a second factor.")]
    public static partial void SignInPending(this ILogger logger, Guid userId, Guid challengeId);

    [LoggerMessage(EventId = 5020, Level = LogLevel.Warning, Message = "Second factor {Method} of User {UserId} was wrong in sign-in {ChallengeId}.")]
    public static partial void SecondFactorFailed(this ILogger logger, Guid userId, Guid challengeId, string method);

    [LoggerMessage(EventId = 5021, Level = LogLevel.Information, Message = "Pending sign-in {ChallengeId} was not completed: {Reason}.")]
    public static partial void PendingSignInRejected(this ILogger logger, Guid challengeId, string reason);

    [LoggerMessage(EventId = 5022, Level = LogLevel.Information, Message = "User {UserId} started a step-up {ChallengeId} of Session {SessionId}.")]
    public static partial void StepUpStarted(this ILogger logger, Guid userId, Guid challengeId, Guid sessionId);

    [LoggerMessage(EventId = 5023, Level = LogLevel.Information, Message = "User {UserId} completed {Method} for Session {SessionId}, which got a new token.")]
    public static partial void SteppedUp(this ILogger logger, Guid userId, Guid sessionId, string method);
}
