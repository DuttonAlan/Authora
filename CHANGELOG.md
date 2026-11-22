# Changelog

All notable changes to Authora are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Authora uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Before 1.0, minor versions may contain breaking changes; they are listed under *Changed* or *Removed*.

## [Unreleased]

### Upgrading

- Stop all instances before applying the three new migrations `TwoStepSignIn`, `SessionTokenConcurrency` and `MultiFactorAuthentication`.
- The Data Protection keys now also encrypt the secrets of authenticator apps: persist them (`Authora:DataProtection:KeysPath`) and include them in your backups, or every authenticator app stops working when they are lost. See [Persist Data Protection keys](docs/how-to/persist-data-protection-keys.md).
- Sessions started before the upgrade end; Users sign in again.

### Added

- Two-step sign-in: a User who has enrolled a second factor gets no Session for the password alone, only a pending sign-in (5 minutes, single use, ended by a change of the User's security stamp), completed at `/account/second-factor`. Wrong second factors count towards the lockout.
- Sessions record how the User authenticated (RFC 8176 methods, `pwd` and `otp`) and when they last did; both are carried by Authorization Codes, so `auth_time` reflects the latest authentication.
- Step-up at `/account/step-up`: a signed-in User completes an additional factor for the current Session, which then gets a new Session token.
- *Account and security* overview at `/account/security`, linked from the account pages.
- Settings `Authora:SignIn:PendingSignInLifetime` and `Authora:SignIn:RecentAuthenticationWindow`, audit events `account.sign-in-pending` and `account.step-up`, log events 5019 – 5023 and 8005.
- Multi-factor authentication with an authenticator app (TOTP, RFC 6238) and recovery codes. Users set it up at `/account/security/authenticator` with a QR code drawn on the server, confirmed by a first code; the sign-in then asks for a code after the password, and the Session records `otp`. A code works once (replayed codes are rejected), and wrong codes count towards the lockout. 10 single-use recovery codes, stored only as hashes, stand in for a lost phone; Users see how many are left and generate new ones at `/account/security/recovery-codes`, after confirming with the app if they signed in without it. Removing the app at `/account/security/authenticator/remove` needs a current code or recovery code and ends all other Sessions. See [Require multi-factor authentication](docs/how-to/require-multi-factor-authentication.md).
- Admin API and interface: `GET /api/users/{id}/mfa` shows whether a User has an authenticator app, when it was last used and how many recovery codes are left; `DELETE /api/users/{id}/mfa` resets it and ends all Sessions and Grants of the User, with the new Permission `users.reset-mfa`. Only administrators can reset an administrator.
- Setting `Authora:Mfa:IssuerName`, audit events `mfa.enrolled`, `mfa.removed`, `mfa.verified`, `mfa.recovery-code-used`, `mfa.recovery-codes-regenerated` and `user.mfa-reset`, log events 1014 and 16001 – 16010.

### Changed

- Deleting a User also deletes their authenticator app and recovery codes.

## [0.2.0] - 2026-11-21

### Upgrading

- Stop all instances before applying the nine new migrations `OptionalPkce`, `PostLogoutRedirectUris`, `ClientCredentialsGrants`, `BindAuthorizationCodeToSession`, `EmailTokens`, `RememberedConsent`, `BindAuthorizationCodeToConsent`, `BindEmailTokensToSecurityStamp` and `AddUserEmailVerification` (see [Upgrade Authora](docs/how-to/upgrade-authora.md)).
- Authorization Codes issued before the upgrade can no longer be exchanged.
- Existing Users start with an unverified email address.
- `/oauth/introspect` is now rate limited. Resource servers that introspect every request may need a cache or a higher `Authora:RateLimiting:TokenPermitsPerMinute`.
- The production checks at startup now apply in every environment except `Development`, also in `Testing`.

New endpoints, settings, audit events and log events are listed in the [OAuth endpoints](docs/reference/oauth-endpoints.md), [admin API](docs/reference/admin-api.md), [configuration](docs/reference/configuration.md), [audit events](docs/reference/audit-events.md) and [log events](docs/reference/log-events.md) references.

### Added

- RP-initiated logout ([OpenID Connect RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html)) at `/oauth/logout`, with Post-logout Redirect URIs registered per Client.
- Client Credentials grant for machine-to-machine access, opt-in per Confidential Client.
- Email delivery through SMTP (`Authora:Email`), off until `Authora:Email:Host` is set. Docker Compose includes the mail catcher Mailpit at <http://localhost:8025>.
- Self-service password change at `/account/password`. It revokes the User's other Sessions and all of their Grants.
- Self-service password reset by email at `/account/forgot-password`, without revealing whether an account exists. Administrators can also send reset links. A reset revokes all Sessions and tokens of the User.
- Email verification: `email_verified` now reflects whether the User confirmed their address with the link sent when the User is created or the Email changes. Administrators can send a new link or mark an address as verified.
- Remembered consent for third-party Clients: Users are asked again only for Scopes they have not approved, and see and withdraw their Consents at `/account/consents`. Withdrawing a Consent revokes the Client's tokens for that User.
- PKCE can be made optional for a single Confidential Client, for OpenID Connect libraries that do not send it. It stays required by default, for Public Clients and for first-party Clients.
- Authorization requests sent as a form with `POST /oauth/authorize`.
- Admin interface: verify the audit log's hash chain.
- The OpenID Foundation conformance suite's Basic OP and RP-Initiated Logout OP test plans run on every pull request (see [Run the conformance tests](docs/how-to/run-the-conformance-tests.md)).
- Documentation website at <https://authora.alandutton.de/>.

### Changed

- Redesigned admin interface: grouped sidebar, a start page with recent failures and activity, the audit log drawn as its hash chain, confirmation dialogs for destructive actions, keyboard-reachable table rows, and light and dark appearance. It shows the reason of a `403` from the API.
- Redesigned website homepage.
- An `id_token_hint` must have the header `typ: JWT`, as all ID Tokens Authora issues do.

### Security

- Delegated admin Permissions could be turned into administrator rights, e.g. by assigning `admin` to oneself or setting an administrator's password. Now nobody grants more than they hold, and only administrators act on administrators; violations return `403` and are audited (see [Admin API](docs/reference/admin-api.md#nobody-grants-more-than-they-hold)). `clients.manage-first-party` is now documented as equivalent to `admin`.
- `/oauth/introspect` had no rate limit, so Client Secrets could be guessed there. It now shares the limit of `/oauth/token` and `/oauth/revoke`.
- An Authorization Code issued just before the User signed out could still be exchanged. Codes are now bound to their Session.
- Startup in an environment named `Testing` skipped the production checks, so insecure settings were accepted.

### Fixed

- Website: diagrams failed to render or clipped their labels.

## [0.1.0] - 2026-11-02

First public version.

### Added

- OAuth 2.1 authorization server: Authorization Code grant with mandatory PKCE (`S256`), rotating Refresh Tokens with reuse detection, revocation (RFC 7009), introspection (RFC 7662), `iss` in authorization responses (RFC 9207).
- OpenID Connect provider: discovery, ID Tokens (`RS256`), JWKS, UserInfo, Scopes `openid`, `profile` and `email`, `nonce`, `prompt` and `max_age`.
- Signing keys from PEM files, reloaded without a restart for key rotation.
- Password sign-in with Sessions, lockout and rate limits; consent screen for third-party Clients; first-party Clients.
- Users, Groups, Roles and Permissions for administering Authora; Scopes and Clients with Redirect URIs, allowed and served Scopes, and expiring Secrets.
- Admin API and admin interface with a backend for frontend that keeps tokens out of the browser.
- Audit log with a tamper-evident hash chain and verification endpoint.
- Startup checks that refuse insecure production settings; security headers; health checks; cleanup of expired data.
- Container images for the server and the database migrations, and a Docker Compose setup for local evaluation.
- Documentation following Diátaxis: tutorials, how-to guides, reference and explanation.

[Unreleased]: https://github.com/DuttonAlan/Authora/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/DuttonAlan/Authora/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/DuttonAlan/Authora/releases/tag/v0.1.0
