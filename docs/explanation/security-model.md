# Security model

This page explains what Authora protects, against whom, and the reasoning behind its main security decisions. It is meant for operators deciding whether Authora fits their needs, and for contributors changing security-relevant code.

> Authora has not had an independent security audit. Report vulnerabilities as described in [SECURITY.md](../../SECURITY.md).

## What Authora protects

- **User credentials**: passwords, and the Sessions that prove a sign-in.
- **Delegated access**: codes and tokens that let applications act for users.
- **Its own configuration**: who is an administrator, which applications exist and what they may request.
- **The record of what happened**: the audit log.

## Assumptions

- The **operator** controls the host, the configuration and the keys, and is trusted.
- **Database administrators** are trusted not to change data, but Authora limits what a stolen database copy is worth and makes changes to the audit log detectable.
- **Clients** are only as trusted as their registration allows: Redirect URIs, Scopes and, for Confidential Clients, their Secret.
- The **network** is hostile: everything runs over TLS, and Authora assumes requests can be forged or replayed.

## Security boundaries

| Endpoint | Who may call it | Protection |
|---|---|---|
| `/.well-known/*` | Anyone | Public metadata only. |
| `/oauth/authorize`, `/account/*` | Browsers | Session cookie, antiforgery tokens, exact Redirect URI match, PKCE, rate limit, lockout. |
| `/oauth/token`, `/oauth/revoke` | Clients | Client authentication (Secret or PKCE), rate limit. |
| `/oauth/introspect` | Confidential Clients | Client Secret, rate limit (shared with `/oauth/token` and `/oauth/revoke`); only tokens with a served Scope are reported. |
| `/oauth/userinfo` | Clients | Access Token with `openid`. |
| `/api/*` | Administrators, through a Client | Access Token with `authora.admin`, plus a Permission per action; nobody grants more than they hold. |
| `/admin/*` | Administrators' browsers | Static files and a backend for frontend; tokens never reach the browser. |
| `/health`, `/health/live` | Anyone | Overall status only. |

## Key decisions

### Secure by default, and refuse to start otherwise

Outside development, Authora refuses to start with an HTTP Issuer, with `AllowedHosts=*`, or with overly broad trusted proxy networks. Every setting has hard upper limits, so a typo like `15` (15 days) instead of `00:15:00` stops the start instead of silently creating a fifteen-day lockout. Misconfigurations fail loudly at deployment time, not quietly in production.

### Nothing in the database is directly usable

Passwords are hashed with PBKDF2-HMAC-SHA512 (210,000 iterations). Codes, tokens, Session tokens, the one-time tokens of email links and Client Secrets are random 256-bit values stored only as SHA-256 hashes. Signing keys are files, never in the database. A stolen backup therefore gives an attacker neither working tokens nor the ability to forge ID Tokens.

### Opaque Access Tokens

