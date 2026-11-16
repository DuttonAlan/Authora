# Changelog

All notable changes to Authora are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and Authora uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Before 1.0, minor versions may contain breaking changes; they are listed under *Changed* or *Removed*.

## [Unreleased]

### Added

- Website with the documentation at <https://authora.alandutton.de/>, built from `docs/`.
- The OpenID Foundation conformance suite's Basic OP test plan runs against the container on every pull request (`.github/workflows/conformance.yml`, `tests/conformance/`); deviations from the recorded results fail the build. See [Run the conformance tests](docs/how-to/run-the-conformance-tests.md).
- PKCE can be made optional for a single Confidential Client, for OpenID Connect libraries that do not send it: `PUT`/`DELETE /api/clients/{id}/pkce-optional` (Permission `clients.update`), *Make PKCE optional* in the admin interface, `isPkceOptional` in the Client details, audit events `client.pkce-made-optional` and `client.pkce-required`, log events 4017 and 4018. PKCE stays required by default and for Public Clients, and existing Clients keep requiring it. A `code_verifier` for a code requested without `code_challenge` is rejected, so a flow that uses PKCE cannot be downgraded.
- Authorization requests sent with `POST /oauth/authorize` as a form (OpenID Connect Core 1.0, Section 3.1.2.1). They are redirected to the same request as GET, so a signed-in User is recognized. Before, they failed with `400 Bad Request`.

### Changed

- Website: the homepage states the project's status next to the call to action and shows how an application signs users in through Authora, and its icons, button contrast, 404 page and link previews are improved.

### Fixed

- Website: the diagram on the admin interface page failed to render and put an error at the bottom of the page. The architecture diagram no longer clips labels or shrinks below a readable text size, and all diagrams have text descriptions.

## [0.1.0] - 2026-11-02

First public version.

### Added

- OAuth 2.1 authorization server: Authorization Code grant with mandatory PKCE (`S256`), rotating Refresh Tokens with reuse detection, revocation (RFC 7009), introspection (RFC 7662), `iss` in authorization responses (RFC 9207).
- OpenID Connect provider: discovery, ID Tokens (`RS256`), JWKS, UserInfo, Scopes `openid`, `profile` and `email`, `nonce`, `prompt` and `max_age`.
- Signing keys from PEM files, reloaded without a restart for key rotation.
- Password sign-in with Sessions, lockout and rate limits; consent screen for third-party Clients; first-party Clients.
- Users, Groups, Roles and Permissions for administering Authora; Scopes and Clients with Redirect URIs, allowed and served Scopes, and expiring Secrets.
- Admin API and admin interface with a backend for frontend that keeps tokens out of the browser.
- Audit log with a tamper-evident hash chain and verification endpoint.
- Startup checks that refuse insecure production settings; security headers; health checks; cleanup of expired data.
- Container images for the server and the database migrations, and a Docker Compose setup for local evaluation.
- Documentation following Diátaxis: tutorials, how-to guides, reference and explanation.

[Unreleased]: https://github.com/DuttonAlan/Authora/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/DuttonAlan/Authora/releases/tag/v0.1.0
