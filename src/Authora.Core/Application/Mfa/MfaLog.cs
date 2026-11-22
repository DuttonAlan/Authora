// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Mfa;

/// <summary>
///     Log messages for multi-factor authentication. Secrets, codes and recovery codes are never logged.
/// </summary>
internal static partial class MfaLog
{
    [LoggerMessage(EventId = 16001, Level = LogLevel.Information, Message = "User {UserId} started to set up an authenticator app.")]
    public static partial void TotpEnrollmentStarted(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 16002, Level = LogLevel.Information, Message = "User {UserId} set up an authenticator app and got {Count} recovery codes.")]
    public static partial void TotpEnrolled(this ILogger logger, Guid userId, int count);

    [LoggerMessage(EventId = 16003, Level = LogLevel.Information, Message = "User {UserId} entered a valid code of their authenticator app.")]
    public static partial void TotpVerified(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 16004, Level = LogLevel.Warning, Message = "A code of User {UserId} was rejected: {Reason}.")]
    public static partial void VerificationFailed(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 16005, Level = LogLevel.Warning, Message = "User {UserId} used a recovery code; {Remaining} left.")]
    public static partial void RecoveryCodeUsed(this ILogger logger, Guid userId, int remaining);

    [LoggerMessage(EventId = 16006, Level = LogLevel.Information, Message = "User {UserId} generated new recovery codes; the previous ones no longer work.")]
    public static partial void RecoveryCodesRegenerated(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 16007, Level = LogLevel.Information, Message = "User {UserId} removed their authenticator app in Session {SessionId}. All other Sessions and all tokens of the User were revoked.")]
    public static partial void TotpRemoved(this ILogger logger, Guid userId, Guid sessionId);

    [LoggerMessage(EventId = 16008, Level = LogLevel.Warning, Message = "Removing the authenticator app of User {UserId} failed: {Reason}.")]
    public static partial void TotpRemovalFailed(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 16009, Level = LogLevel.Error, Message = "The authenticator secret of User {UserId} cannot be decrypted. Was the Data Protection key ring lost or replaced?")]
    public static partial void TotpSecretUnreadable(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 16010, Level = LogLevel.Information, Message = "User {UserId} must complete the authenticator app in Session {SessionId} before generating new recovery codes.")]
    public static partial void RecoveryCodesNeedStepUp(this ILogger logger, Guid userId, Guid sessionId);
}
