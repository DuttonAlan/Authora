// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.BackchannelLogout;

/// <summary>
///     Log messages of ending Sessions explicitly. The delivery of the notifications logs in the same range, from 18011
///     on (Infrastructure).
/// </summary>
internal static partial class BackchannelLogoutLog
{
    [LoggerMessage(EventId = 18001, Level = LogLevel.Information, Message = "Revoked {Sessions} Sessions of User {UserId} and {Grants} Grants approved in them; queued {Notifications} back-channel logout notifications.")]
    public static partial void SessionsEnded(this ILogger logger, Guid userId, int sessions, int grants, int notifications);
}
