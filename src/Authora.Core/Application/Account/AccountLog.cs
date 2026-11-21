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

    [LoggerMessage(EventId = 5012, Level = LogLevel.Information, Message = "A password reset link for User {UserId} was queued for delivery.")]
    public static partial void PasswordResetRequested(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 5013, Level = LogLevel.Warning, Message = "A password reset was requested for an unknown Email. No link was sent.")]
    public static partial void PasswordResetRequestedForUnknownUser(this ILogger logger);

    [LoggerMessage(EventId = 5014, Level = LogLevel.Warning, Message = "No password reset link was sent to User {UserId}: {Reason}.")]
    public static partial void PasswordResetNotSent(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 5015, Level = LogLevel.Information, Message = "User {UserId} set a new password with a reset link. All Sessions and tokens of the User were revoked.")]
    public static partial void PasswordReset(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 5016, Level = LogLevel.Warning, Message = "A password reset link was not accepted: {Reason}.")]
    public static partial void PasswordResetRejected(this ILogger logger, string reason);
}
