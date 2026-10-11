// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Core.Application.Roles;

/// <summary>
///     Log messages for changes to Roles and their Permissions.
/// </summary>
internal static partial class RoleLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Role {RoleId} ({RoleName}) was created.")]
    public static partial void RoleCreated(this ILogger logger, Guid roleId, string roleName);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Role {RoleId} ({RoleName}) was updated.")]
    public static partial void RoleUpdated(this ILogger logger, Guid roleId, string roleName);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Role {RoleId} ({RoleName}) was deleted and removed from {UserCount} Users.")]
    public static partial void RoleDeleted(this ILogger logger, Guid roleId, string roleName, int userCount);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Permission {PermissionId} ({PermissionName}) was granted to Role {RoleId} ({RoleName}).")]
    public static partial void PermissionGranted(this ILogger logger, Guid roleId, string roleName, Guid permissionId, string permissionName);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Permission {PermissionId} ({PermissionName}) was revoked from Role {RoleId} ({RoleName}).")]
    public static partial void PermissionRevoked(this ILogger logger, Guid roleId, string roleName, Guid permissionId, string permissionName);
}
