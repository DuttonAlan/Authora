// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Email;

/// <summary>
///     Log messages of the one-time tokens of email links. Tokens and Emails are never logged.
/// </summary>
internal static partial class EmailTokenLog
{
    [LoggerMessage(EventId = 14011, Level = LogLevel.Information, Message = "Issued email token {EmailTokenId} for {Purpose} to User {UserId}, valid until {ExpiresAt:O}.")]
    public static partial void EmailTokenIssued(this ILogger logger, Guid emailTokenId, EmailTokenPurpose purpose, Guid userId, DateTime expiresAt);

    [LoggerMessage(EventId = 14012, Level = LogLevel.Information, Message = "Email token {EmailTokenId} for {Purpose} of User {UserId} was used.")]
    public static partial void EmailTokenUsed(this ILogger logger, Guid emailTokenId, EmailTokenPurpose purpose, Guid userId);

    [LoggerMessage(EventId = 14013, Level = LogLevel.Warning, Message = "An unknown email token was presented for {Purpose}.")]
    public static partial void EmailTokenUnknown(this ILogger logger, EmailTokenPurpose purpose);

    [LoggerMessage(EventId = 14014, Level = LogLevel.Warning, Message = "Email token {EmailTokenId} of User {UserId} was rejected for {Purpose}: {Reason}.")]
    public static partial void EmailTokenRejected(this ILogger logger, Guid emailTokenId, Guid userId, EmailTokenPurpose purpose, string reason);
}
