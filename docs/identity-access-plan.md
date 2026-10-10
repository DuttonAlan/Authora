# Authora – Identity & Access Management Plan

This document lists the endpoints, application logic, infrastructure and utilities needed to manage the identity domain described in [domain-identity.md](domain-identity.md): Users, Roles, Permissions, Scopes and Clients.

The OAuth runtime (Sessions, Authorization Codes, Refresh Tokens, sign-in) is out of scope and only listed under [Later](#later-oauth-runtime-auth-schema).

## Endpoints

✅ = already implemented. Each new endpoint gets its own `*Service` in `Core/Application/<Area>/<UseCase>/`, following the existing User services.

### Users – `/api/users` (US-006, US-009, US-028)

| Method | Route | Service | Logic / notes |
|---|---|---|---|
| POST | `/` ✅ | `CreateUserService` | Return 409 if the `NormalizedEmail` already exists. |
| GET | `/` ✅ | `GetAllUsersService` | Add paging and filters (`state`, `search`). |
| GET | `/{id}` ✅ | `GetUserService` | Include the Roles (`Include(Roles)`). |
| PUT | `/{id}` ✅ | `UpdateUserService` | A missing User currently throws `Exception`, which returns 500. Return 404 instead. Set `ModifiedAt`. Return 409 on an Email conflict. |
| DELETE | `/{id}` ✅ | `DeleteUserService` | Currently a hard delete. `UserState.Deleted` means "keep for history", so set `State = Deleted` and call `RotateSecurityStamp()` instead. |
| POST | `/{id}/deactivate` | `DeactivateUserService` | Set `State = Deactivated` and call `RotateSecurityStamp()`. Log it. |
| POST | `/{id}/activate` | `ActivateUserService` | Set `State = Active`. Reject `Deleted` Users. |
| POST | `/{id}/unlock` | `UnlockUserService` | Set `FailedLoginCount = 0` and `LockoutEndAt = null`. |
| POST | `/{id}/revoke-sessions` | `RevokeUserSessionsService` | Call `RotateSecurityStamp()`. This is the admin "sign out everywhere" (US-009). |
| GET | `/{id}/roles` | `GetUserRolesService` | |
| PUT | `/{id}/roles/{roleId}` | `AssignRoleService` | Idempotent. Return 404 for an unknown User or Role. Load `Roles` first. |
| DELETE | `/{id}/roles/{roleId}` | `RemoveRoleService` | Idempotent. Prevent removing the last administrator. |
| GET | `/{id}/permissions` | `GetEffectivePermissionsService` | Union of the Permissions of all Roles, with the Role each one comes from (traceability, US-030). |
| POST | `/{id}/password` or `/{id}/invite` | `SetPasswordService` / `InviteUserService` | Depends on how [TD-001](tech-debt.md) is resolved. An invite needs a reset token table and an email sender. |

### Roles – `/api/roles` (US-027)

| Method | Route | Service | Logic / notes |
|---|---|---|---|
| POST | `/` | `CreateRoleService` | `Name` must be unique (409). Validate its format, e.g. lowercase `[a-z0-9._-]`. |
| GET | `/` | `GetAllRolesService` | |
| GET | `/{id}` | `GetRoleService` | Include the Permissions. |
| PUT | `/{id}` | `UpdateRoleService` | Updates `DisplayName` and `Description`. Decide whether `Name` can change on System Roles (probably not). |
| DELETE | `/{id}` | `DeleteRoleService` | Return 409 if `IsSystem`. Deleting cascades to `user_roles`, so log it. |
| PUT | `/{id}/permissions/{permissionId}` | `GrantPermissionService` | Load `Permissions` first. |
| DELETE | `/{id}/permissions/{permissionId}` | `RevokePermissionService` | Do not let the system `admin` Role lose its admin Permissions. |

### Permissions – `/api/permissions`

| Method | Route | Service | Logic / notes |
|---|---|---|---|
| GET | `/` | `GetAllPermissionsService` | Read-only. |

Permissions are defined in code, not through CRUD endpoints. A Permission only has an effect when an endpoint checks it (e.g. `[Authorize("users.create")]`), so a Permission created at runtime would do nothing. They are defined as constants and seeded (see [Infrastructure](#infrastructure)).

### Scopes – `/api/scopes` (US-031, US-026)

| Method | Route | Service | Logic / notes |
|---|---|---|---|
| POST | `/` | `CreateScopeService` | `Name` must be unique and follow RFC 6749, Section 3.3 (`%x21 / %x23-5B / %x5D-7E`, no spaces). This belongs in the domain, but `Scope` has no validation yet. |
| GET | `/` | `GetAllScopesService` | |
| GET | `/{id}` | `GetScopeService` | |
| PUT | `/{id}` | `UpdateScopeService` | Renaming changes what Clients send in requests. Consider making `Name` immutable. |
| DELETE | `/{id}` | `DeleteScopeService` | Deleting cascades from `client_scopes`, so return 409 if the Scope is in use (or add `?force`). Protect `openid`, `profile` and `email`; this needs an `IsSystem` flag on `Scope`. |

### Clients – `/api/clients` (US-011 – US-014)

| Method | Route | Service | Logic / notes |
|---|---|---|---|
| POST | `/` | `RegisterClientService` | Takes Name, Type, Grant Types, TTLs, Redirect URIs and Scope Ids. For a Confidential Client, generate the first Secret and return it in plain text once. |
| GET | `/` | `GetAllClientsService` | |
| GET | `/{id}` | `GetClientService` | Include Redirect URIs, Scopes and Secret metadata (Id, created, expires, revoked). Never return the hash. |
| PUT | `/{id}` | `UpdateClientService` | Updates Name, TTLs and Grant Types. TTLs must be greater than 0, and the Refresh Token TTL greater than the Access Token TTL. Reject `RefreshToken` without `AuthorizationCode`. `Type` cannot change. |
| DELETE | `/{id}` | `DeleteClientService` | Or only allow disabling, to keep the audit trail. |
| POST | `/{id}/enable` | `EnableClientService` | Log it. |
| POST | `/{id}/disable` | `DisableClientService` | Log it (US-014). Later, also revoke the Client's Refresh Tokens. |
| POST | `/{id}/redirect-uris` | `AddRedirectUriService` | `RedirectUri.Create` validates the URI; `ArgumentException` maps to 400. |
| DELETE | `/{id}/redirect-uris` | `RemoveRedirectUriService` | Pass the URI in the body or query string, because a URI in the route path is awkward. |
| PUT | `/{id}/scopes/{scopeId}` | `AllowScopeService` | Load `AllowedScopes` first. |
| DELETE | `/{id}/scopes/{scopeId}` | `DisallowScopeService` | |
| POST | `/{id}/secrets` | `CreateClientSecretService` | Optional `expiresAt`. Returns the plain-text Secret once. Return 400/409 for a Public Client (`AddSecret` throws `InvalidOperationException`). |
| POST | `/{id}/secrets/{secretId}/revoke` | `RevokeClientSecretService` | Uses `TimeProvider` for `now`. |

### Operations (US-001 – US-003)

| What | Form | Notes |
|---|---|---|
| Health and migration status | `GET /health` | `AddHealthChecks().AddDbContextCheck<AuthoraContext>()` and report pending migrations. |
| Bootstrap the first administrator | CLI command (e.g. `dotnet Authora.Api.dll bootstrap-admin`) or a one-time startup seeder | Not an HTTP endpoint. Creates the User, assigns the system `admin` Role and sets a password. Idempotent, and refuses to run if an administrator already exists. |

## Application layer (Core)

| Item | Purpose |
|---|---|
| `NotFoundException`, `ConflictException` (or a `Result<T>` type) | Typed outcomes for services instead of `new Exception(...)`. |
| `IExceptionHandler` (in Api) | Maps exceptions to ProblemDetails: `ArgumentException` → 400, `NotFoundException` → 404, `ConflictException` / `InvalidOperationException` → 409, unique-violation `DbUpdateException` (Npgsql `23505`) → 409. Covers US-004. |
| Repository abstractions | `IRoleRepository`, `IPermissionRepository`, `IScopeRepository`, `IClientRepository`. Each loads the whole aggregate tracked, with `Include`s, and has a `SaveChangesAsync`. |
| `IPasswordHasher` | `Hash(password)` and `Verify(hash, password)` → `Success` / `Failed` / `SuccessRehashNeeded`. Needed for TD-001 and the bootstrap administrator. |
| `ISecretGenerator` | Generates Client Secrets now. Reset tokens, Authorization Codes and Refresh Tokens use it later. |
| `ISecretHasher` | One-way hash and constant-time verify for high-entropy secrets. |
| `TimeProvider` (built into .NET) | For `RevokeSecret(now)`, `IsActive(now)`, lockout and `ModifiedAt`. Makes the code testable. |
| Paging contracts | `PageRequest(page, size)` and `PagedResponse<T>(items, total)` for the list endpoints. |
| `IAuditLogger` | Records disabling, deletion and Role / Permission changes, which several stories require. Writes structured logs for now; US-034 can store them in a table later. |

## Infrastructure

| Item | Implementation |
|---|---|
| `RoleRepository`, `PermissionRepository`, `ScopeRepository`, `ClientRepository` | EF Core with `Include`s. Also fix `UserRepository.UpdateUser`: it calls `context.Users.Update(user)`, which [domain-identity.md](domain-identity.md#working-with-the-domain) warns against. Use tracked changes and `SaveChangesAsync` instead. |
| `PasswordHasher` | Wraps `Microsoft.AspNetCore.Identity.PasswordHasher<User>` (PBKDF2, versioned format, rehash support) without pulling in full ASP.NET Identity. Argon2id is an alternative. |
| `SecretGenerator` | `RandomNumberGenerator.GetBytes(32)`, encoded as Base64Url. |
| `Sha256SecretHasher` | SHA-256 and `CryptographicOperations.FixedTimeEquals`. A slow hash is not needed because the secrets are 256-bit random values, and it keeps `/token` fast. |
| `ModifiedAt` interceptor | A `SaveChangesInterceptor` that sets `ModifiedAt` on modified entities, so services do not have to. |
| `IdentitySeeder` | Idempotent seeding at startup: the Permission catalog, the system `admin` Role with all Permissions, and the standard Scopes (`openid`, `profile`, `email`, `offline_access`). Runs at runtime instead of `HasData`, because Ids are version 7 Guids created in code. |
| Options with `ValidateOnStart` | Strongly typed options (database, security settings) that fail fast at startup (US-001). |
| Health checks | Database connectivity and pending migrations (US-002). |
| `IEmailSender` | Only needed if TD-001 is resolved with an invite or reset flow. A logging or no-op implementation is enough for development. |

## Utilities

| Utility | Location | Purpose |
|---|---|---|
| `Permissions` constants (e.g. `Users.Create = "users.create"`) | Core | Single list used by the seeder, the authorization policies and the tests. |
| `ScopeName.IsValid`, `RoleName.IsValid` | Core / Domain | Name format rules. Better placed in the entities as `Create` factories, like `User.Create`. |
| `Base64Url` | `System.Buffers.Text.Base64Url` (.NET 9+) | Encodes secrets and tokens. |
| `GrantTypes` ↔ `"authorization_code"` mapping | Core | Converts between the enum and the API / OAuth strings. |
| Log redaction | `Microsoft.Extensions.Compliance.Redaction` or `[LogProperties]` | Keeps passwords and secrets out of logs (US-004). |

## Open decisions

- **The admin API is unauthenticated.** All endpoints above should eventually require `[Authorize(Policy = "<permission>")]`, backed by a permission-based `IAuthorizationHandler` (US-032). That needs tokens or sessions, which come later. Until then, either restrict the API to development or add a temporary API key.
- **The domain is missing some rules.** `Role`, `Scope` and `Permission` have public setters and no validation. `Scope` has no `IsSystem` flag. Groups (US-029, US-030) are not modelled yet.
- **TD-001.** Decide between an invite flow and a password on creation before building the bootstrap, because the bootstrap needs a way to set a password.

## Later (OAuth runtime, `auth` schema)

Sign-in, sign-out and Sessions, password reset tokens, `/authorize`, consent, `/token`, `/revoke`, `/.well-known/openid-configuration`, `/jwks`, `/userinfo`, and signing key management.

## Suggested order

1. Exception handler and `TimeProvider`
2. Fix the existing User endpoints
3. Seeder and `Permissions` constants
4. Roles and Permissions
5. Scopes
6. Clients, including the secret services
7. Password hasher and bootstrap administrator
