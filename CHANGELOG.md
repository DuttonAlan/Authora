# Changelog

All notable changes to Authora are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Authora uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Before 1.0, minor versions may contain breaking changes; they are listed under *Changed* or *Removed*.

## [Unreleased]

### Upgrading

- Stop all instances before applying the five new migrations `TwoStepSignIn`, `SessionTokenConcurrency`, `MultiFactorAuthentication`, `MfaRequirements` and `Passkeys`.
- Passkeys are bound to the host of `Authora:OAuth:Issuer`: settle on the final host before Users add them.
- Grants from before the upgrade have no recorded authentication methods and count as a sign-in with the password only; if you turn on `Authora:Mfa:RequireForAdministrators`, administrators sign in again.
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
- ID Tokens contain `amr` (RFC 8176: `pwd`, `otp`, `hwk`/`swk` for passkeys, plus `mfa` for two or more factors) and `acr` (`urn:authora:acr:pwd`, `urn:authora:acr:mfa`, or `phr` of OpenID EAP ACR Values with a passkey); the discovery document lists `acr_values_supported`.
- `acr_values` in authorization requests: a Session below the requested level is stepped up if the User has a factor that reaches it; otherwise the User gets the strongest level they can reach. With `prompt=none`, `interaction_required`.
- Client setting *Require MFA* (`PUT`/`DELETE /api/clients/{id}/mfa-required`, `clients.update`, in the admin interface): Users step up or set up an authenticator app before the Client gets a code, and codes issued without a second factor can no longer be exchanged. Audit events `client.mfa-required` and `client.mfa-no-longer-required`.
- Setting `Authora:Mfa:RequireForAdministrators` (off by default): Users who hold a Permission set up an authenticator app right after the password, authorization requests with `authora.admin` step up their Sessions, and the admin API rejects their Access Tokens from sign-ins without a second factor with `403` and `insufficient_user_authentication` (RFC 9470). Grants now record the authentication methods of their sign-in.
- The authenticator setup page takes a `returnUrl` to continue at once the app is set up. Log events 4021, 4022, 5024 and 6017 – 6019.
- Passkeys (WebAuthn Level 2, with Fido2NetLib) as a phishing-resistant second factor ([#50](https://github.com/DuttonAlan/Authora/issues/50)). Users register several at `/account/security/passkeys` (recent authentication, and a step-up if they have a second factor but the Session lacks it), name, rename and remove them; removing one ends all other Sessions. The second step of the sign-in offers *Use a passkey* next to the code of the app; the Session records `hwk` (device-bound) or `swk` (may be synced) and reaches `acr` `phr`. Challenges are random, stored as hashes, single-use and valid for 5 minutes; a signature counter that did not increase is rejected as a possible clone, and rejected passkeys count towards the lockout. The relying party Id is the host of the Issuer.
- Admin API and interface: `GET /api/users/{id}/mfa` lists the passkeys of a User, `DELETE /api/users/{id}/passkeys/{passkeyId}` removes one (`users.reset-mfa`) and ends all Sessions and Grants of the User; the MFA reset and deleting a User remove the passkeys too.
- Audit events `passkey.registered`, `passkey.renamed`, `passkey.removed`, `passkey.used`, `passkey.failed` and `user.passkey-removed`, log events 8006 and 17001 – 17009.
- Signed-in Users whose Email is not verified request a new verification link themselves at `/account/email-verification`, offered as a notice on `/account/security` and on the page after sign-in ([#77](https://github.com/DuttonAlan/Authora/issues/77)). Only with email delivery configured; earlier links stop working. At most `Authora:EmailVerification:MaxEmailsPerHour` (3) links per User and hour, counting those sent automatically or by an administrator, and `Authora:RateLimiting:EmailVerificationPermitsPerMinute` (5) requests per IP address. Audit event `account.email-verification-requested`, log events 5025 and 5026.
- The sign-in pages that use passkeys (`/account/second-factor`, `/account/security/passkeys`) load the static module `/js/passkeys.js` and allow `script-src 'self'` and `connect-src 'self'`; inline scripts stay forbidden.

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
