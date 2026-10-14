// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Authora.Infrastructure.Seeding;

/// <summary>
///     Log messages of the seeding at startup. Passwords are never logged.
/// </summary>
internal static partial class SeedLog
{
    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Seeded {Count} new Permissions.")]
    public static partial void PermissionsSeeded(this ILogger logger, int count);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Information, Message = "Created the administrator Role {RoleId}.")]
    public static partial void AdministratorRoleCreated(this ILogger logger, Guid roleId);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information, Message = "Created the System Scope {ScopeId} ({ScopeName}).")]
    public static partial void SystemScopeCreated(this ILogger logger, Guid scopeId, string scopeName);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Warning, Message = "Created the first administrator, User {UserId}. Sign in and set a new password, then remove the bootstrap password from the configuration.")]
    public static partial void AdministratorBootstrapped(this ILogger logger, Guid userId);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Warning, Message = "Created the Public Client {ClientId} ({ClientName}) for the admin API.")]
    public static partial void AdminClientBootstrapped(this ILogger logger, Guid clientId, string clientName);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Warning, Message = "No administrator who can sign in exists, so nobody can use the admin API. Set 'Authora:Bootstrap:Administrator' to create one.")]
    public static partial void NoAdministrator(this ILogger logger);

    [LoggerMessage(EventId = 7007, Level = LogLevel.Warning, Message = "An administrator already exists, but a bootstrap password is still configured. Remove 'Authora:Bootstrap:Administrator:Password' from the configuration.")]
    public static partial void BootstrapPasswordNotRemoved(this ILogger logger);

    [LoggerMessage(EventId = 7008, Level = LogLevel.Information, Message = "Created the Public Client {ClientId} of the admin interface with the Redirect URI {RedirectUri}.")]
    public static partial void AdminUiClientCreated(this ILogger logger, Guid clientId, string redirectUri);

    [LoggerMessage(EventId = 7009, Level = LogLevel.Warning, Message = "The Client of the admin interface was not created, because its Redirect URI {RedirectUri} is invalid: {Reason} The admin interface cannot be used.")]
    public static partial void AdminUiClientNotCreated(this ILogger logger, string redirectUri, string reason);
}
