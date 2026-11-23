// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.BackchannelLogout;

/// <summary>
///     Log messages of the delivery of back-channel logout notifications. Ending the Sessions logs 18001 (Core).
/// </summary>
internal static partial class BackchannelLogoutDeliveryLog
{
    [LoggerMessage(EventId = 18011, Level = LogLevel.Information, Message = "Sent the Logout Token of notification {NotificationId} for User {UserId} to Client {ClientId}.")]
    public static partial void LogoutTokenDelivered(this ILogger logger, Guid notificationId, Guid userId, Guid clientId);

    [LoggerMessage(EventId = 18012, Level = LogLevel.Warning, Message = "Sending the Logout Token of notification {NotificationId} to Client {ClientId} failed in attempt {Attempt}: {Error}. Retried at {NextAttemptAt:O}.")]
    public static partial void LogoutTokenRetryScheduled(this ILogger logger, Guid notificationId, Guid clientId, int attempt, string error, DateTime nextAttemptAt);

    [LoggerMessage(EventId = 18013, Level = LogLevel.Warning, Message = "Gave up sending the Logout Token of notification {NotificationId} for User {UserId} to Client {ClientId} after {Attempts} attempts: {Error}. The Client may still consider the User signed in.")]
    public static partial void LogoutTokenGivenUp(this ILogger logger, Guid notificationId, Guid userId, Guid clientId, int attempts, string error);

    [LoggerMessage(EventId = 18014, Level = LogLevel.Information, Message = "Dropped notification {NotificationId}, because Client {ClientId} no longer has a Back-Channel Logout URI.")]
    public static partial void LogoutTokenDropped(this ILogger logger, Guid notificationId, Guid clientId);

    [LoggerMessage(EventId = 18015, Level = LogLevel.Error, Message = "Sending back-channel logout notifications failed. They are retried with the next run.")]
    public static partial void DeliveryFailed(this ILogger logger, Exception exception);
}
