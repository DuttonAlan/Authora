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
}
