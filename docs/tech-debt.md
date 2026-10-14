# Authora – Tech Debt

Shortcuts and temporary decisions that were taken on purpose and must be resolved later. Bugs (unintended behaviour) are tracked as GitHub issues.

When an entry is resolved, keep it: add `**Status:** Resolved (<date>)` and replace "To resolve" with a short "Resolution". IDs are never reused, so the next entry is **TD-015**.

| ID | Title | Status |
|---|---|---|
| [TD-001](#td-001-users-can-be-created-without-a-password) | Users can be created without a password | Resolved (2026-10-12) |
| [TD-002](#td-002-disabling-a-client-does-not-revoke-its-refresh-tokens) | Disabling a Client does not revoke its Refresh Tokens | Resolved (2026-10-11) |
| [TD-003](#td-003-the-admin-api-has-no-authentication-or-authorization) | The admin API has no authentication or authorization | Resolved (2026-10-11) |
| [TD-004](#td-004-the-last-administrator-check-is-not-safe-against-concurrent-requests) | The last-administrator check is not safe against concurrent requests | Resolved (2026-10-12) |
| [TD-005](#td-005-the-email-of-a-deleted-user-can-never-be-used-again) | The Email of a deleted User can never be used again | Resolved (2026-10-12) |
| [TD-006](#td-006-removing-roles-or-permissions-does-not-rotate-the-securitystamp) | Removing Roles or Permissions does not rotate the SecurityStamp | Resolved (2026-10-12) |
| [TD-007](#td-007-browser-based-clients-cannot-call-the-token-endpoint-no-cors) | Browser-based Clients cannot call the token endpoint (no CORS) | Resolved (2026-10-12) |
| [TD-008](#td-008-expired-codes-tokens-and-sessions-are-never-deleted) | Expired codes, tokens and Sessions are never deleted | Resolved (2026-10-12) |
| [TD-009](#td-009-every-confidential-client-can-introspect-every-access-token) | Every Confidential Client can introspect every Access Token | Resolved (2026-10-12) |
| [TD-010](#td-010-the-health-endpoint-reveals-the-schema-version) | The health endpoint reveals the schema version | Resolved (2026-10-12) |
| [TD-011](#td-011-signing-keys-are-only-read-at-startup) | Signing keys are only read at startup | Resolved (2026-10-14) |
| [TD-012](#td-012-the-admin-interface-asks-for-consent-on-every-sign-in) | The admin interface asks for consent on every sign-in | Open |
| [TD-013](#td-013-audit-events-can-be-changed-with-database-access) | Audit events can be changed with database access | Open |
| [TD-014](#td-014-the-admin-interface-keeps-its-tokens-in-the-browser) | The admin interface keeps its tokens in the browser | Open |

## TD-001: Users can be created without a password

**Status:** Resolved (2026-10-12) · **Area:** Users (`User`, `CreateUserService`)

`User.PasswordHash` was nullable and `POST /api/users` took no password, so a new User could not sign in until an administrator called `PUT /api/users/{id}/password`.

**Resolution:** `CreateUserRequest` requires a `Password` that follows `PasswordPolicy`, `User.Create` takes its hash, and `PasswordHash` is `NOT NULL` (migration `RequireUserPassword`). The migration fills the column for deleted Users and fails if any other User has no password. `GetUserResponse.HasPassword` was removed because it was always `true`. Users still cannot choose their own password, which is part of US-010.

## TD-002: Disabling a Client does not revoke its Refresh Tokens

**Status:** Resolved (2026-10-11) · **Area:** Client Management (`DisableClientService`)

Disabling a Client only set its status, so already issued Refresh Tokens could still be exchanged for new Access Tokens.

**Resolution:** `DisableClientService` revokes all Grants of the Client, and with them their Access and Refresh Tokens. `/oauth/token` and `/oauth/introspect` reject disabled Clients. Enabling the Client again does not restore the Grants.

## TD-003: The admin API has no authentication or authorization

**Status:** Resolved (2026-10-11) · **Area:** API (admin controllers under `/api/*`)

Anyone who could reach Authora could call every admin endpoint, e.g. to grant themselves the administrator Role.

**Resolution:** the admin API accepts only Authora Access Tokens with the System Scope `authora.admin` (policy `admin-api`), and every action requires a Permission through the User's Roles. `IdentitySeeder` seeds the Permissions, the administrator Role, the Scope and the first administrator. The public health details were split off into TD-010.

## TD-004: The last-administrator check is not safe against concurrent requests

**Status:** Resolved (2026-10-12) · **Area:** Users (`AdministratorGuard`, `DeactivateUserService`, `DeleteUserService`, `RemoveRoleService`)

`AdministratorGuard` counted the remaining administrators and then saved, without a lock (check-then-act). Two requests at the same time against the last two administrators could both pass the check, and then no administrator was left.

**Resolution:** `IUserRepository.BeginAdministratorChangeAsync` starts a transaction that holds `pg_advisory_xact_lock(AdvisoryLockKeys.Administrators)`. The three services take it before loading the User and commit after saving, so these changes run one after another. No unit of work was needed, because all three only use `IUserRepository`.

## TD-005: The Email of a deleted User can never be used again

**Status:** Resolved (2026-10-12) · **Area:** Users (`DeleteUserService`, `UserRepository.EmailExists`, `UserConfiguration`)

A soft-deleted User kept its Email, and the unique index includes deleted Users, so the Email returned 409 forever. Keeping it could also conflict with GDPR Art. 17.

**Resolution:** `User.MarkDeleted` anonymizes the User: the Email becomes `deleted-<id>@deleted.invalid`, the Name "Deleted User", and the password hash is removed. The row and its Id stay for history, so the unique index is unchanged. The migration `AnonymizeDeletedUsers` does the same for Users that were already deleted. Administrators can no longer see who a deleted User was.

## TD-006: Removing Roles or Permissions does not rotate the SecurityStamp

**Status:** Resolved (2026-10-12) · **Area:** Users and Roles (`RemoveRoleService`, `DeleteRoleService`, `RevokePermissionService`)

These operations reduce a User's Permissions but do not rotate the `SecurityStamp`, so Sessions and tokens stay valid.

**Resolution:** closed without a code change, because nothing caches Permissions. Access Tokens, Refresh Tokens, Sessions and introspection responses only carry the User Id, the Client and the Scopes, never Roles or Permissions. Granting a Scope, including `authora.admin`, does not depend on Permissions. The admin API loads the Permissions on every request (`PermissionEvaluator`), which `AdminApiAccessTests.RoleRemovedBetweenRequests_TakesEffectOnNextRequest` covers. Reopen this entry if Roles or Permissions are ever put into tokens (e.g. ID Token claims in Phase 5) or cached.

## TD-007: Browser-based Clients cannot call the token endpoint (no CORS)

**Status:** Resolved (2026-10-12) · **Area:** OAuth (`/oauth/token`, `/oauth/revoke`)

No CORS headers were sent, so SPAs on another origin could not exchange codes. They needed a backend for frontend instead.

**Resolution:** `OAuthCorsPolicyProvider` replaces the default `ICorsPolicyProvider` and only knows the policy `oauth-clients`, which `[EnableCors]` applies to the token and revocation actions. It allows the origins of the Redirect URIs of enabled Clients, only `POST`, only `Content-Type` and no credentials. The origins are cached for `Authora:OAuth:CorsOriginsCacheDuration` (1 minute by default), so a new or disabled Client takes effect after at most that time. All other endpoints have no policy.

## TD-008: Expired codes, tokens and Sessions are never deleted

**Status:** Resolved (2026-10-12) · **Area:** OAuth (`auth` schema)

Authorization Codes, Grants, Access and Refresh Tokens and Sessions were marked as consumed, revoked or expired, but never deleted, so the tables grew without limit.

**Resolution:** `ExpiredDataCleanupService` runs `ExpiredDataCleaner` every `Authora:Cleanup:Interval`. It deletes in batches what expired or was revoked more than `Authora:Cleanup:Retention` (7 days by default) ago: codes and tokens by `ExpiresAt`, Sessions by `ExpiresAt` or `RevokedAt`, and Grants that were revoked or have no tokens left. It logs the counts per run. Consumed codes and Refresh Tokens stay until their expiry plus the retention, so reuse detection keeps working meanwhile. The migration `AddExpiresAtIndexes` indexes `ExpiresAt` for the cleanup queries.

## TD-009: Every Confidential Client can introspect every Access Token

**Status:** Resolved (2026-10-12) · **Area:** OAuth (`IntrospectTokenService`, `/oauth/introspect`)

Access Tokens have no audience, so any enabled Confidential Client could learn the User, Client and Scopes of any token it had.

**Resolution:** a resource server is a Confidential Client with served Scopes (`Client.ServedScopes`, table `client_served_scopes`, `PUT`/`DELETE /api/clients/{id}/served-scopes/{scopeId}`). It can introspect an Access Token only if the token contains one of its served Scopes, and `scope` then lists only those. The Client a token was issued to still sees all its Scopes. For every other Client the token is inactive. RFC 8707 (`aud`) was not added.

## TD-010: The health endpoint reveals the schema version

**Status:** Resolved (2026-10-12) · **Area:** API (`/health`)

`GET /health` listed the applied and pending migrations to anyone, which revealed the exact version that is running.

**Resolution:** `/health` and `/health/live` only return `{"status": "..."}` with the usual status codes (503 when unhealthy), which is enough for load balancers and orchestrators. The full report moved to `GET /health/details`, which needs an admin Access Token and the new Permission `health.read`. The seeder adds the Permission and grants it to the administrator Role. Access by internal network was not added, because it needs trusted proxy and IP range configuration, and probes do not need the details.

## TD-011: Signing keys are only read at startup

**Status:** Resolved (2026-10-14) · **Area:** OpenID Connect (`PemSigningKeyProvider`)

The ID Token signing key and the keys published for rotation are read from PEM files once at startup. Every rotation step (publish the new key, switch the signing key, remove the old key) needs a restart of all instances, and the operator has to time the steps by hand, see [openid-connect.md](openid-connect.md#key-rotation-us-035).

**Resolution:** `SigningKeyReloadService` reloads the key files and paths every `Authora:Oidc:KeyReloadInterval` (default 1 minute) through `IOptionsMonitor`, comparing a hash of the files first (US-035). Polling was chosen over a file watcher, because watchers miss the symlink swaps of mounted Kubernetes secrets. A file that cannot be loaded keeps the previous keys and reports the health check `signing-keys` as `Degraded`. Every change is recorded in the audit log. The steps are still timed by the operator; automatic rotation on a schedule would need the keys in a store Authora can write to.

## TD-012: The admin interface asks for consent on every sign-in

**Status:** Open · **Area:** Admin interface, consent (`/oauth/authorize`)

Authora asks for consent on every authorization request and does not remember decisions (see [oauth-core.md](oauth-core.md#consent-policy)). The admin interface is part of Authora, but it is an ordinary Public Client, so administrators see the consent screen every time they sign in to it, which trains them to click "Allow" without reading.

**To resolve:** remember consent per User, Client and Scopes (with a way to withdraw it), or mark Clients as first-party so they skip the consent screen. First-party must not be settable through the admin API without a dedicated Permission, because it would let a Client skip consent for any Scope.

## TD-013: Audit events can be changed with database access

**Status:** Open · **Area:** Audit log (`audit.events`)

The audit log (US-034) is stored in Authora's own database. The API cannot change or delete events, but anyone with write access to the database, including the application's database user if `UPDATE` is not revoked, can change or delete them without a trace. There is also no export to an external system.

**To resolve:** chain the events with hashes (each event stores the hash of the previous one) so changes are detectable, or forward them to an append-only external store (e.g. a SIEM or object storage with retention locks). Until then, ship the application logs to a separate system and restrict the database permissions as described in [operations.md](operations.md#database).

## TD-014: The admin interface keeps its tokens in the browser

**Status:** Open · **Area:** Admin interface (`wwwroot/admin/js/auth.js`)

The admin interface keeps its Access and Refresh Token in `sessionStorage`. A script that runs on the admin interface could read them and use them from elsewhere until they expire or the Grant is revoked. The strict Content Security Policy (no inline or foreign scripts) and inserting all text as text nodes make such a script unlikely, but do not rule it out (e.g. a compromised dependency of the browser).

**To resolve:** a backend for frontend: a server-side part of the admin interface that holds the tokens and gives the browser only an `HttpOnly` cookie, or sender-constrained tokens (DPoP, RFC 9449), which a stolen token cannot be used without.
