# Authora – Tech Debt

Shortcuts and temporary decisions that were taken on purpose and must be resolved later. Bugs (unintended behaviour) are tracked as GitHub issues.

When an entry is resolved, keep it: add `**Status:** Resolved (<date>)` and replace "To resolve" with a short "Resolution". IDs are never reused, so the next entry is **TD-011**.

| ID | Title | Status |
|---|---|---|
| [TD-001](#td-001-users-can-be-created-without-a-password) | Users can be created without a password | Resolved (2026-10-12) |
| [TD-002](#td-002-disabling-a-client-does-not-revoke-its-refresh-tokens) | Disabling a Client does not revoke its Refresh Tokens | Resolved (2026-10-11) |
| [TD-003](#td-003-the-admin-api-has-no-authentication-or-authorization) | The admin API has no authentication or authorization | Resolved (2026-10-11) |
| [TD-004](#td-004-the-last-administrator-check-is-not-safe-against-concurrent-requests) | The last-administrator check is not safe against concurrent requests | Resolved (2026-10-12) |
| [TD-005](#td-005-the-email-of-a-deleted-user-can-never-be-used-again) | The Email of a deleted User can never be used again | Open |
| [TD-006](#td-006-removing-roles-or-permissions-does-not-rotate-the-securitystamp) | Removing Roles or Permissions does not rotate the SecurityStamp | Open |
| [TD-007](#td-007-browser-based-clients-cannot-call-the-token-endpoint-no-cors) | Browser-based Clients cannot call the token endpoint (no CORS) | Open |
| [TD-008](#td-008-expired-codes-tokens-and-sessions-are-never-deleted) | Expired codes, tokens and Sessions are never deleted | Open |
| [TD-009](#td-009-every-confidential-client-can-introspect-every-access-token) | Every Confidential Client can introspect every Access Token | Open |
| [TD-010](#td-010-the-health-endpoint-reveals-the-schema-version) | The health endpoint reveals the schema version | Open |

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

**Area:** Users (`DeleteUserService`, `UserRepository.EmailExists`, `UserConfiguration`)

A soft-deleted User keeps its Email, and the unique index includes deleted Users, so the Email returns 409 forever. Keeping it may also conflict with GDPR Art. 17.

**To resolve:** this is a product decision. Either use a filtered unique index and exclude deleted Users from every Email lookup, or anonymize the Email and Name on delete, or keep the Email blocked and only improve the error message.

## TD-006: Removing Roles or Permissions does not rotate the SecurityStamp

**Area:** Users and Roles (`RemoveRoleService`, `DeleteRoleService`, `RevokePermissionService`)

These operations reduce a User's Permissions but do not rotate the `SecurityStamp`, so Sessions and tokens stay valid. The admin API loads Permissions on every request (`PermissionEvaluator`), so the admin API is not affected.

**To resolve:** decide whether anything else caches Permissions. If something does, rotate the stamp of the affected Users in the same transaction, with a bulk update for Role-wide changes. If nothing does, close this entry.

## TD-007: Browser-based Clients cannot call the token endpoint (no CORS)

**Area:** OAuth (`/oauth/token`, `/oauth/revoke`)

No CORS headers are sent, so SPAs on another origin cannot exchange codes. They need a backend for frontend instead.

**To resolve:** add an `ICorsPolicyProvider` for these two endpoints only. It allows the origins of the Redirect URIs of enabled Clients (cached), only `POST`, and no credentials. Never enable CORS for `/oauth/authorize`, `/account/*` or `/api/*`.

## TD-008: Expired codes, tokens and Sessions are never deleted

**Area:** OAuth (`auth` schema)

Authorization Codes, Grants, Access and Refresh Tokens and Sessions are marked as consumed, revoked or expired, but never deleted, so the tables grow without limit.

**To resolve:** add a `BackgroundService` that deletes expired rows in batches after a configurable retention period and logs how many it deleted. Reuse detection needs consumed codes and Refresh Tokens for a while.

## TD-009: Every Confidential Client can introspect every Access Token

**Area:** OAuth (`IntrospectTokenService`, `/oauth/introspect`)

Access Tokens have no audience, so any enabled Confidential Client can learn the User, Client and Scopes of any token it has.

**To resolve:** model resource servers and the Scopes they serve. Let a resource server introspect only tokens that contain one of its Scopes, and return only those Scopes. Optionally support RFC 8707 (`aud`).

## TD-010: The health endpoint reveals the schema version

**Area:** API (`/health`)

`GET /health` lists the applied and pending migrations to anyone, which reveals the exact version that is running.

**To resolve:** return only the overall status to anonymous callers, and the details only to authorized callers or internal networks. `/health/live` can stay public.
