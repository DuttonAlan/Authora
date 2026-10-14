# External Identity Providers (Draft)

Status: **brainstorm / proposal**. Nothing in this document is implemented yet. Once it is agreed, the stories below move to [Stories.md](Stories.md) as Phase 8.

## Goal

Users can sign in to Authora, and optionally sign up, with an account from an external identity provider, starting with **Google**. Administrators configure these providers in the admin interface, much like "Sources" in Authentik or "Identity Providers" in Keycloak.

Authora stays the **only issuer** for its own Clients. Applications keep talking only to Authora. The external provider is just one more way to prove who the user is on the sign-in page. After that, Authora starts its normal Session and issues its own codes, Access Tokens and ID Tokens, with its own Roles and Permissions.

```
App ──/oauth/authorize──▶ /account/login ──"Continue with Google"──▶ Google
                              ▲                                        │
                              └──── /account/external/{slug}/callback ◀┘
                                     ID Token validated
                                     → find / link / provision the Authora User
                                     → Authora Session → back to /oauth/authorize
```

## Decisions so far

| Question | Decision |
|---|---|
| Target audience | Open-source, general-purpose IAM. Instances range from a single company with Google Workspace to public apps that accept any Google account, so **sign-up behavior must be configurable per provider**. |
| Who configures providers | **Administrators**, in the admin interface (stored in the database), not only in `appsettings`. |
| Linking and passwords | Administrators decide, through per-provider policies (see [Policies](#provider-policies)). |
| Scope of the first version | A generic **OpenID Connect provider**, with **Google** as a preset. Other providers (Microsoft Entra ID, GitLab, Keycloak, Okta and so on) work through the same generic type. GitHub uses plain OAuth 2.0 without OIDC, so it is out of scope for now. |

## Effort: config file or admin-managed

Here is the difference in effort between the two options:

| | Config only (`appsettings`) | Admin-managed (database + admin UI) |
|---|---|---|
| Provider definition | Options class + validator | Entity, configuration, migration, CRUD services, controller, admin UI view |
| Client Secret of the provider | Comes from config / environment / secret store | Must be stored **encrypted, not hashed**, because Authora sends it to the provider. Authora currently only hashes secrets, so this is new (see [Secret storage](#secret-storage)). |
| ASP.NET Core authentication schemes | Registered once at startup | Registered and removed **at runtime** (`IAuthenticationSchemeProvider.AddScheme/RemoveScheme`) and their cached options invalidated (`IOptionsMonitorCache<OpenIdConnectOptions>.TryRemove`) when an admin edits a provider. With several instances, the other instances must also pick up the change (reload on a version stamp, or resolve options per request). |
| Audit, Permissions | Not needed for the config | New events and Permissions for provider changes |
| Rough size | ~1 story | ~3 stories |

The sign-in flow, account linking, provisioning, the `ExternalLogin` entity and the login page are **the same in both options**, and they are most of the work.

**Recommendation:** build it admin-managed from the start. For an Authentik-like product, administrators will expect it, and adding it after a config-based version means building the provider model twice. For automated deployments, a provider can also be **seeded from config** on startup, the same way `Authora:Bootstrap` seeds the first administrator. This works only when the provider does not exist yet, and the database is the source of truth after that.

## Domain model

### `ExternalIdentityProvider` (schema `identity`)

| Field | Notes |
|---|---|
| `Id` | `Guid.CreateVersion7()` |
| `Slug` | URL-safe and unique, e.g. `google`. Used in the callback path, so it cannot be changed after the first sign-in (otherwise the redirect URI registered at Google breaks). |
| `DisplayName` | Button text: "Continue with {DisplayName}" |
| `Type` | `Google` or `OpenIdConnect`. `Google` fixes the authority (`https://accounts.google.com`) and adds the Google-specific checks (`hd`). |
| `Authority` | Issuer URL; discovery is loaded from `{Authority}/.well-known/openid-configuration`. HTTPS only. |
| `ClientId` | |
| `ClientSecretCiphertext` | Encrypted; never returned by the API (only "is set" / last changed) |
| `Scopes` | Default `openid email profile` |
| `Enabled` | Disabled providers are hidden on the login page, and their callbacks are rejected. |
| `DisplayOrder` | Order of the buttons |
| Policy fields | See [Provider policies](#provider-policies) |
| `CreatedAt`, `ModifiedAt` | UTC |

### `ExternalLogin` (schema `identity`)

Connects one Authora User to one account at one provider.

| Field | Notes |
|---|---|
| `Id` | |
| `UserId` | FK to `User`, cascade on User deletion |
| `ProviderId` | FK to `ExternalIdentityProvider` |
| `Subject` | The provider's `sub` claim. **Unique per provider.** This is the only matching key. |
| `Email` | Email reported at link time (for display only; never used for matching after linking) |
| `LinkedAt`, `LastUsedAt` | UTC |

Constraints: unique (`ProviderId`, `Subject`), and unique (`UserId`, `ProviderId`), so each User has at most one account per provider. That keeps the UI and unlinking simple.

### Changes to `User`

- `PasswordHash` becomes **nullable**, because a User created through Google has no password.
  - `SignInService` treats a User without a password as "invalid credentials" (with the same generic message and timing, so it does not reveal that the account is external-only).
  - Admins can still give such a User a password with the existing `SetPassword` use case, if the policy allows it.
- Possibly a `Source` / `CreatedVia` value ("Admin", "Provisioned via {provider}") for display and audit. This is optional.

## Provider policies

These are set per provider by an administrator.

### Provisioning (sign-up)

| Mode | Behavior |
|---|---|
| `Disabled` (default) | Only Users that already exist and have a link can sign in. Unknown accounts get a generic "no account" message. |
| `AllowedDomains` | An account is created on the first sign-in only if the verified email domain (and for Google Workspace the `hd` claim) is on the provider's domain list. |
| `Open` | Every account with a verified email gets a User. |

Additional settings for provisioning:
- **Default Groups** (preferred) and/or **default Roles** that new Users join. The default is none, so a new User can sign in but has no permissions.
- **Initial state**: `Active`, or `Inactive` until an admin approves the User (an approval workflow for public instances).
- **Profile sync**: copy `given_name` / `family_name` only when the User is created (default), or update them on every sign-in.

### Linking existing accounts

This decides what happens when there is no `ExternalLogin` yet, but a User with the same email exists.

| Mode | Behavior |
|---|---|
| `Never` (default) | Not linked. An admin can link the account manually, or the user signs in with the password first (see below). |
| `ConfirmWithPassword` | The user is asked for the existing Authora password once. If it is correct, the link is created. |
| `VerifiedEmail` | Linked automatically, **only** if the provider says `email_verified = true` *and* the provider is authoritative for that domain (for Google: `hd` claim present, or `@gmail.com`). The admin UI shows a warning when this is enabled for a generic OIDC provider. |

> Why the strict defaults: auto-linking by email is the most common way federated sign-in leads to account takeover. An attacker who can get an unverified or non-authoritative address at any configured provider would get the victim's Authora account, including their administrator role.

### Passwords for external Users

- **Allow password sign-in for Users provisioned through this provider**: yes or no. If no, `SetPassword` is rejected for these Users, so they can only sign in through the provider.
- Unlinking the **last** sign-in method of a User (no password and no other link) is rejected with a `ConflictException`, so a User is never locked out by accident.

## Sign-in flow

1. The login page shows one button per enabled provider. The buttons are plain `<form method="post">` buttons with antiforgery. We do **not** use Google's JS button (Google Identity Services), because it would need CSP exceptions for scripts and frames from `accounts.google.com`.
2. `POST /account/external/{slug}` → `Challenge` on the provider's OIDC scheme, with the `ReturnUrl` (still validated as a local URL) stored in the authentication properties.
3. The ASP.NET Core OpenID Connect handler does `state`, `nonce`, **PKCE**, the code exchange and **ID Token validation** (issuer, audience, signature through the provider's JWKS, expiry). We don't build any of this ourselves.
4. The handler signs in to a short-lived, separate **external cookie scheme** (`Authora.External`, about 5 minutes, `HttpOnly`, `Secure`, `SameSite=Lax`). It only carries the result to the next step. It is never accepted as an Authora Session.
5. `GET /account/external/{slug}/callback` (Razor Page) calls `ExternalSignInService` in Core:
   - Look up `ExternalLogin` by (`provider`, `sub`).
   - Otherwise apply the linking policy, then the provisioning policy.
   - Check `User.CanSignIn` (disabled or deleted Users stay blocked) and lockout.
   - Record `LastLoginAt` / `LastUsedAt`, and start the normal Authora `Session` (the same code path as password sign-in, including Session renewal).
6. The external cookie is deleted, and the user is redirected to `ReturnUrl` (usually back to `/oauth/authorize`).

If a step fails, the user returns to the login page with a generic error. The specific reason goes only to the log and the audit log.

### Claims and OIDC behavior

- `amr` in the ID Token: `pwd` for password, `fed` / `ext` for an external provider. An `idp` claim names the provider slug, so downstream apps can tell them apart.
- `prompt=login` and `max_age` from the client are forwarded to the provider (`prompt=login` / `max_age`). Otherwise, a user who is still signed in at Google would be signed in silently, without re-authenticating.
- Signing out of Authora ends only the Authora Session, **not** the Google session. This is documented in [openid-connect.md](openid-connect.md).

## Secret storage

Provider Client Secrets have to be decrypted again, so `ISecretHasher` cannot be used. Options:

1. **ASP.NET Core Data Protection** (`IDataProtector` with purpose `Authora.ExternalProviders.ClientSecret`).
   - Authora does **not configure Data Protection persistence today**. Antiforgery uses the default key ring. For this feature, the key ring must be persisted and protected (e.g. `PersistKeysToDbContext` in its own table plus `ProtectKeysWith…`). Otherwise secrets cannot be decrypted after a restart or on another instance.
   - Losing the key ring makes all stored provider secrets unreadable. Admins then have to enter them again. This needs to be documented in [operations.md](operations.md#backup-and-recovery).
2. **Envelope encryption with a dedicated key from config** (like the signing key PEM). Simpler to reason about, but we would build our own key rotation.

**Recommendation:** Option 1, with the key ring in the database. It also fixes the current gap (antiforgery and other Data Protection payloads do not survive restarts or work across instances), which may deserve its own TD entry.

## Admin API and UI

New Permissions (added to `PermissionNames`, so seeding and policies follow automatically):

- `ExternalProviders.Read`, `ExternalProviders.Create`, `ExternalProviders.Update`, `ExternalProviders.Delete`
- `Users.ManageExternalLogins` (view, link and unlink a User's external accounts)

Endpoints (sketch):

```
GET    /api/external-providers
POST   /api/external-providers
GET    /api/external-providers/{id}
PUT    /api/external-providers/{id}
POST   /api/external-providers/{id}/secret        # replace the secret; response never contains it
POST   /api/external-providers/{id}/enable|disable
DELETE /api/external-providers/{id}               # 409 while Users have links, or cascade the links (decide)
GET    /api/users/{id}/external-logins
DELETE /api/users/{id}/external-logins/{loginId}
```

Admin interface:

- New "Identity providers" view: list, create with a **Google preset** (only Client ID, Client Secret, policies), edit, enable or disable. It shows the exact **Redirect URI to register at the provider** (`{Issuer}/account/external/{slug}/callback`) with a copy button.
- "Test connection": loads the discovery document and reports errors (wrong authority, no HTTPS, missing `openid` support).
- User detail: "Linked accounts" section with provider, email and last use, plus an unlink action.

## Audit events

| Event | Recorded with | Notes |
|---|---|---|
| `ExternalProvider.Created/Updated/Deleted/Enabled/Disabled` | `Record` | Details: slug, type, changed policy fields. Never the secret. |
| `ExternalProvider.SecretChanged` | `Record` | Only the fact that it changed |
| `User.ExternalSignIn` | `Record` | Provider slug, `UserId` |
| `User.ExternalSignInFailed` | `RecordNowAsync` | Reason (unknown account, domain not allowed, User disabled, provider error). Provider subject only as a hash, if at all. |
| `User.ProvisionedExternally` | `Record` | Provider, default Groups/Roles applied |
| `User.ExternalLoginLinked/Unlinked` | `Record` | Actor: the User themselves (password confirmation) or an admin |

## Security checklist

- Match only on (`provider`, `sub`), never on email after linking.
- Require `email_verified = true` for provisioning and auto-linking. For Google, use `hd` for Workspace domain restrictions; the email domain alone is not enough.
- Callback and challenge endpoints are rate-limited like `/account/login`.
- `ReturnUrl` is validated as local on the challenge **and** the callback.
- The external cookie scheme is separate from the Session scheme and is never accepted by `AccessTokenAuthenticationHandler` or `SessionAuthenticationHandler`.
- The provider Authority must be HTTPS (except in Development / Testing).
- A disabled provider immediately stops new sign-ins. Existing Authora Sessions stay valid; admins revoke them if needed.
- Deleting a User deletes their `ExternalLogin` rows. Deactivating keeps them.
- CSP for the login page stays unchanged (no third-party scripts).
- Errors from the provider (`error`, `error_description`) are logged but not shown to the user unfiltered.

## Testing

- Integration tests can't call Google. Run a **fake OIDC provider** in the test host instead: a small in-process authority with discovery, JWKS, authorize and token endpoints that signs ID Tokens with a test key. `AuthoraApiFactory` registers it, and an `ExternalSignInFlow` helper drives it, like `AuthorizationCodeFlow`.
- Cases: existing link signs in; each provisioning mode; domain allowlist with and without `hd`; each linking mode, including `email_verified = false`; disabled User and disabled provider; replay of the callback; invalid `state` / `nonce`; open-redirect attempts via `ReturnUrl`; unlinking the last sign-in method.

## Draft stories (Phase 8 – External Identity Providers)

IDs continue after US-036.

### [ ] US-037 – Manage external identity providers
As an administrator, I want to add, configure, enable and disable external identity providers so that users can sign in with accounts they already have.

Acceptance criteria
- Providers of type Google and generic OpenID Connect can be created, edited, enabled, disabled and deleted.
- The provider Client Secret is stored encrypted and is never returned by the API.
- The Redirect URI to register at the provider is shown.
- Changes take effect without restarting Authora.
- All changes are recorded in the audit log.

### [ ] US-038 – Sign in with an external identity provider
As a user, I want to sign in with my Google account so that I do not need a separate password for Authora.

Acceptance criteria
- Enabled providers appear on the sign-in page.
- The provider's ID Token is fully validated (issuer, audience, signature, expiry, nonce, state, PKCE).
- Users are matched only by provider and subject.
- Disabled, deleted or locked-out Users cannot sign in.
- Successful sign-in starts a normal Authora Session and continues the authorization request.
- Failures show a generic message and are logged and audited.

### [ ] US-039 – Sign up through an external identity provider
As an administrator, I want to decide per provider whether new accounts are created automatically so that my instance can be closed, domain-restricted or open.

Acceptance criteria
- Provisioning modes: disabled, allowed domains, open.
- Only verified email addresses are accepted; Google Workspace domains are checked with `hd`.
- New Users can get default Groups and Roles and an initial state.
- Provisioning is recorded in the audit log.

### [ ] US-040 – Link external accounts to existing users
As a user or administrator, I want to link an external account to an existing Authora account so that the same person does not end up with two Users.

Acceptance criteria
- Linking modes per provider: never, confirm with password, verified email.
- Administrators can view and remove a User's links.
- The last sign-in method of a User cannot be removed.
- Linking and unlinking are recorded in the audit log.

### [ ] US-041 – Users without a password
As an administrator, I want Users that only sign in through an external provider so that no unused passwords exist.

Acceptance criteria
- Users can exist without a password.
- Password sign-in for such Users fails with the generic error.
- Each provider has a setting for whether its provisioned Users may get a password.

### [ ] US-042 – Persist Data Protection keys
As an operator, I want Authora's Data Protection keys to be persisted and protected so that encrypted settings and antiforgery tokens survive restarts and work across instances.

Acceptance criteria
- The key ring is stored durably and protected at rest.
- Backup and recovery of the key ring are documented.
- Losing the key ring has documented consequences (provider secrets must be entered again).

## Open questions

1. When a provider that Users are linked to is **deleted**: reject with 409 (disable instead), or cascade-delete the links?
2. Should users get a small **self-service page** (`/account`) to link or unlink their own accounts? Today there is no end-user account page; `ConfirmWithPassword` linking only happens during sign-in.
3. Should **Clients** be able to restrict providers (e.g. "this app allows only Google"), or should a client be able to skip the login page with a hint (`idp_hint` / `kc_idp_hint`-style parameter)?
4. Which providers besides Google are worth a **preset** (Microsoft Entra ID, Apple)? Apple needs a JWT-based client secret, so it is extra work.
5. How should the **external `email` change** be handled when Profile sync is on: update the Authora email automatically, or never?
