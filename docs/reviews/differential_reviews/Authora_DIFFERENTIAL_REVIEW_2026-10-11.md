# Authora – Differential Security Review: PR #8 (`feature/add-identity-endpoints`)

**Date:** 2026-10-11
**Range:** `1908972..04a8e75` (merge of PR #8 into `main`)
**Commits:** 13 (`d769439` … `8813cc5`)
**Size:** 140 files, +6,658 / −347 lines (repo: 164 tracked files → MEDIUM, FOCUSED strategy)

---

## 1. Executive Summary

| Severity | New findings | Already tracked |
|---|---|---|
| 🔴 CRITICAL | 0 | 1 (TD-003: admin API is unauthenticated) |
| 🟠 HIGH | 0 | – |
| 🟡 MEDIUM | 2 | 1 (TD-004: last-admin race) |
| 🟢 LOW | 3 | 1 (TD-006: stale sessions after role removal) |
| ℹ️ INFO | 3 | – |

**Overall risk:** CRITICAL *if deployed anywhere reachable* (because of TD-003). Otherwise MEDIUM.
**Recommendation:** CONDITIONAL. The code is careful and the security-sensitive primitives are correct. But `main` now exposes unauthenticated endpoints that mint client credentials and grant roles in every environment. Gate them (TD-003, step 1) before any deployment, and resolve F-1 before writing the seeder.

**Key metrics**

- Files analysed: 123 source files in the diff. HIGH-risk logic was read in full. Response DTOs and read-only `GetAll` services were only scanned. Generated migration files were skimmed.
- Test coverage of changed code: **0%**. The repo has no test projects.
- Security regressions detected: **0**. All removed checks were moved into the domain and made stricter (`User.SetFirstName` / `SetLastName`), not dropped.
- High blast-radius changes: `ProblemDetailsExceptionHandler` (every endpoint, 45 actions) and `ModifiedAtInterceptor` (every `SaveChanges`)

**What is done well** (verified, not assumed):

- `SecretGenerator` uses 32 bytes from `RandomNumberGenerator` with Base64Url encoding. `Sha256SecretHasher.Verify` checks the decoded length and uses `CryptographicOperations.FixedTimeEquals`. A fast hash is justified for 256-bit random secrets.
- The plaintext secret is returned once, with `Cache-Control: no-store` on both endpoints that return it (`ClientsController.cs:76`, `:379`). It is never logged (`ClientLog` logs only Ids), and `GetClientResponse` never includes the hash.
- `RevokeClientSecretService` resolves the secret through `client.Secrets`, so a secret Id belonging to another client returns 404. There is no IDOR across clients.
- `UserRepository.GetUsers` escapes `\`, `%` and `_` and passes an explicit escape character to `ILIKE`.
- `HealthReportWriter` replaces exception messages with a fixed string.
- ISS-001 to ISS-004 are fixed as described.

---

## 2. What Changed

| Area | Files | Risk | Notes |
|---|---|---|---|
| `Api/Controllers/*` (5 controllers, 45 actions) | 5 new, 5 deleted | **HIGH** | All new public surface, with no authn/authz (TD-003) |
| `Infrastructure/Security/*` | 2 new | **HIGH** | Secret generation and hashing |
| `Core/Application/Clients/*` | 27 new | **HIGH** | Credential issuance and revocation, redirect URIs |
| `Core/Application/Users/*` | 20 new/changed | **HIGH** | Role assignment, admin guard, lifecycle, security stamp |
| `Core/Application/Roles/*`, `Scopes/*` | 25 new | MEDIUM | System-object protections |
| `Api/ExceptionHandling/ProblemDetailsExceptionHandler.cs` | 1 new | MEDIUM | Global error → client message mapping |
| `Api/HealthChecks/*`, `Infrastructure/HealthChecks/*` | 5 new | MEDIUM | Info disclosure (TD-003) |
| `Core/Domain/Entities/*` | 4 changed | MEDIUM | Invariants moved into the domain |
| `Infrastructure/Persistence/*` | 12 new/changed | LOW–MEDIUM | Repositories, interceptor, one migration |
| `docs/*` | 14 | LOW | Plan, issues, tech debt |

---

## 3. Findings

### 🔴 KNOWN / ACCEPTED – TD-003: every admin endpoint is unauthenticated, and `main` now ships them

**Files:** `src/Authora.Api/Program.cs:39-52` (no `AddAuthentication` / `AddAuthorization`, no `[Authorize]` anywhere in `src/`)
**Status:** Documented in `docs/tech-debt/td-003.md`. Not a new finding, but the merge raises its importance.

The PR turns a five-endpoint user CRUD into 45 endpoints. These include `POST /api/clients` and `POST /api/clients/{id}/secrets`, which return working client credentials, and `PUT /api/users/{id}/roles/{roleId}`. All are mapped in every environment.

**Attack scenario (anyone with network reach):**
1. `POST /api/clients` with `{"name":"x","type":"Confidential","redirectUris":["https://attacker.example/cb"],"scopeIds":[...all...]}` returns a `client_id` and secret.
2. `POST /api/users`, then `PUT /api/users/{id}/roles/{adminRoleId}` creates an administrator.
3. Once the OAuth runtime exists, both are live credentials.

**Recommendation:** Do step 1 of TD-003 now, not later. The cheapest version is to map the admin controllers only in Development:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapControllers();
}
```

Add an `X-Api-Key` check later if a shared environment is needed.

---

### 🟡 MEDIUM F-1: system-object protections are unreachable, and reserved names can be squatted

**Files:**
- `src/Authora.Core/Application/Roles/Create/CreateRoleService.cs:33`
- `src/Authora.Core/Application/Scopes/Create/CreateScopeService.cs:33`
- `src/Authora.Core/Domain/Entities/Role.cs:79` (`IsAdministrator => IsSystem && Name == "admin"`)

**Commits:** `bf4b114` (roles), `1d9a3d2` (scopes)
**Blast radius:** 6 guards depend on `IsSystem` or `IsAdministrator`: `AdministratorGuard`, `RemoveRoleService`, `RevokePermissionService`, `DeleteRoleService`, `UpdateRoleService` and `DeleteScopeService`.
**Test coverage:** NO

**Description:**
No code path creates a Role or Scope with `IsSystem = true`. The `IdentitySeeder` (step 3 of `identity-access-plan.md`) was not built, there is no `HasData`, and both create services call `Create(..., isSystem: false)`. So in any database created by this code:

- `Role.IsAdministrator` is always `false`. **The last-administrator guard (`AdministratorGuard`), the "admin role cannot lose permissions" rule and the "system role cannot be renamed or deleted" rules never trigger.** The manual checks in ISS-001 must have used a hand-inserted row.
- Meanwhile, the API will create a **non-system** Role named `admin` and **non-system** Scopes named `openid`, `profile`, `email` and `offline_access`. All of these pass the name validation.

**Attack scenario (squatting before the seeder ships):**
1. Before the seeder exists, someone (an operator, or anyone given TD-003) calls `POST /api/roles {"name":"admin",...}` and `POST /api/scopes {"name":"openid",...}`.
2. The seeder later runs. A typical idempotent seeder does "if a role named `admin` exists, skip". It then adopts the squatted row with `IsSystem = false`.
3. Result: the "administrator" Role can be deleted (`DeleteRoleService` only blocks `IsSystem`), stripped of permissions and renamed. `AdministratorGuard` never protects it. `openid` can be deleted with `?force=true`, which silently removes it from every Client. Alternatively, a seeder that inserts unconditionally fails at startup on the unique index on `Name`.

**Recommendation:**
- Reserve system names in the create paths now: reject `Role.AdministratorName` and the standard OIDC scope names in `CreateRoleService` and `CreateScopeService` with a 409. Expose the reserved list as constants next to `Role.AdministratorName`.
- When writing the seeder, match on name **and** require `IsSystem`. If a non-system row with a reserved name exists, fail loudly instead of adopting it.
- Until the seeder exists, note in TD-004 and in the plan that the admin guards are inert.

---

### 🟡 MEDIUM F-2: the last-administrator check counts administrators who cannot actually sign in

**Files:**
- `src/Authora.Core/Application/Users/AdministratorGuard.cs:33`, `:45`
- `src/Authora.Infrastructure/Persistence/Repositories/UserRepository.cs:99` (`CountUsersWithRoleWhoCanSignIn`)

**Commit:** `acdc7fe`
**Blast radius:** 3 callers (`DeactivateUserService`, `DeleteUserService`, `RemoveRoleService`). LOW count, but this is the account-recovery invariant.
**Test coverage:** NO

**Description:**
"Can sign in" is implemented as `State is Active or Inactive`. It ignores two cases:

- `PasswordHash == null`. Per TD-001, **every User created through the API has no password**, and TD-001 states such a User "cannot sign in".
- An active lockout (`LockoutEndAt > now`). This is a minor case because it is temporary.

**Attack or mistake scenario (once the system admin role exists, see F-1):**
1. A real admin A (bootstrapped, has a password) creates User B through the API. B has no password and is given the admin role.
2. Someone deletes or deactivates A, or removes A's admin role. The guard counts B as an administrator who can sign in and allows it.
3. No one can sign in as an administrator. Recovery needs direct database access. This is the exact outcome the guard exists to prevent.

This is not a race (TD-004). It happens with sequential requests.

**Recommendation:**

```csharp
.Where(user => user.State == UserState.Active || user.State == UserState.Inactive)
.Where(user => user.PasswordHash != null)   // TD-001: no password → cannot sign in
```

If the invite flow is chosen for TD-001, use whatever "has a usable credential" means then. Update the remark on `CountUsersWithRoleWhoCanSignIn` accordingly.

---

### 🟢 LOW F-3: `ArgumentException` → 400 echoes framework exception messages (same issue class as ISS-001)

**File:** `src/Authora.Api/ExceptionHandling/ProblemDetailsExceptionHandler.cs:40`
**Commit:** `2eeec04` (the ISS-001 fix)
**Blast radius:** global (all 45 actions)

**Description:**
ISS-001 stopped mapping `InvalidOperationException` because the framework throws it for bugs. `ArgumentException` has the same problem. The pattern also matches every subtype thrown anywhere in the stack, such as `ArgumentNullException`, `ArgumentOutOfRangeException`, and the `ArgumentException` that `Dictionary`/`ToDictionary` throw for duplicate keys (e.g. inside `ModifiedAtInterceptor`). EF Core and Npgsql also throw it (e.g. `Cannot write DateTime with Kind=Unspecified…`). When one of these is a bug, the caller gets a **400** that blames their request, with internal type, parameter or key details in `detail`, and nothing is logged as a server error.

**Recommendation:** Give request and domain validation its own type (e.g. `DomainValidationException : ArgumentException`, thrown by the entity setters and `GrantTypeNames.Parse`), and map only that type to 400. Let any other `ArgumentException` fall through to 500. Record it as ISS-005.

---

### 🟢 LOW F-4: changing a User's Email does not rotate the `SecurityStamp`

**File:** `src/Authora.Core/Application/Users/Update/UpdateUserService.cs:47`
**Commit:** `acdc7fe`

Email is the sign-in identifier and will be the reset-token target (US-010). Deactivate, delete, revoke-sessions and `SetPasswordHash` all rotate the stamp. An Email change does not, so sessions and any future reset tokens issued for the old address stay valid. ASP.NET Identity rotates the stamp on email change for this reason. **Fix:** call `user.RotateSecurityStamp()` when `NormalizedEmail` changes, ideally inside `User.SetEmail`. Today the impact is nil (no sessions exist), so it can also be added to TD-006.

---

### 🟢 LOW F-5: `RegisterClientRequest` lists are unbounded, and redirect URIs have no length cap there

**File:** `src/Authora.Core/Application/Clients/Register/RegisterClientRequest.cs:28-29`

`AddRedirectUriRequest` caps a URI at 2048 characters, which matches the column. The `RedirectUris` list on register has no per-item cap. `RedirectUris`, `ScopeIds` and `GrantTypes` also have no count limit.

- A URI over 2048 characters passes `RedirectUri.Create` and fails at the database with Postgres `22001`, which surfaces as an unmapped `DbUpdateException` → 500.
- A body with tens of thousands of `ScopeIds` turns into one large `IN (...)` query, bounded only by Kestrel's 30 MB body limit.

**Fix:** enforce the length in `RedirectUri.Create` (`MaxLength = 2048`, shared with the configuration), and add `[MaxLength(n)]` to the three lists.

---

### ℹ️ INFO

- **I-1:** `RedirectUri.Create` (baseline code from `9324868`, which this PR makes reachable over HTTP) only rejects *leading and trailing* whitespace (`RedirectUri.cs:50`), but its message says "can't contain whitespaces". `https://app.example.com/cb x` is accepted. Exact-string matching means this is not an open-redirect bypass. It does allow odd registrations, so reject any `char.IsWhiteSpace` or control character.
- **I-2:** `CreateClientSecretService` (`:44`) issues secrets for **Disabled** clients. This is harmless as long as `/token` checks `Status`, but consider a 409 so credentials aren't minted for a client that was disabled on purpose (US-014).
- **I-3:** `ClientSecret.CreatedAt` (`ClientSecret.cs:34`), and `CreatedAt` on the other entities, use `DateTime.UtcNow`. Expiry checks use the injected `TimeProvider`. This inconsistency will make expiry tests flaky. Pass `now` into `Client.AddSecret`.

**Out of scope (baseline, unchanged by this PR):** `appsettings.json:10` commits a database password (`Password=12345`). It is local-dev only, but it belongs in user-secrets or environment variables, with `appsettings.json` holding no credential.

---

## 4. Test Coverage Analysis

**Coverage: 0%.** There is no test project in `src/Authora.slnx`. Verification of ISS-001 to ISS-004 was manual.

| Untested logic | Risk | Why it matters |
|---|---|---|
| `Sha256SecretHasher.Verify` | HIGH | Auth primitive. The length and Base64Url rejection paths are untested. |
| `AdministratorGuard` and its 3 callers | HIGH | Account-recovery invariant (F-2), currently inert (F-1) |
| `ModifiedAtInterceptor` | MEDIUM | Non-trivial EF metadata walk, global blast radius |
| `RedirectUri.Create` | HIGH | Future open-redirect boundary |
| `GrantTypeNames.Parse` / `Client.SetAllowedGrantTypes` | MEDIUM | Grant-type policy |
| `ProblemDetailsExceptionHandler` mapping | MEDIUM | Info disclosure (F-3, ISS-001) |

Per the methodology, complex new logic without tests raises severity. That is why F-1 and F-2 are rated MEDIUM rather than LOW. Pure domain tests (`User`, `Role`, `Client`, `RedirectUri`, `Sha256SecretHasher`) are cheap and need no database.

---

## 5. Blast Radius

| Changed unit | Callers / reach | Risk | Priority |
|---|---|---|---|
| `ProblemDetailsExceptionHandler` | every request (45 actions) | MEDIUM | P1 |
| `ModifiedAtInterceptor` | every `SaveChanges` (all repositories) | LOW–MEDIUM | P2 |
| `DeletedUserGuard.EnsureNotDeleted` | 5 services | MEDIUM | P2 |
| `AdministratorGuard.EnsureNotLastAdministratorAsync` | 3 services | HIGH | P2 |
| `ClientSecretIssuer.Issue` | 2 services | HIGH | P2 |
| `ISecretHasher` / `ISecretGenerator` | 1 now; `/token`, reset tokens and auth codes later | HIGH | P2 (will grow) |

---

## 6. Historical Context

- **Removed code:** the five per-action user controllers (`CreateUserController.cs` etc., removed in `d769439`) contained no authorization, so nothing was lost there. The removed `ArgumentException.ThrowIfNullOrWhiteSpace` calls in `User.Create` were moved into `ValidateName`, which also adds a length check (ISS-002). The removed `throw new Exception("User not found")` was replaced with `NotFoundException`. **No security check was removed without a stronger replacement.**
- **Fix commits in range:** `2eeec04` (ISS-001/002), `b8302c5` (ISS-003), `3ed16a5` (ISS-004). All were checked. F-3 is a residual of the ISS-001 class that the fix did not cover.
- **Regressions:** none found.

---

## 7. Recommendations

### Immediate (before any non-local deployment)
- [ ] TD-003 step 1: map the admin controllers only in Development, or add an API key
- [ ] F-1: reserve `admin` and the standard OIDC scope names in the create services

### Before the seeder or bootstrap admin (plan steps 3 and 7)
- [ ] F-1: seeder must match `IsSystem`, not just `Name`
- [ ] F-2: exclude password-less Users from `CountUsersWithRoleWhoCanSignIn`
- [ ] Add a test project. Cover the domain entities, `Sha256SecretHasher`, `RedirectUri` and `AdministratorGuard` first.

### Before the OAuth runtime
- [ ] F-3: dedicated validation exception type; stop mapping all `ArgumentException`s
- [ ] F-4: rotate `SecurityStamp` on Email change
- [ ] F-5, I-1: bound redirect-URI length and charset, and request list sizes
- [ ] I-2, I-3: decide on secrets for disabled clients; inject `now` for `CreatedAt`

---

## 8. Analysis Methodology

**Strategy:** FOCUSED (MEDIUM codebase, 164 files; diff 140 files)

**Scope**
- HIGH-risk files: 100% read in full (controllers' routing and binding, security, all Client, User and Role services, entities, repositories, DI, `Program.cs`)
- MEDIUM-risk files: 100% read (scopes, exception handler, health checks, interceptor, configurations)
- Generated EF migration designer and snapshot files: diff skimmed only
- Docs: plan, ISS-001 to ISS-004 and TD-001 to TD-006 read, to avoid re-reporting tracked items

**Techniques:** baseline vs. head diff; review of removed lines for dropped checks; `git log` on the security-relevant files; caller counts by grep; manual adversarial walk-through of each HIGH-risk flow (credential issuance, role assignment, admin guard, redirect-URI registration, error mapping)

**Limitations**
- Nothing was executed. The findings come from static reading and were not reproduced against a running instance or database.
- The behaviour of NuGet dependencies (EF Core 10, Npgsql, Swashbuckle) was not audited.
- The OAuth runtime (`/authorize`, `/token`) does not exist yet. Several findings (F-2, F-4, I-1, I-2) are rated by their impact once it does.

**Confidence:** HIGH for the analysed scope. MEDIUM for forward-looking impact ratings.

---

## Appendix: commits in range

| Commit | Message |
|---|---|
| `d769439` | chore: move user related endpoints into one controller |
| `0a7d8d0` | docs: add identity access endpoint implementation plan |
| `acdc7fe` | feat: add user endpoints |
| `bf4b114` | feat: add role endpoints |
| `9dfc879` | feat: add permission endpoints |
| `1d9a3d2` | feat: add scope endpoints and IsSystem field to database |
| `2fbb649` | docs: add tech-debt folder |
| `0e2e0da` | feat: add client endpoints, SecretGenerator and SecretHasher |
| `130e916` | feat: add health checks |
| `2eeec04` | fix: iss-001 and iss-002 |
| `b8302c5` | fix: iss-003 |
| `3ed16a5` | fix: iss-004 |
| `8813cc5` | docs: add tech debt 3 - 6 |
