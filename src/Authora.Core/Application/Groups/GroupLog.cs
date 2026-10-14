// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Groups;

/// <summary>
///     Log messages for changes to Groups, their Roles and their members.
/// </summary>
internal static partial class GroupLog
{
    [LoggerMessage(EventId = 10001, Level = LogLevel.Information, Message = "Group {GroupId} ({GroupName}) was created.")]
    public static partial void GroupCreated(this ILogger logger, Guid groupId, string groupName);

    [LoggerMessage(EventId = 10002, Level = LogLevel.Information, Message = "Group {GroupId} ({GroupName}) was updated.")]
    public static partial void GroupUpdated(this ILogger logger, Guid groupId, string groupName);

    [LoggerMessage(EventId = 10003, Level = LogLevel.Warning, Message = "Group {GroupId} ({GroupName}) was deleted and {MemberCount} memberships ended.")]
    public static partial void GroupDeleted(this ILogger logger, Guid groupId, string groupName, int memberCount);

    [LoggerMessage(EventId = 10004, Level = LogLevel.Information, Message = "Role {RoleId} ({RoleName}) was assigned to Group {GroupId} ({GroupName}).")]
    public static partial void GroupRoleAssigned(this ILogger logger, Guid groupId, string groupName, Guid roleId, string roleName);

    [LoggerMessage(EventId = 10005, Level = LogLevel.Information, Message = "Role {RoleId} ({RoleName}) was removed from Group {GroupId} ({GroupName}).")]
    public static partial void GroupRoleRemoved(this ILogger logger, Guid groupId, string groupName, Guid roleId, string roleName);

    [LoggerMessage(EventId = 10006, Level = LogLevel.Information, Message = "User {UserId} was added to Group {GroupId} ({GroupName}).")]
    public static partial void MemberAdded(this ILogger logger, Guid groupId, string groupName, Guid userId);

    [LoggerMessage(EventId = 10007, Level = LogLevel.Information, Message = "User {UserId} was removed from Group {GroupId} ({GroupName}).")]
    public static partial void MemberRemoved(this ILogger logger, Guid groupId, string groupName, Guid userId);
}
