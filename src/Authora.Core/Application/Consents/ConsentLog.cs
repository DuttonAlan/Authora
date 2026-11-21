// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Consents;

/// <summary>
///     Log messages for granted and revoked Consents.
/// </summary>
internal static partial class ConsentLog
{
    [LoggerMessage(EventId = 15001, Level = LogLevel.Information, Message = "User {UserId} approved Client {ClientId} for the Scopes '{Scope}'.")]
    public static partial void ConsentGranted(this ILogger logger, Guid userId, Guid clientId, string scope);

    [LoggerMessage(EventId = 15002, Level = LogLevel.Information, Message = "The Consent of User {UserId} for Client {ClientId} was revoked. {RevokedGrants} Grant(s) were revoked with it.")]
    public static partial void ConsentRevoked(this ILogger logger, Guid userId, Guid clientId, int revokedGrants);
}
