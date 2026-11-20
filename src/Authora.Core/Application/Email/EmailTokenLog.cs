// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Authora.Core.Domain.Enums;

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Email;

/// <summary>
///     Log messages of the one-time tokens of email links. Tokens and Emails are never logged.
/// </summary>
/// <remarks>
///     The purpose is logged as a fixed name from <see cref="LogName"/>, not as the enum value: static analysis
///     (CodeQL) treats the names <c>PasswordReset</c> and <c>EmailVerification</c> as a password and an email address.
/// </remarks>
internal static partial class EmailTokenLog
{
    /// <summary>
    ///     The name of a purpose in log messages.
    /// </summary>
    /// <param name="purpose">The purpose.</param>
    /// <returns>A fixed name, e.g. <c>password-reset</c>.</returns>
    public static string LogName(this EmailTokenPurpose purpose) => purpose switch
    {
        EmailTokenPurpose.PasswordReset => "password-reset",
        EmailTokenPurpose.EmailVerification => "email-verification",
        _ => "unknown"
    };

    [LoggerMessage(EventId = 14011, Level = LogLevel.Information, Message = "Issued email token {TokenId} for {Purpose} to User {UserId}, valid until {ExpiresAt:O}.")]
    public static partial void EmailTokenIssued(this ILogger logger, Guid tokenId, string purpose, Guid userId, DateTime expiresAt);

    [LoggerMessage(EventId = 14012, Level = LogLevel.Information, Message = "Email token {TokenId} for {Purpose} of User {UserId} was used.")]
    public static partial void EmailTokenUsed(this ILogger logger, Guid tokenId, string purpose, Guid userId);

    [LoggerMessage(EventId = 14013, Level = LogLevel.Warning, Message = "An unknown email token was presented for {Purpose}.")]
    public static partial void EmailTokenUnknown(this ILogger logger, string purpose);

    [LoggerMessage(EventId = 14014, Level = LogLevel.Warning, Message = "Email token {TokenId} of User {UserId} was rejected for {Purpose}: {Reason}.")]
    public static partial void EmailTokenRejected(this ILogger logger, Guid tokenId, Guid userId, string purpose, string reason);
}