Access Tokens carry no data and are checked against the database on every use, so revocation, deactivating a User or disabling a Client takes effect at once. See [Tokens, Grants and Sessions](tokens-and-sessions.md#why-access-tokens-are-opaque).

### PKCE for everyone, exact Redirect URIs

Every Client must use PKCE with `S256`, Confidential Clients included. PKCE protects a code that leaks, e.g. through a log or a malicious app on the device, from being redeemed by someone else, and from being injected into another user's sign-in.

An administrator can make PKCE optional for a single Confidential Client, for OpenID Connect libraries that do not send it; the OpenID conformance suite's Basic OP tests are one such Client. OAuth 2.1 (Section 7.5.1) allows this for Confidential Clients that check the `nonce` in the ID Token, which stops code injection, while the Secret stops others from redeeming the code. It stays an exception: Public Clients always need PKCE, a request that does send PKCE is checked in full, and a `code_verifier` for a code issued without PKCE is rejected, so an attacker cannot downgrade a flow that uses PKCE.

Redirect URIs must match a registered URI exactly; there are no wildcards or prefix matches. An invalid Redirect URI is shown as an error and never redirected to, so Authora cannot be used as an open redirector. The authorization response includes `iss`, so Clients can detect mix-up attacks.

### Reuse revokes everything

A code or Refresh Token used twice revokes the whole Grant, because Authora cannot tell the attacker from the legitimate application. Losing a sign-in is a smaller harm than letting a thief keep access.

### Sign-in does not leak who has an account

Every failed sign-in shows the same message, and unknown Emails still go through a password check, so neither the message nor the response time reveals whether an account exists. The lockout after repeated failures is short (15 minutes by default, at most 1 hour), because anyone who knows an Email can trigger it.

### A second factor comes before the Session, not after

Sign-in is built for a second factor: once a User has enrolled one, the correct password only starts a *pending sign-in*, a single-use, five-minute record bound to the User's security stamp, held in its own cookie. The browser gets a Session cookie only after the second factor, so there is never a half-authenticated Session that some page could forget to check. Wrong second factors count towards the same lockout as wrong passwords, and a correct password alone does not reset that count. A step-up of an existing Session and every completed sign-in issue a new Session token. Sensitive changes to the account, such as setting up or removing the authenticator app, require a recent authentication, and removing it also a current code. Codes of the authenticator app work once, so a phished code is useless after its first use. See [How a User signs in](tokens-and-sessions.md#how-a-user-signs-in).

### Password reset proves control of the mailbox, nothing more

A reset link is only as safe as the User's mailbox: whoever can read it can set a new password. Authora limits what such a link is worth and what the "Forgot password?" form gives away:

- The form answers every address the same way and only queues the request: looking up the address, issuing the link and sending the email happen in the background, so the response time does not depend on whether an account exists. The address of an unknown account is neither logged nor recorded in the audit log.
- The link carries a random 256-bit token, stored only as its hash. It works once, for 30 minutes by default, only for the newest link of the User and only while the User still has the address it was sent to and has not changed the password since (any rotation of the security stamp, such as a password change, a password set by an administrator, deactivation or "sign out everywhere", ends all open links). Opening it does not use it up, so mail scanners that follow links cannot break it.
- A reset revokes every Session and every token of the User, so someone who knew the old password is locked out at once, and the User gets an email about it. It also ends a lockout: proving control of the mailbox is a stronger check than waiting.
- A reset never signs the User in and never replaces a second factor: afterwards, the User signs in with the new password and, if enrolled, the second factor.
- Abuse is bounded twice: per IP address (5 submissions per minute) and per address (3 links per hour), so the form cannot be used to flood a mailbox or probe many accounts quickly.
- Administrators can send a link instead of setting a password, so they never learn it. Sending one needs `users.set-password` and follows the rule that nobody acts on a User with more rights than their own.

### A verified Email is a promise to applications

Applications use `email_verified` to decide whether an Email belongs to the User, e.g. to link an Authora account to an existing one with the same address. A wrong `true` would let someone take over such an account, so Authora only sets it on proof and drops it when the proof no longer applies:

- The proof is a verification link with the same kind of token as a reset link: random, stored only as its hash, single use, valid for 24 hours by default, replaced by newer links, and only valid while the User still has the address it was sent to and the same security stamp. Opening it only shows a button, so mail scanners do not use it up.
- Changing the Email to a different address resets the verification, so `email_verified` always refers to the current address. Upper and lower case are not a different address, because Authora compares Emails without case.
- No sign-in is needed to confirm: the link itself proves control of the mailbox, which is all the claim states.
- Administrators can mark an address as verified without a link. That needs `users.update`, follows the rule that nobody acts on a User with more rights than their own, and is recorded as `user.email-marked-verified`.

### First-party Clients

Third-party Clients ask for consent the first time and for every Scope the User has not approved yet; the User can withdraw the Consent at any time, which also ends the Client's tokens. First-party Clients, applications of the same operator such as the admin interface, skip it and store no Consent. Asking administrators for consent to their own tools on every sign-in would only train them to click *Allow* without reading.

Skipping consent is powerful: a first-party Client gets any allowed Scope for any signed-in User who opens a link. So:

- marking a Client as first-party needs its own Permission, `clients.manage-first-party`, which only `admin` has by default; treat it as equivalent to `admin`, because whoever can make a Client of their own first-party collects the codes of any signed-in User, administrators included;
- while a Client is first-party, no Redirect URI and no Scope can be added to it. Otherwise anyone with the ordinary `clients.update` Permission could add their own Redirect URI to the admin interface's Client and collect the codes of every administrator who clicks a link. For the same reason, `clients.update` cannot lower its other protections either: no Post-logout Redirect URI can be added (it would make the logout of the admin interface, whose Client Id is public, an open redirect on Authora's origin), PKCE cannot be made optional, and `client_credentials` cannot be newly allowed.

