# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Authora is a self-hosted IAM platform: an OAuth 2.1 authorization server and OpenID Connect provider with an admin REST API, built on .NET 10, ASP.NET Core and EF Core with PostgreSQL.

## Git

Never run `git commit`, `git pull` or `git push`. Leave all changes uncommitted in the working tree; the user handles git.

## Commands

```sh
dotnet build Authora.slnx                      # warnings are errors (TreatWarningsAsErrors + EnforceCodeStyleInBuild)
dotnet test --solution Authora.slnx            # all tests; needs Docker (Testcontainers starts postgres:17-alpine)

# Single test class / method (xunit v3 on Microsoft.Testing.Platform, see global.json)
dotnet test --project tests/Authora.Api.IntegrationTests --filter-class "*IdTokenTests"
dotnet test --project tests/Authora.Api.IntegrationTests --filter-method "*Details_WithPermission_ReturnsChecks"
dotnet test --project tests/Authora.Api.IntegrationTests --list-tests

# Run the API locally (Development: Swagger UI enabled, connection string and bootstrap admin in appsettings.Development.json).
# Use the https profile: sign-in needs HTTPS. The admin interface is then at https://localhost:7178/admin/
dotnet run --project src/Authora.Api --launch-profile https

# EF Core migrations (Infrastructure holds the DbContext, Api is the startup project)
dotnet ef migrations add <Name> --project src/Authora.Infrastructure --startup-project src/Authora.Api
dotnet ef database update --project src/Authora.Infrastructure --startup-project src/Authora.Api
dotnet ef migrations has-pending-model-changes --project src/Authora.Infrastructure --startup-project src/Authora.Api

# Containers: two targets of one Dockerfile (runtime = server on HTTP 8080, migrator = EF migration bundle)
docker build --target runtime -t authora .
docker build --target migrator -t authora-migrator .
docker compose up --build   # local stack on https://localhost:8443; needs .env and secrets/, see docs/tutorials/getting-started.md
```

There is no separate lint step: analyzers and `.editorconfig` style rules run on build and fail it. CI (`.github/workflows/build.yml`) runs restore, Release build, tests and `has-pending-model-changes`, so every model change needs a migration.

Local setup: the API does **not** apply migrations at startup (run `database update` first), and it needs an RSA signing key for ID Tokens at `Authora:Oidc:SigningKeyPath` (`signing-key.pem` in Development; `*.pem` is git-ignored):

```sh
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out src/Authora.Api/signing-key.pem
```

## Architecture

Three projects with a strict dependency direction: `Authora.Api` → `Authora.Core` ← `Authora.Infrastructure`.

- **Authora.Core** has no EF/ASP.NET dependency.
  - `Domain/` – rich entities (`User`, `Client`, `Role`, `Grant`, tokens, …) created through static `Create(...)` factories and changed through methods (`client.Enable()`, `user.AssignRole(...)`). Invalid input throws `DomainValidationException`. Timestamps are UTC (`UtcGuard`).
  - `Application/<Area>/<UseCase>/` – one service class per use case (`EnableClientService`, `IssueTokenService`, …) with a single `ExecuteAsync`, plus its `*Request`/`*Response` records. Each new service must be registered in `Core/ServiceCollectionExtensions.cs`.
  - `Abstractions/` – repository and security interfaces (`I*Repository`, `ISecretHasher`, `ISigningKeyProvider`, `ITransaction`) implemented in Infrastructure.
- **Authora.Infrastructure** – `AuthoraContext` with `IEntityTypeConfiguration`s, repositories, migrations, hashing/key loading (`PemSigningKeyProvider`, reloaded by `SigningKeyReloadService`), `IdentitySeeder`, `AuditLog`, and the `ExpiredDataCleanupService` hosted service. Three PostgreSQL schemas: `identity` (users, groups, clients, roles, permissions, scopes), `auth` (sessions, codes, grants, tokens) and `audit` (audit events). Concurrency-sensitive rules use transactions with `pg_advisory_xact_lock` (`AdvisoryLockKeys`).
- **Authora.Api** – thin controllers that call Core services, Razor Pages for the browser flows (`/account/login`, `/account/logout`, `/oauth/authorize` consent), the admin interface as static files in `wwwroot/admin` (plain JavaScript modules, no build step), `SecurityHeadersMiddleware`, and `Program.cs` for wiring.

### Admin interface

`wwwroot/admin` is a browser app with a backend for frontend (`AdminUiController`, `Api/AdminUi/`): the server runs Authora's own Authorization Code flow with PKCE as the seeded first-party Public Client `Authora Admin UI` (Redirect URI `{Issuer}/admin/callback`, handled by the server; first-party Clients skip the consent screen) and keeps the tokens in an encrypted `HttpOnly` cookie. The JavaScript never sees a token: it calls `/api/*` with that cookie and the header `X-Authora-Admin: 1`, and `AdminUiTokenMiddleware` turns the cookie into the Bearer token, so the API checks it like that of any other Client. `GET /api/me` returns the User's Permissions so the UI can hide what they may not do; the API still checks every Permission. Its CSP forbids inline scripts and styles (no `style="…"` attributes or `innerHTML`; build DOM with `h()` from `js/dom.js`). See `docs/explanation/admin-interface.md`.

