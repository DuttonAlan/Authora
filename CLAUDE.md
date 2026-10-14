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

# Run the API locally (Development: Swagger UI enabled, connection string and bootstrap admin in appsettings.Development.json)
dotnet run --project src/Authora.Api

# EF Core migrations (Infrastructure holds the DbContext, Api is the startup project)
dotnet ef migrations add <Name> --project src/Authora.Infrastructure --startup-project src/Authora.Api
dotnet ef database update --project src/Authora.Infrastructure --startup-project src/Authora.Api
dotnet ef migrations has-pending-model-changes --project src/Authora.Infrastructure --startup-project src/Authora.Api
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
- **Authora.Infrastructure** – `AuthoraContext` with `IEntityTypeConfiguration`s, repositories, migrations, hashing/key loading, `IdentitySeeder`, and the `ExpiredDataCleanupService` hosted service. Two PostgreSQL schemas: `identity` (users, clients, roles, permissions, scopes) and `auth` (sessions, codes, grants, tokens). Concurrency-sensitive rules use transactions with `pg_advisory_xact_lock` (`AdvisoryLockKeys`).
- **Authora.Api** – thin controllers that call Core services, Razor Pages for the browser flows (`/account/login`, `/account/logout`, `/oauth/authorize` consent), and `Program.cs` for wiring.

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

Only integration tests exist (`tests/Authora.Api.IntegrationTests`). `AuthoraApiFactory` (`WebApplicationFactory<Program>`) starts a PostgreSQL container, migrates it, writes temporary signing keys, runs in environment `Testing`, and offers helpers to create users, administrators, sessions, access tokens and public clients directly in the database. `AuthorizationCodeFlow` drives the full browser flow. Test-wide build settings live in `tests/Directory.Build.props`.

## Docs

- `docs/oauth-core.md`, `docs/openid-connect.md` – endpoint behavior, token model, configuration settings and security rules. Keep them in sync when changing those flows.
- `docs/Stories.md` – user stories (US-xxx) by phase; commits and docs reference these IDs.
- `docs/tech-debt.md` – deliberate shortcuts (TD-xxx). Resolved entries are kept with a status and resolution; IDs are never reused (check the file for the next free ID).