### Nobody grants more than they hold

Permissions are meant to be delegated: a help desk Role may reset passwords without being able to manage Clients. Delegation is only safe if a Permission cannot be turned into more rights. Without a check, `users.assign-roles` could assign `admin` to its holder, `roles.grant-permissions` could extend its holder's own Role, and `users.set-password` could set the password of an administrator and then sign in as them.

So every change that gives out rights or acts on a User is also checked against the caller's own effective Permissions: a caller who is not an administrator can only give out Permissions they hold, and can only act on Users whose Permissions are a subset of their own. Only an administrator assigns `admin` or acts on an administrator. The check lives in the Core services, not the controllers, holds a database lock until the change is saved, and records denied attempts in the audit log. See [Admin API](../reference/admin-api.md#nobody-grants-more-than-they-hold).

`clients.manage-first-party` is the exception the rule cannot catch: it gives out no Permission, but lets its holder skip consent for a Client they control (see above), so it is equivalent to `admin`.

### Two authentication schemes, kept apart

The Session cookie is only accepted by the sign-in and consent pages. The admin API only accepts Access Tokens. Even the admin interface, served from the same origin, goes through a regular OAuth flow. See [The admin interface](admin-interface.md).

### Defense in depth in the browser

Strict Content Security Policies with no inline scripts, `__Host-` cookies with `SameSite`, antiforgery tokens on every form, no CORS for the admin API, `X-Frame-Options: DENY` and `frame-ancestors 'none'` against clickjacking. See [HTTP security headers and cookies](../reference/http-security.md).

### An audit log that cannot be quietly changed

Every change is recorded in the same transaction as the change itself, and events are sealed into a hash chain whose checkpoints go to the application logs. See [Audit log integrity](audit-log-integrity.md).

## Known gaps

- Multi-factor authentication is up to each User: Authora cannot require it, and applications cannot tell from the ID Token whether a sign-in used it. Ask administrators to set up an authenticator app, see [Require multi-factor authentication](../how-to/require-multi-factor-authentication.md). TOTP codes can still be phished in real time by a proxy that relays them at once; passkeys cannot, because the browser only uses them on Authora's own origin. With email delivery configured, the mailbox is a second way in through the password reset: protect it as well as the password, or turn the self-service reset off (`Authora:PasswordReset:SelfServiceEnabled`).
- Reset requests from the sign-in page and the emails sent in the background are kept in memory and lost if the server stops before they are handled; the User asks again.
- `prompt=login` and `max_age` travel through the browser and can be stripped. Clients that need a recent sign-in must check `auth_time` themselves.
- A script running on the admin interface could act as the signed-in administrator while the page is open, like on any web application. The Content Security Policy is what keeps such scripts out.
- Someone with write access to the database can change audit events in the few seconds before they are sealed, or rebuild the whole chain. Only the checkpoints in a separate log system reveal a rebuilt chain.

The full list of unsupported features is in [Standards and limitations](../reference/standards.md#not-supported).
