// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Audit;

/// <summary>
///     Log messages of the verification of the audit chain. The sealing in Infrastructure uses the same area (110xx).
/// </summary>
internal static partial class AuditChainLog
{
    [LoggerMessage(EventId = 11011, Level = LogLevel.Critical, Message = "The audit chain is broken at Sequence {Sequence}: {Problem}")]
    public static partial void AuditChainBroken(this ILogger logger, long? sequence, string problem);
}
