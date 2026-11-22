// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Users;

/// <summary>
///     Log messages for security-relevant changes to Users.
/// </summary>
internal static partial class UserLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "User {UserId} was deleted.")]
    public static partial void UserDeleted(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "User {UserId} was deactivated.")]
    public static partial void UserDeactivated(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "User {UserId} was activated.")]
    public static partial void UserActivated(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "User {UserId} was unlocked.")]
    public static partial void UserUnlocked(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "All sessions of User {UserId} were revoked.")]
    public static partial void UserSessionsRevoked(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Role {RoleId} ({RoleName}) was assigned to User {UserId}.")]
    public static partial void RoleAssigned(this ILogger logger, Guid userId, Guid roleId, string roleName);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "Role {RoleId} ({RoleName}) was removed from User {UserId}.")]
    public static partial void RoleRemoved(this ILogger logger, Guid userId, Guid roleId, string roleName);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "The password of User {UserId} was set. All Sessions and tokens of the User were revoked.")]
    public static partial void PasswordSet(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "A password reset link was sent to User {UserId} at the request of an administrator.")]
    public static partial void PasswordResetSent(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "An email verification link was sent to User {UserId} at the request of an administrator.")]
    public static partial void EmailVerificationSent(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "An email verification link for User {UserId} was queued, because the User was created or their Email changed.")]
    public static partial void EmailVerificationQueued(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Warning, Message = "No email verification link was sent to User {UserId}, because the email queue is full.")]
    public static partial void EmailVerificationNotQueued(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Information, Message = "The Email of User {UserId} was marked as verified by an administrator.")]
    public static partial void EmailMarkedVerified(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Information, Message = "The multi-factor authentication of User {UserId} was reset by an administrator. All Sessions and tokens of the User were revoked.")]
    public static partial void MfaReset(this ILogger logger, Guid userId);
}