### Audit log

Every state-changing service records an event through `IAuditLog` (`AuditEventTypes`, `AuditEntry.ForUser(...)` etc.). `Record` stages the event in the request's DbContext right before the repository saves, so it is committed atomically with the change; call it after early returns for no-ops. `RecordNowAsync` saves immediately in its own DbContext (failed sign-ins, token reuse). The actor (User, Client, IP) comes from `IAuditActorAccessor`. Never put secrets in `Details`. New state-changing services must record an event. `AuditChainSealer` (background, Infrastructure) seals committed events into the insert-only hash chain `audit.chain`; `AuditChainHash` defines the hashed fields, so a new `AuditEvent` field must be added there (which breaks existing chains).

### Authentication and authorization

Two authentication schemes, deliberately separated:
- **Session cookie** (`SessionAuthenticationHandler`) – only for the Razor Pages (sign-in and authorize).
- **Access Token** (`AccessTokenAuthenticationHandler`) – Authora's own opaque tokens; the only scheme the admin API accepts.

Admin controllers carry `[Authorize(AuthorizationPolicies.AdminApi)]` (token must have the `authora.admin` scope) plus a per-action `[Authorize(PermissionNames.<Area>.<Action>)]`. Every name in `PermissionNames.All` becomes a policy and is seeded by `IdentitySeeder` at startup (along with the administrator role, system scopes, OIDC scopes and the first administrator from `Authora:Bootstrap`). Adding a permission = add it to `PermissionNames`; seeding and policy registration follow automatically.

### Errors

- Admin API: services throw `NotFoundException` (404), `ConflictException` (409) or `DomainValidationException` (400); `ProblemDetailsExceptionHandler` maps them, plus unique-violation and concurrency `DbUpdateException`s, to 409. `InvalidOperationException`/`ArgumentException` are intentionally **not** mapped and stay 500 — don't throw them for business-rule violations.
- OAuth endpoints (`/oauth/*`): throw `OAuthException` with codes from `OAuthErrors`; responses use the RFC `{"error", "error_description"}` shape, not ProblemDetails.

### Conventions

- Every `.cs` file starts with the MIT copyright header (`file_header_template` in `.editorconfig`, IDE0073).
- Public members need XML doc comments (`GenerateDocumentationFile`; they also feed Swagger). Controllers document every response code with `<response>` and `[ProducesResponseType]`.
- Logging uses source-generated `[LoggerMessage]` extensions in per-area `*Log.cs` classes with unique EventIds per area (e.g. `ClientLog` uses 40xx). Never log secrets, only their Ids.
- Options classes are bound in `Program.cs` with `ValidateOnStart()` and validated by `IValidateOptions<T>` validators registered via `TryAddEnumerable`.
- Secrets, codes and tokens are stored only as SHA-256 hashes (`ISecretHasher`); use `TimeProvider` for the current time.

### Tests

Only integration tests exist (`tests/Authora.Api.IntegrationTests`). `AuthoraApiFactory` (`WebApplicationFactory<Program>`) starts a PostgreSQL container, migrates it, writes temporary signing keys, runs in environment `Testing` with HTTPS clients (`AuthoraApiFactory.BaseAddress`; the antiforgery cookie is only issued over HTTPS, so clients created with own options must set it), and offers helpers to create users, administrators, sessions, access tokens and public clients directly in the database. `AuthorizationCodeFlow` drives the full browser flow. Test-wide build settings live in `tests/Directory.Build.props`.

## Docs

The documentation follows Diátaxis (`docs/README.md` is the index). Keep it in sync with the code: a change to endpoints, settings, events or flows updates the matching reference page and any affected guide.

- `docs/tutorials/` – learning by doing (Docker Compose quickstart, first application). The commands in them were run against the real container; re-run them after changing the flows they show.
- `docs/how-to/` – task guides for operators and integrators (production deployment, reverse proxy, migrations, key rotation, backup, connecting applications and APIs).
- `docs/reference/` – exact facts: `configuration.md` (every setting, default and limit), `oauth-endpoints.md`, `openid-connect.md`, `admin-api.md` (endpoints and Permissions), `audit-events.md`, `log-events.md`, `http-security.md`, `health-checks.md`, `standards.md` (supported specs and limitations).
- `docs/explanation/` – design and reasoning: architecture, access model, tokens and sessions, security model, admin interface, audit log integrity.
- `CHANGELOG.md` – add an entry under *Unreleased* for every user-visible change. Bugs, planned work and shortcuts are tracked as GitHub issues, not in files.
