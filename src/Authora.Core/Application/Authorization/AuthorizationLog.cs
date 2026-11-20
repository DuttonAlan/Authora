// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Authorization;

/// <summary>
///     Log messages for denied changes of the admin API.
/// </summary>
internal static partial class AuthorizationLog
{
    [LoggerMessage(EventId = 13001, Level = LogLevel.Warning, Message = "User {CallerId} was denied a change of {TargetType} {TargetId}: {Reason}")]
    public static partial void PrivilegeDenied(
        this ILogger logger,
        Guid? callerId,
        string? targetType,
        string? targetId,
        string reason);
}
