# Contributing to Authora

Thank you for your interest in improving Authora. This guide explains how to propose changes and what a good pull request looks like.

By taking part, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Ways to contribute

- **Report a bug:** open an [issue](https://github.com/DuttonAlan/Authora/issues/new/choose) with the bug template.
- **Report a vulnerability:** not in an issue. Follow [SECURITY.md](SECURITY.md).
- **Suggest a feature:** open an issue with the feature template first. Authora deliberately stays small, so it is worth agreeing on a feature before you build it.
- **Improve the documentation:** pull requests are welcome directly. The docs follow [Diátaxis](https://diataxis.fr/); see [docs/README.md](docs/README.md) for which part a change belongs in.
- **Fix a bug or build an agreed feature:** see below.

## Development setup

Prerequisites:

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Docker (the tests start PostgreSQL with Testcontainers)
- PostgreSQL 17 to run Authora locally, e.g. in Docker

[Run Authora from source](docs/how-to/run-from-source.md) shows how to create the database, a signing key and start the server.

## Build and test

```sh
dotnet build Authora.slnx
dotnet test --solution Authora.slnx
```

Run a single test class or method:

```sh
dotnet test --project tests/Authora.Api.IntegrationTests --filter-class "*IdTokenTests"
dotnet test --project tests/Authora.Api.IntegrationTests --filter-method "*Details_WithPermission_ReturnsChecks"
```

There is no separate lint step. Analyzers and the `.editorconfig` style rules run during the build, and **warnings are errors**, so a clean build means the style is right.

CI runs the build in Release, the tests, and a check that every model change has a migration. Your pull request must pass it.

## Project structure

```
src/Authora.Core            domain entities and use cases; no ASP.NET Core or EF Core
src/Authora.Infrastructure  EF Core, PostgreSQL, keys, background services
src/Authora.Api             controllers, Razor Pages, admin interface, Program.cs
tests/Authora.Api.IntegrationTests
docs/                       documentation (Diátaxis)
```

Dependencies point inward only: `Api → Core ← Infrastructure`. [Architecture](docs/explanation/architecture.md) explains the design.

## Conventions

**Code**

- Every `.cs` file starts with the MIT license header (enforced by the build):
  ```csharp
  // Copyright(c) Alan Dutton.
  // Licensed under the MIT License.
  // See the LICENSE file in the project root for license information.
  ```
- Public members need XML documentation comments; they also feed Swagger. Controllers document every response code with `<response>` and `[ProducesResponseType]`.
- Domain entities are created through static `Create(...)` factories and changed through methods. Invalid input throws `DomainValidationException`.
- One service class per use case in `Core/Application/<Area>/<UseCase>/`, registered in `Core/ServiceCollectionExtensions.cs`.
- Use `TimeProvider` for the current time; timestamps are UTC.
- Logging uses source-generated `[LoggerMessage]` methods in per-area `*Log.cs` classes, with unique Event Ids per area.

**Security rules**

- Never log or audit secrets, passwords, codes or tokens; only their Ids.
- Store secrets, codes and tokens only as hashes (`ISecretHasher`).
- Admin API errors: throw `NotFoundException`, `ConflictException` or `DomainValidationException`. `InvalidOperationException` and `ArgumentException` mean a bug and become `500`.
- OAuth endpoints throw `OAuthException` with codes from `OAuthErrors`.
- Every state-changing use case records an audit event through `IAuditLog`.
- New admin actions need a Permission: add it to `PermissionNames`; seeding and policies follow automatically.

**Database**

- Every model change needs a migration:
  ```sh
  dotnet ef migrations add <Name> --project src/Authora.Infrastructure --startup-project src/Authora.Api
  ```
- Authora never migrates at startup. Note in the changelog if a migration is not compatible with the previous version running.

**Admin interface**

- Plain JavaScript modules, no build step, no third-party scripts.
- The Content Security Policy forbids inline scripts and styles: no `style="…"`, no `innerHTML`. Build DOM nodes with `h()` from `js/dom.js`.

**Tests**

- Authora has integration tests only, in `tests/Authora.Api.IntegrationTests`. `AuthoraApiFactory` starts PostgreSQL and the app, and offers helpers to create Users, administrators, Sessions, tokens and Clients. `AuthorizationCodeFlow` drives the full browser flow.
- Add a test for every bug fix and every new rule, especially security rules.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep the change focused: one bug or one feature per pull request.
3. Add or update tests.
4. Update the documentation when behavior, configuration or endpoints change, and add an entry under *Unreleased* in [CHANGELOG.md](CHANGELOG.md).
5. Make sure `dotnet build` and `dotnet test` pass.
6. Open the pull request and fill in the template.

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/), e.g. `fix: reject reused refresh tokens for disabled clients` or `docs: explain key rotation`.

A maintainer reviews every pull request. Security-relevant changes get an especially careful review, so expect questions.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE.txt).
