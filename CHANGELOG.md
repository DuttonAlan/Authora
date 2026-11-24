# Changelog

All notable changes to Authora are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Authora uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Before 1.0, minor versions may contain breaking changes; they are listed under *Changed* or *Removed*.

## [Unreleased]

### Upgrading

- Apply the new migration `AudienceRestrictedAccessTokens` (see [Upgrade Authora](docs/how-to/upgrade-authora.md)). It adds the Client columns `ResourceIdentifier` and `RequiresResourceIndicator`, `Resources` on Authorization Codes and Grants, and `Audience` on Access Tokens.
- **Introspection now checks the audience** (see *Changed*): an API sees a token only if it is in the token's audience, which is fixed when the token is issued. Tokens requested without `resource` are meant for every Client that serves one of their Scopes at that moment, so existing setups keep working, but an API that starts serving a Scope no longer sees tokens issued before.
- Access Tokens issued before the upgrade have no audience: only the Client they were issued to can introspect them, and the admin API and UserInfo reject them. Applications get working tokens on their next refresh, and the admin interface refreshes its token on the first rejected call. Access Tokens live one hour by default, so APIs that introspect tokens of other Clients may reject requests for up to that long.

### Added

- Audience-restricted Access Tokens ([RFC 8707](https://www.rfc-editor.org/rfc/rfc8707), [#55](https://github.com/DuttonAlan/Authora/issues/55)): a Confidential Client gets an optional Resource Identifier, which makes it an API, and **Require the resource parameter** (admin API `PUT /api/clients/{id}`, **API** panel in the admin interface). `resource` (repeatable) at `/oauth/authorize` and `/oauth/token` for all grant types names the APIs a token is meant for, with downscoping at the token endpoint and on refresh, and the new error `invalid_target`. Authora itself is the resource `{Issuer}/api`. The Scopes of such a token are cut to those its audience serves. See [Protect an API](docs/how-to/protect-an-api.md#4-give-the-api-a-resource-identifier).
- `aud` in introspection responses for Access Tokens.
- Setting `Authora:OAuth:AllowHttpResourceIdentifiers` for HTTP Resource Identifiers in Development.
- Log event 6020 for `resource` values rejected with `invalid_target`.

### Changed

- Introspection reports an Access Token as active to the Client it was issued to and to the Clients in its audience, no longer to every Client that serves one of its Scopes.
- The admin API and UserInfo accept only Access Tokens with Authora (`{Issuer}/api`) in their audience (`401 invalid_token` otherwise).
- The audit events `client.updated` (Resource Identifier and setting) and `client.token-issued` (audience) and the log events 6002, 6003 and 6014 (audience) contain more details.

## [0.4.0] - 2026-11-24

### Upgrading

- Stop all instances before applying the three new migrations `BackchannelLogout`, `ExternalIdentityProviders` and `AccountLinking` (see [Upgrade Authora](docs/how-to/upgrade-authora.md)). Sessions started before the upgrade end; Users sign in again.
- **Signing out now revokes tokens** (see *Changed*): applications that kept using their Refresh Token after the User signed out of Authora have to send the User through Authora again. Grants from before the upgrade are not tied to a Session and are not revoked by a sign-out.
- The Data Protection keys now also encrypt the Client Secrets of external identity providers: keep persisting them (`Authora:DataProtection:KeysPath`) and include them in your backups, or every identity provider fails until its Client Secret is set again. See [Persist Data Protection keys](docs/how-to/persist-data-protection-keys.md).
- Instances now send HTTPS requests to the Back-Channel Logout URIs of Clients and to the identity providers you add (discovery, keys, token endpoint): allow that outgoing traffic, and make sure the certificates of the Back-Channel Logout URIs are trusted on the host.

New pages, endpoints, settings, audit events and log events are listed in the [OpenID Connect](docs/reference/openid-connect.md), [admin API](docs/reference/admin-api.md), [configuration](docs/reference/configuration.md), [audit events](docs/reference/audit-events.md) and [log events](docs/reference/log-events.md) references.

### Added

- Back-channel logout ([OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html), [#54](https://github.com/DuttonAlan/Authora/issues/54)): Clients with a Back-Channel Logout URI receive a signed Logout Token when a Session in which they got tokens is ended, sent from a database outbox with retries. ID Tokens contain `sid`. See [Receive back-channel logout](docs/how-to/receive-back-channel-logout.md).
- Sign-in through external OpenID Connect identity providers, generic or with a preset for Google ([#52](https://github.com/DuttonAlan/Authora/issues/52)), managed in the admin API and interface (new Permissions `identity-providers.read` and `identity-providers.manage`). The provider is a first factor: Users with a second factor still complete it, and Clients see `fed` in `amr`. Signed-in Users link and unlink accounts at `/account/security/external-accounts`. See [Sign in with Google](docs/how-to/sign-in-with-google.md) and [Add an external identity provider](docs/how-to/add-an-external-identity-provider.md).
- Account linking and just-in-time provisioning ([#53](https://github.com/DuttonAlan/Authora/issues/53)): an account at a provider with the verified Email of a User is linked only after the person signs in to that User with its existing methods; a matching Email alone never links. Providers can let people sign up (off by default), optionally only from allowed Email domains, and add new Users to default Groups. See [Let people sign up](docs/how-to/add-an-external-identity-provider.md#let-people-sign-up).
- Users without a password, who sign in through a provider and can set a password at `/account/password`; `hasPassword` in the admin API's Users.
- Admin API and interface: see and remove a User's linked accounts.
- The conformance workflow also runs the OpenID Foundation's Back-Channel Logout OP test plan.

### Changed

- Ending a Session on purpose (signing out at `/account/logout` or `/oauth/logout`, password change and reset, removing a second factor, and the admin API's revoke sessions, deactivate, delete, set password, reset MFA and remove passkey) revokes the Grants approved in it, with their Access and Refresh Tokens, for every Client.
- The token endpoint holds a lock on the Session while it creates a Grant from a code, so a sign-out at the same time cannot miss the new Grant.
- The password hash of a deleted User is now removed (`null`) instead of replaced by a placeholder.

## [0.3.0] - 2026-11-23

### Upgrading

- Stop all instances before applying the five new migrations `TwoStepSignIn`, `SessionTokenConcurrency`, `MultiFactorAuthentication`, `MfaRequirements` and `Passkeys` (see [Upgrade Authora](docs/how-to/upgrade-authora.md)). Sessions started before the upgrade end; Users sign in again.
- The Data Protection keys now also encrypt the secrets of authenticator apps: persist them (`Authora:DataProtection:KeysPath`) and include them in your backups, or every authenticator app stops working when they are lost. See [Persist Data Protection keys](docs/how-to/persist-data-protection-keys.md).
- Passkeys are bound to the host of `Authora:OAuth:Issuer`: settle on the final host before Users add them.
- Grants from before the upgrade count as a sign-in with the password only; if you turn on `Authora:Mfa:RequireForAdministrators`, administrators sign in again.

New pages, endpoints, settings, audit events and log events are listed in the [OpenID Connect](docs/reference/openid-connect.md), [admin API](docs/reference/admin-api.md), [configuration](docs/reference/configuration.md), [audit events](docs/reference/audit-events.md) and [log events](docs/reference/log-events.md) references.

### Added

- Two-step sign-in: Users who have set up a second factor complete it at `/account/second-factor` after the password. Wrong second factors count towards the lockout. Users manage their sign-in methods at `/account/security`.
- Multi-factor authentication with an authenticator app (TOTP, RFC 6238) and 10 single-use recovery codes, set up with a QR code at `/account/security/authenticator` (see [Require multi-factor authentication](docs/how-to/require-multi-factor-authentication.md)).
- Passkeys (WebAuthn Level 2) as a phishing-resistant second factor, registered and managed at `/account/security/passkeys` ([#50](https://github.com/DuttonAlan/Authora/issues/50)).
- Step-up at `/account/step-up`, for a Session that needs a stronger authentication than it has.
- `amr` (RFC 8176) and `acr` in ID Tokens, with `auth_time` reflecting the latest authentication, and `acr_values` in authorization requests, which steps the Session up when the User can reach the requested level.
- Client setting *Require MFA*: Users step up or set up an authenticator app before the Client gets a code.
- Setting `Authora:Mfa:RequireForAdministrators` (off by default): Users who hold a Permission must use a second factor, and the admin API rejects their Access Tokens from sign-ins with the password only (`insufficient_user_authentication`, RFC 9470).
- Admin API and interface: see a User's authenticator app, recovery codes and passkeys, reset them all or remove a single passkey (new Permission `users.reset-mfa`).
- Signed-in Users whose Email is not verified request a new verification link at `/account/email-verification` ([#77](https://github.com/DuttonAlan/Authora/issues/77)).

### Changed

- Deleting a User also deletes their authenticator app, recovery codes and passkeys.
- The pages that use passkeys load the static module `/js/passkeys.js` and allow `script-src 'self'` and `connect-src 'self'`; inline scripts stay forbidden.

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

[Unreleased]: https://github.com/DuttonAlan/Authora/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/DuttonAlan/Authora/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/DuttonAlan/Authora/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/DuttonAlan/Authora/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/DuttonAlan/Authora/releases/tag/v0.1.0
