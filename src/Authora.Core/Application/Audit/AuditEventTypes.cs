// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Audit;

/// <summary>
///     The Types of all audit events (US-034). The part before the dot is the area, so events can be searched by
///     prefix, e.g. <c>user.</c>.
/// </summary>
public static class AuditEventTypes
{
    /// <summary>
    ///     Sign-in and sign-out.
    /// </summary>
    public static class Account
    {
        /// <summary>
        ///     A User signed in (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>), or a sign-in failed
        ///     (<see cref="Domain.Enums.AuditOutcome.Failed"/>, with the reason in the Details).
        /// </summary>
        public const string SignIn = "account.sign-in";

        /// <summary>
        ///     A User was locked out after too many failed sign-ins.
        /// </summary>
        public const string LockedOut = "account.locked-out";

        /// <summary>
        ///     A User signed out.
        /// </summary>
        public const string SignedOut = "account.signed-out";
    }

    /// <summary>
    ///     Events of the OAuth endpoints that indicate an attack.
    /// </summary>
    public static class OAuth
    {
        /// <summary>
        ///     A consumed Authorization Code was used again. The Grants issued from it were revoked.
        /// </summary>
        public const string AuthorizationCodeReused = "oauth.authorization-code-reused";

        /// <summary>
        ///     A consumed Refresh Token was used again. Its Grant was revoked.
        /// </summary>
        public const string RefreshTokenReused = "oauth.refresh-token-reused";
    }

    /// <summary>
    ///     Changes to Users.
    /// </summary>
    public static class Users
    {
        /// <summary>A User was created.</summary>
        public const string Created = "user.created";

        /// <summary>The Email or Name of a User was changed.</summary>
        public const string Updated = "user.updated";

        /// <summary>A User was deleted.</summary>
        public const string Deleted = "user.deleted";

        /// <summary>A User was deactivated.</summary>
        public const string Deactivated = "user.deactivated";

        /// <summary>A User was activated.</summary>
        public const string Activated = "user.activated";

        /// <summary>The lockout of a User was ended.</summary>
        public const string Unlocked = "user.unlocked";

        /// <summary>All Sessions and tokens of a User were revoked.</summary>
        public const string SessionsRevoked = "user.sessions-revoked";

        /// <summary>The password of a User was set by an administrator.</summary>
        public const string PasswordSet = "user.password-set";

        /// <summary>A Role was assigned to a User.</summary>
        public const string RoleAssigned = "user.role-assigned";

        /// <summary>A Role was removed from a User.</summary>
        public const string RoleRemoved = "user.role-removed";
    }

    /// <summary>
    ///     Changes to Roles.
    /// </summary>
    public static class Roles
    {
        /// <summary>A Role was created.</summary>
        public const string Created = "role.created";

        /// <summary>A Role was changed.</summary>
        public const string Updated = "role.updated";

        /// <summary>A Role was deleted.</summary>
        public const string Deleted = "role.deleted";

        /// <summary>A Permission was granted to a Role.</summary>
        public const string PermissionGranted = "role.permission-granted";

        /// <summary>A Permission was revoked from a Role.</summary>
        public const string PermissionRevoked = "role.permission-revoked";
    }

    /// <summary>
    ///     Changes to Groups.
    /// </summary>
    public static class Groups
    {
        /// <summary>A Group was created.</summary>
        public const string Created = "group.created";

        /// <summary>A Group was changed.</summary>
        public const string Updated = "group.updated";

        /// <summary>A Group was deleted.</summary>
        public const string Deleted = "group.deleted";

        /// <summary>A User was added to a Group.</summary>
        public const string MemberAdded = "group.member-added";

        /// <summary>A User was removed from a Group.</summary>
        public const string MemberRemoved = "group.member-removed";

        /// <summary>A Role was assigned to a Group.</summary>
        public const string RoleAssigned = "group.role-assigned";

        /// <summary>A Role was removed from a Group.</summary>
        public const string RoleRemoved = "group.role-removed";
    }

    /// <summary>
    ///     Changes to Scopes.
    /// </summary>
    public static class Scopes
    {
        /// <summary>A Scope was created.</summary>
        public const string Created = "scope.created";

        /// <summary>A Scope was changed.</summary>
        public const string Updated = "scope.updated";

        /// <summary>A Scope was deleted.</summary>
        public const string Deleted = "scope.deleted";
    }

    /// <summary>
    ///     Changes to Clients.
    /// </summary>
    public static class Clients
    {
        /// <summary>A Client was registered.</summary>
        public const string Registered = "client.registered";

        /// <summary>A Client was changed.</summary>
        public const string Updated = "client.updated";

        /// <summary>A Client was deleted.</summary>
        public const string Deleted = "client.deleted";

        /// <summary>A Client was enabled.</summary>
        public const string Enabled = "client.enabled";

        /// <summary>A Client was disabled and its Grants were revoked.</summary>
        public const string Disabled = "client.disabled";

        /// <summary>A Redirect URI was added to a Client.</summary>
        public const string RedirectUriAdded = "client.redirect-uri-added";

        /// <summary>A Redirect URI was removed from a Client.</summary>
        public const string RedirectUriRemoved = "client.redirect-uri-removed";

        /// <summary>A Client was allowed to request a Scope.</summary>
        public const string ScopeAllowed = "client.scope-allowed";

        /// <summary>A Client was no longer allowed to request a Scope.</summary>
        public const string ScopeDisallowed = "client.scope-disallowed";

        /// <summary>A Client started to serve a Scope as a resource server.</summary>
        public const string ScopeServed = "client.scope-served";

        /// <summary>A Client stopped serving a Scope.</summary>
        public const string ScopeNoLongerServed = "client.scope-no-longer-served";

        /// <summary>A Client Secret was created.</summary>
        public const string SecretCreated = "client.secret-created";

        /// <summary>A Client Secret was revoked.</summary>
        public const string SecretRevoked = "client.secret-revoked";

        /// <summary>A Client was marked as first-party, so Users are no longer asked for consent.</summary>
        public const string MarkedFirstParty = "client.marked-first-party";

        /// <summary>A Client was marked as third-party again, so Users are asked for consent.</summary>
        public const string MarkedThirdParty = "client.marked-third-party";
    }

    /// <summary>
    ///     Events of Authora itself.
    /// </summary>
    public static class System
    {
        /// <summary>The first administrator was created from the configuration.</summary>
        public const string AdministratorBootstrapped = "system.administrator-bootstrapped";

        /// <summary>A Client was created from the configuration, for the admin API or the admin interface.</summary>
        public const string ClientBootstrapped = "system.client-bootstrapped";

        /// <summary>
        ///     The signing key or the published keys changed (US-035). The Target is the new signing key, the Details
        ///     list all published keys.
        /// </summary>
        public const string SigningKeysChanged = "system.signing-keys-changed";
    }
}
