// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Authorization;

/// <summary>
///     The Names of all Permissions that Authora checks.
/// </summary>
/// <remarks>
///     This is the single list used by the seeder and the authorization policies. A Permission only has an effect
///     when an endpoint checks it, so Permissions are defined here instead of through the API.
/// </remarks>
public static class PermissionNames
{
    /// <summary>
    ///     Permissions to manage Users.
    /// </summary>
    public static class Users
    {
        /// <summary>
        ///     Read Users, their Roles and their effective Permissions.
        /// </summary>
        public const string Read = "users.read";

        /// <summary>
        ///     Create Users.
        /// </summary>
        public const string Create = "users.create";

        /// <summary>
        ///     Change the Email and Name of Users.
        /// </summary>
        public const string Update = "users.update";

        /// <summary>
        ///     Delete Users.
        /// </summary>
        public const string Delete = "users.delete";

        /// <summary>
        ///     Activate, deactivate and unlock Users, and revoke their Sessions.
        /// </summary>
        public const string ManageAccess = "users.manage-access";

        /// <summary>
        ///     Set the password of Users.
        /// </summary>
        public const string SetPassword = "users.set-password";

        /// <summary>
        ///     Assign Roles to Users and remove them. Whoever has this Permission can give themselves any Role.
        /// </summary>
        public const string AssignRoles = "users.assign-roles";
    }

    /// <summary>
    ///     Permissions to manage Roles.
    /// </summary>
    public static class Roles
    {
        /// <summary>
        ///     Read Roles and their Permissions.
        /// </summary>
        public const string Read = "roles.read";

        /// <summary>
        ///     Create Roles.
        /// </summary>
        public const string Create = "roles.create";

        /// <summary>
        ///     Change Roles.
        /// </summary>
        public const string Update = "roles.update";

        /// <summary>
        ///     Delete Roles.
        /// </summary>
        public const string Delete = "roles.delete";

        /// <summary>
        ///     Grant Permissions to Roles and revoke them. Whoever has this Permission can give their own Roles any
        ///     Permission.
        /// </summary>
        public const string GrantPermissions = "roles.grant-permissions";
    }

    /// <summary>
    ///     Permissions to read Permissions.
    /// </summary>
    public static class Permissions
    {
        /// <summary>
        ///     Read all Permissions.
        /// </summary>
        public const string Read = "permissions.read";
    }

    /// <summary>
    ///     Permissions to manage Scopes.
    /// </summary>
    public static class Scopes
    {
        /// <summary>
        ///     Read Scopes.
        /// </summary>
        public const string Read = "scopes.read";

        /// <summary>
        ///     Create Scopes.
        /// </summary>
        public const string Create = "scopes.create";

        /// <summary>
        ///     Change Scopes.
        /// </summary>
        public const string Update = "scopes.update";

        /// <summary>
        ///     Delete Scopes.
        /// </summary>
        public const string Delete = "scopes.delete";
    }

    /// <summary>
    ///     Permissions to manage Clients.
    /// </summary>
    public static class Clients
    {
        /// <summary>
        ///     Read Clients, including the metadata of their Secrets.
        /// </summary>
        public const string Read = "clients.read";

        /// <summary>
        ///     Register Clients.
        /// </summary>
        public const string Create = "clients.create";

        /// <summary>
        ///     Change Clients, their Redirect URIs, allowed Scopes and served Scopes, and enable or disable them.
        /// </summary>
        public const string Update = "clients.update";

        /// <summary>
        ///     Delete Clients.
        /// </summary>
        public const string Delete = "clients.delete";

        /// <summary>
        ///     Create and revoke Client Secrets.
        /// </summary>
        public const string ManageSecrets = "clients.manage-secrets";
    }

    /// <summary>
    ///     Permissions to monitor Authora.
    /// </summary>
    public static class Health
    {
        /// <summary>
        ///     Read the detailed health report at <c>/health/details</c>, including the database schema version.
        /// </summary>
        public const string Read = "health.read";
    }

    /// <summary>
    ///     All Permission Names.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Users.Read,
        Users.Create,
        Users.Update,
        Users.Delete,
        Users.ManageAccess,
        Users.SetPassword,
        Users.AssignRoles,
        Roles.Read,
        Roles.Create,
        Roles.Update,
        Roles.Delete,
        Roles.GrantPermissions,
        Permissions.Read,
        Scopes.Read,
        Scopes.Create,
        Scopes.Update,
        Scopes.Delete,
        Clients.Read,
        Clients.Create,
        Clients.Update,
        Clients.Delete,
        Clients.ManageSecrets,
        Health.Read
    ];
}
