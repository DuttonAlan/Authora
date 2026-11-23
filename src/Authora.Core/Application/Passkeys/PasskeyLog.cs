// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Passkeys;

/// <summary>
///     Log messages for passkeys. Challenges, public keys and responses are never logged, only the Ids of passkeys.
/// </summary>
internal static partial class PasskeyLog
{
    [LoggerMessage(EventId = 17001, Level = LogLevel.Information, Message = "User {UserId} registered passkey {PasskeyId}.")]
    public static partial void PasskeyRegistered(this ILogger logger, Guid userId, Guid passkeyId);

    [LoggerMessage(EventId = 17002, Level = LogLevel.Warning, Message = "Registering a passkey for User {UserId} failed: {Reason}.")]
    public static partial void PasskeyRegistrationFailed(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 17003, Level = LogLevel.Information, Message = "User {UserId} renamed passkey {PasskeyId}.")]
    public static partial void PasskeyRenamed(this ILogger logger, Guid userId, Guid passkeyId);

    [LoggerMessage(EventId = 17004, Level = LogLevel.Information, Message = "User {UserId} removed passkey {PasskeyId} in Session {SessionId}. All other Sessions and all tokens of the User were revoked.")]
    public static partial void PasskeyRemoved(this ILogger logger, Guid userId, Guid passkeyId, Guid sessionId);

    [LoggerMessage(EventId = 17005, Level = LogLevel.Information, Message = "User {UserId} verified with passkey {PasskeyId}.")]
    public static partial void PasskeyVerified(this ILogger logger, Guid userId, Guid passkeyId);

    [LoggerMessage(EventId = 17006, Level = LogLevel.Warning, Message = "A passkey response of User {UserId} was rejected: {Reason}.")]
    public static partial void PasskeyRejected(this ILogger logger, Guid userId, string reason);

    [LoggerMessage(EventId = 17007, Level = LogLevel.Warning, Message = "The signature counter of passkey {PasskeyId} of User {UserId} did not increase ({Received} after {Stored}). The authenticator may have been cloned.")]
    public static partial void PasskeySignCountDidNotIncrease(this ILogger logger, Guid userId, Guid passkeyId, long stored, long received);

    [LoggerMessage(EventId = 17008, Level = LogLevel.Information, Message = "Passkey {PasskeyId} of User {UserId} was removed by an administrator. All Sessions and tokens of the User were revoked.")]
    public static partial void PasskeyRemovedByAdministrator(this ILogger logger, Guid userId, Guid passkeyId);

    [LoggerMessage(EventId = 17009, Level = LogLevel.Information, Message = "User {UserId} must complete a second factor in Session {SessionId} before changing their passkeys.")]
    public static partial void PasskeyChangeNeedsStepUp(this ILogger logger, Guid userId, Guid sessionId);
}
