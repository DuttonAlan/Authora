// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Auditing;

/// <summary>
///     Log messages of the sealing of audit events. The verification in Core uses the same area (110xx).
/// </summary>
internal static partial class AuditSealLog
{
    [LoggerMessage(EventId = 11001, Level = LogLevel.Information, Message = "Sealed {Count} audit events. Checkpoint: the audit chain ends at Sequence {Sequence} with hash {Hash}.")]
    public static partial void AuditEventsSealed(this ILogger logger, int count, long sequence, string hash);

    [LoggerMessage(EventId = 11002, Level = LogLevel.Error, Message = "Sealing the audit events failed. It is retried with the next run.")]
    public static partial void SealingFailed(this ILogger logger, Exception exception);
}
