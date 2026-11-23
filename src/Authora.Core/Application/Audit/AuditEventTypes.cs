// Copyright(c) Alan Dutton.
// Licensed under the MIT License.
// See the LICENSE file in the project root for license information.

namespace Authora.Core.Application.Audit;

/// <summary>
///     The Types of all audit events. The part before the dot is the area, so events can be searched by
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
        ///     The password of a User was correct, but the User enrolled a second factor, so the sign-in waits for it.
        ///     Also recorded when a signed-in User starts a step-up of their Session. The Details name the pending
        ///     sign-in. A wrong second factor is recorded as a failed <see cref="SignIn"/> or <see cref="StepUp"/>.
        /// </summary>
        public const string SignInPending = "account.sign-in-pending";

        /// <summary>
        ///     A signed-in User completed an additional factor for their Session (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>;
        ///     the Session got a new token), or the step-up failed (<see cref="Domain.Enums.AuditOutcome.Failed"/>,
        ///     with the reason in the Details).
        /// </summary>
        public const string StepUp = "account.step-up";

        /// <summary>
        ///     A User was locked out after too many failed sign-ins.
        /// </summary>
        public const string LockedOut = "account.locked-out";

        /// <summary>
        ///     A User signed out.
        /// </summary>
        public const string SignedOut = "account.signed-out";

        /// <summary>
        ///     A User signed out at the request of a Client (RP-initiated logout). The Details name the Client and
        ///     whether the User confirmed it or an <c>id_token_hint</c> of the Session was sent.
        /// </summary>
        public const string SignedOutByClient = "account.signed-out-by-client";

        /// <summary>
        ///     A User changed their own password (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>; all other Sessions
        ///     and all tokens of the User were revoked), or the change failed
        ///     (<see cref="Domain.Enums.AuditOutcome.Failed"/>, with the reason in the Details).
        /// </summary>
        public const string PasswordChanged = "account.password-changed";

        /// <summary>
        ///     Someone asked for a password reset link on the sign-in page. Succeeded: a link was sent to the User.
        ///     Failed: no link was sent, with the reason in the Details, e.g. an unknown address (then without a
        ///     Target; the address is not recorded) or too many links for the address.
        /// </summary>
        public const string PasswordResetRequested = "account.password-reset-requested";

        /// <summary>
        ///     A User set a new password with a reset link (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>; all
        ///     Sessions and tokens of the User were revoked and a lockout ended), or the link was not accepted
        ///     (<see cref="Domain.Enums.AuditOutcome.Failed"/>, with the reason in the Details).
        /// </summary>
        public const string PasswordReset = "account.password-reset";

        /// <summary>
        ///     A User confirmed their Email with a verification link (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>),
        ///     or the link was not accepted (<see cref="Domain.Enums.AuditOutcome.Failed"/>, with the reason in the
        ///     Details).
        /// </summary>
        public const string EmailVerified = "account.email-verified";
    }

    /// <summary>
    ///     Passkeys (WebAuthn) of a User as a second factor. The Target is the User; the Details name the passkey by its
    ///     Id. A removal by an administrator is <see cref="Users.PasskeyRemoved"/>, a reset <see cref="Users.MfaReset"/>.
    /// </summary>
    public static class Passkeys
    {
        /// <summary>
        ///     A User registered a passkey. From now on they can use it after the password.
        /// </summary>
        public const string Registered = "passkey.registered";

        /// <summary>
        ///     A User renamed a passkey.
        /// </summary>
        public const string Renamed = "passkey.renamed";

        /// <summary>
        ///     A User removed a passkey; all other Sessions and all tokens of the User were revoked.
        /// </summary>
        public const string Removed = "passkey.removed";

        /// <summary>
        ///     A passkey was accepted for the second step of a sign-in or a step-up; the sign-in records its own outcome as
        ///     well.
        /// </summary>
        public const string Used = "passkey.used";

        /// <summary>
        ///     A passkey response was rejected, with the reason in the Details, e.g. an expired challenge, a wrong
        ///     signature, or a signature counter that did not increase (a sign of a cloned authenticator).
        /// </summary>
        public const string Failed = "passkey.failed";
    }

    /// <summary>
    ///     Multi-factor authentication of a User with an authenticator app and recovery codes. The Target is the User.
    ///     A reset by an administrator is <see cref="Users.MfaReset"/>.
    /// </summary>
    public static class Mfa
    {
        /// <summary>
        ///     A User set up an authenticator app and got a first set of recovery codes. From now on they enter a code
        ///     after the password.
        /// </summary>
        public const string Enrolled = "mfa.enrolled";

        /// <summary>
        ///     A User removed their authenticator app and recovery codes (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>;
        ///     all other Sessions and all tokens of the User were revoked), or the removal failed
        ///     (<see cref="Domain.Enums.AuditOutcome.Failed"/>, with the reason in the Details).
        /// </summary>
        public const string Removed = "mfa.removed";

        /// <summary>
        ///     A code of the authenticator app was accepted (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>), or a code
        ///     or recovery code was rejected (<see cref="Domain.Enums.AuditOutcome.Failed"/>, with the reason in the
        ///     Details, e.g. a code that was already used). Recorded for the second step of a sign-in, a step-up and the
        ///     removal; the sign-in records its own outcome as well.
        /// </summary>
        public const string Verified = "mfa.verified";

        /// <summary>
        ///     A User used a recovery code instead of the authenticator app. The Details say how many are left.
        /// </summary>
        public const string RecoveryCodeUsed = "mfa.recovery-code-used";

        /// <summary>
        ///     A User generated a new set of recovery codes; the previous codes no longer work.
        /// </summary>
        public const string RecoveryCodesRegenerated = "mfa.recovery-codes-regenerated";
    }

    /// <summary>
    ///     Consents of Users for third-party Clients. The Target is the User; the Details name the Client of the
    ///     Consent. A granted Consent has that Client as the Client of the event; a revoked one has the Client the
    ///     revocation came through (none for the account page, e.g. the admin interface for an administrator).
    /// </summary>
    public static class Consents
    {
        /// <summary>
        ///     A User approved Scopes for a Client on the consent screen that their Consent did not contain yet: either
        ///     the first approval, or a request for further Scopes. The Details list the added Scopes.
        /// </summary>
        public const string Granted = "consent.granted";

        /// <summary>
        ///     The Consent of a User for a Client was revoked, by the User on the account page or by an administrator,
        ///     and the Grants of the User for the Client were revoked with it.
        /// </summary>
        public const string Revoked = "consent.revoked";
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

        /// <summary>
        ///     An administrator sent a User a password reset link (<see cref="Domain.Enums.AuditOutcome.Succeeded"/>
        ///     once the mail server accepted it), or the email could not be sent (<see cref="Domain.Enums.AuditOutcome.Failed"/>).
        /// </summary>
        public const string PasswordResetSent = "user.password-reset-sent";

        /// <summary>
        ///     A verification link was sent to the Email of a User. Sent by an administrator: Succeeded once the mail
        ///     server accepted it, Failed if it could not be sent. Sent by itself after the User was created or their Email
        ///     was changed: Succeeded once it was queued (the Details say so), Failed if the queue was full; delivery
        ///     failures are then only logged.
        /// </summary>
        public const string EmailVerificationSent = "user.email-verification-sent";

        /// <summary>An administrator marked the Email of a User as verified.</summary>
        public const string EmailMarkedVerified = "user.email-marked-verified";

        /// <summary>
        ///     An administrator reset the multi-factor authentication of a User: the authenticator app, the recovery
        ///     codes and the passkeys were removed, and all Sessions and tokens of the User were revoked.
        /// </summary>
        public const string MfaReset = "user.mfa-reset";

        /// <summary>
        ///     An administrator removed a passkey of a User; all Sessions and tokens of the User were revoked. The Details
        ///     name the passkey.
        /// </summary>
        public const string PasskeyRemoved = "user.passkey-removed";

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

        /// <summary>A Post-logout Redirect URI was added to a Client.</summary>
        public const string PostLogoutRedirectUriAdded = "client.post-logout-redirect-uri-added";

        /// <summary>A Post-logout Redirect URI was removed from a Client.</summary>
        public const string PostLogoutRedirectUriRemoved = "client.post-logout-redirect-uri-removed";

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

        /// <summary>A Confidential Client may now send authorization requests without PKCE.</summary>
        public const string PkceMadeOptional = "client.pkce-made-optional";

        /// <summary>A Client must send PKCE again.</summary>
        public const string PkceRequired = "client.pkce-required";

        /// <summary>Users of a Client must now sign in with multi-factor authentication.</summary>
        public const string MfaRequired = "client.mfa-required";

        /// <summary>Users of a Client no longer need multi-factor authentication.</summary>
        public const string MfaNoLongerRequired = "client.mfa-no-longer-required";

        /// <summary>
        ///     A Client obtained an Access Token for itself with <c>grant_type=client_credentials</c>. The Details list
        ///     the Grant, the Scopes and the expiry, never the token.
        /// </summary>
        public const string TokenIssued = "client.token-issued";
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
        ///     The signing key or the published keys changed. The Target is the new signing key, the Details
        ///     list all published keys.
        /// </summary>
        public const string SigningKeysChanged = "system.signing-keys-changed";

        /// <summary>
        ///     The cleanup deletes the start of the audit chain after the retention. The Target is the
        ///     Sequence the chain starts at afterwards; a chain that starts later without such an event is reported as
        ///     broken by the verification.
        /// </summary>
        public const string AuditChainTruncated = "system.audit-chain-truncated";
    }
}
