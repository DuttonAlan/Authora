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
| `/oauth/introspect` | Confidential Clients | Client Secret; only tokens with a served Scope are reported. |
| `/oauth/userinfo` | Clients | Access Token with `openid`. |
| `/api/*` | Administrators, through a Client | Access Token with `authora.admin`, plus a Permission per action. |
| `/admin/*` | Administrators' browsers | Static files and a backend for frontend; tokens never reach the browser. |
| `/health`, `/health/live` | Anyone | Overall status only. |

## Key decisions

### Secure by default, and refuse to start otherwise

Outside development, Authora refuses to start with an HTTP Issuer, with `AllowedHosts=*`, or with overly broad trusted proxy networks. Every setting has hard upper limits, so a typo like `15` (15 days) instead of `00:15:00` stops the start instead of silently creating a fifteen-day lockout. Misconfigurations fail loudly at deployment time, not quietly in production.

### Nothing in the database is directly usable

Passwords are hashed with PBKDF2-HMAC-SHA512 (210,000 iterations). Codes, tokens, Session tokens and Client Secrets are random 256-bit values stored only as SHA-256 hashes. Signing keys are files, never in the database. A stolen backup therefore gives an attacker neither working tokens nor the ability to forge ID Tokens.

### Opaque Access Tokens

Access Tokens carry no data and are checked against the database on every use, so revocation, deactivating a User or disabling a Client takes effect at once. See [Tokens, Grants and Sessions](tokens-and-sessions.md#why-access-tokens-are-opaque).

### PKCE for everyone, exact Redirect URIs

Every Client must use PKCE with `S256`, Confidential Clients included. Redirect URIs must match a registered URI exactly; there are no wildcards or prefix matches. An invalid Redirect URI is shown as an error and never redirected to, so Authora cannot be used as an open redirector. The authorization response includes `iss`, so Clients can detect mix-up attacks.

### Reuse revokes everything

A code or Refresh Token used twice revokes the whole Grant, because Authora cannot tell the attacker from the legitimate application. Losing a sign-in is a smaller harm than letting a thief keep access.

### Sign-in does not leak who has an account

Every failed sign-in shows the same message, and unknown Emails still go through a password check, so neither the message nor the response time reveals whether an account exists. The lockout after repeated failures is short (15 minutes by default, at most 1 hour), because anyone who knows an Email can trigger it.

### First-party Clients

Third-party Clients ask for consent on every sign-in. First-party Clients, applications of the same operator such as the admin interface, skip it. Asking administrators for consent to their own tools on every sign-in would only train them to click *Allow* without reading.

Skipping consent is powerful: a first-party Client gets any allowed Scope for any signed-in User who opens a link. So:

- marking a Client as first-party needs its own Permission, `clients.manage-first-party`, which only `admin` has by default;
- while a Client is first-party, no Redirect URI and no Scope can be added to it. Otherwise anyone with the ordinary `clients.update` Permission could add their own Redirect URI to the admin interface's Client and collect the codes of every administrator who clicks a link.

### Two authentication schemes, kept apart

The Session cookie is only accepted by the sign-in and consent pages. The admin API only accepts Access Tokens. Even the admin interface, served from the same origin, goes through a regular OAuth flow. See [The admin interface](admin-interface.md).

### Defense in depth in the browser

Strict Content Security Policies with no inline scripts, `__Host-` cookies with `SameSite`, antiforgery tokens on every form, no CORS for the admin API, `X-Frame-Options: DENY` and `frame-ancestors 'none'` against clickjacking. See [HTTP security headers and cookies](../reference/http-security.md).

### An audit log that cannot be quietly changed

Every change is recorded in the same transaction as the change itself, and events are sealed into a hash chain whose checkpoints go to the application logs. See [Audit log integrity](audit-log-integrity.md).

## Known gaps

- No multi-factor authentication. Passwords are the only factor, so protect administrator accounts with long, unique passwords.
- `prompt=login` and `max_age` travel through the browser and can be stripped. Clients that need a recent sign-in must check `auth_time` themselves.
- A script running on the admin interface could act as the signed-in administrator while the page is open, like on any web application. The Content Security Policy is what keeps such scripts out.
- Someone with write access to the database can change audit events in the few seconds before they are sealed, or rebuild the whole chain. Only the checkpoints in a separate log system reveal a rebuilt chain.

The full list of unsupported features is in [Standards and limitations](../reference/standards.md#not-supported).
