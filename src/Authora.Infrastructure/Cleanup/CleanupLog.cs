// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Cleanup;

/// <summary>
///     Log messages of the cleanup of expired data.
/// </summary>
internal static partial class CleanupLog
{
    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Deleted {AuthorizationCodes} Authorization Codes, {AccessTokens} Access Tokens, {RefreshTokens} Refresh Tokens, {Grants} Grants and {Sessions} Sessions that expired or were revoked before {Cutoff:O}.")]
    public static partial void ExpiredDataDeleted(
        this ILogger logger,
        int authorizationCodes,
        int accessTokens,
        int refreshTokens,
        int grants,
        int sessions,
        DateTime cutoff);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Information, Message = "Deleted {AuditEvents} audit events that occurred before {Cutoff:O}.")]
    public static partial void AuditEventsDeleted(this ILogger logger, int auditEvents, DateTime cutoff);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Error, Message = "The cleanup of expired data failed. It is retried with the next run.")]
    public static partial void CleanupFailed(this ILogger logger, Exception exception);
}
