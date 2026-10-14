# Authora – Production Operation

This document describes how to run Authora securely (US-036): the required settings, the security boundaries, the HTTP hardening, key handling, and backup and recovery. Endpoint behavior is described in [oauth-core.md](oauth-core.md), [openid-connect.md](openid-connect.md) and [admin.md](admin.md).

## Checklist

- [ ] `ASPNETCORE_ENVIRONMENT=Production` (the default when it is not set).
- [ ] `Authora:OAuth:Issuer` is the public HTTPS URL, e.g. `https://auth.example.com`.
- [ ] `AllowedHosts` lists the public host names, e.g. `auth.example.com`.
- [ ] TLS is terminated by Kestrel, or by a reverse proxy listed in `Authora:ForwardedHeaders`.
- [ ] `ConnectionStrings:Database` comes from a secret store or an environment variable, and its database user has only the permissions below.
- [ ] The signing key is a mounted secret, readable only by the user that runs Authora, and backed up separately.
- [ ] Migrations were applied before the start (`dotnet ef database update`, or a migration bundle or SQL script).
- [ ] After the first sign-in of the bootstrap administrator: a new password is set and `Authora:Bootstrap:Administrator:Password` is removed (Authora logs event 7007 until it is).
- [ ] Health checks are wired to the orchestrator, and the application logs are shipped to a log system.
- [ ] Database backups run and a restore was tested, see [Backup and recovery](#backup-and-recovery).

## Settings checked at startup

Authora does not start if a setting is invalid. Every options class has a validator (`ValidateOnStart`), and the key files are loaded at startup. In the `Production` environment, these settings are also required:

| Setting | Requirement |
|---|---|
| `Authora:OAuth:Issuer` | HTTPS. Outside Production, HTTP is allowed for loopback hosts. |
| `AllowedHosts` | Set and without `*`. Host filtering rejects requests with another `Host` header. `appsettings.json` contains `*` for development, so it must be overridden, e.g. `AllowedHosts=auth.example.com`. |
| `Authora:ForwardedHeaders` | Valid IP addresses and CIDR networks (in every environment). |

Further settings are described in [oauth-core.md](oauth-core.md#configuration), [openid-connect.md](openid-connect.md#configuration) and [admin.md](admin.md#configuration). Swagger UI and the OpenAPI document are only available in `Development`.

## Security boundaries

| Endpoint | Who may call it | Protection |
|---|---|---|
| `GET /.well-known/openid-configuration`, `GET /.well-known/jwks.json` | Anyone | Public metadata. CORS from every origin, `GET` only. |
| `GET/POST /oauth/authorize`, `/account/login`, `/account/logout` | Browsers | Session cookie, antiforgery tokens, exact Redirect URI match, PKCE `S256`, sign-in rate limit, lockout. |
| `POST /oauth/token`, `POST /oauth/revoke` | Clients | Client authentication (Secret or PKCE), rate limit. CORS only from origins of registered Redirect URIs. |
| `POST /oauth/introspect` | Confidential Clients that serve a Scope | Client Secret; only tokens with a served Scope are reported active. |
| `GET/POST /oauth/userinfo` | Clients with an Access Token with `openid` | Access Token. |
| `/api/*` | Admin Clients, e.g. the admin interface | Access Token with `authora.admin` plus a Permission per action. Cookies are ignored. No CORS. |
| `/admin/*` | Browsers of administrators | Static files and public settings only; all data comes from `/api/*`. |
| `GET /health`, `GET /health/live` | Load balancers, orchestrators | Public, only the overall status. |
| `GET /health/details` | Monitoring with an admin Access Token | Permission `health.read`. |

Users authenticate with Email and password only; there is no self-registration, password reset or multi-factor authentication yet (see [Stories.md](Stories.md)).

## HTTPS and reverse proxy

Authora must be reached over HTTPS: the Session and antiforgery cookies are `Secure`, and the antiforgery system refuses to issue its cookie on a plain HTTP request, so sign-in fails with an error. Outside `Development`, responses carry `Strict-Transport-Security: max-age=31536000` (not for `localhost`). Plain HTTP requests are redirected to HTTPS when Kestrel knows the HTTPS port.

### Reverse proxy

Behind a reverse proxy that terminates TLS, Authora must trust the proxy's `X-Forwarded-For` and `X-Forwarded-Proto` headers. Otherwise it sees every request as plain HTTP from the proxy's IP address: the sign-in fails, all clients share one rate limit, and the audit log records the proxy's address.

| Setting | Default | Description |
|---|---|---|
| `Authora:ForwardedHeaders:KnownProxies` | `[]` | IP addresses of the proxies, e.g. `10.0.0.5`. |
| `Authora:ForwardedHeaders:KnownNetworks` | `[]` | Networks of the proxies in CIDR notation, e.g. `10.0.0.0/24`. |
| `Authora:ForwardedHeaders:ForwardLimit` | `1` | Number of proxies in front of Authora, 1 to 10. |

The headers are only read when one of the lists is set, and only from those addresses, so nobody else can fake their IP address. The proxy must overwrite (not append to) `X-Forwarded-For` from the client, and forward the original `Host` header.

## HTTP hardening

| Header or attribute | Where | Purpose |
|---|---|---|
| `X-Content-Type-Options: nosniff` | All responses | Browsers do not run JSON as a script or page. |
| `X-Frame-Options: DENY`, CSP `frame-ancestors 'none'` | All responses | No clickjacking. |
| `Referrer-Policy: no-referrer` | All responses | Codes and Ids in URLs do not leak to other sites. |
| `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'` | API responses | A response can never run as a page. |
| Own CSP, `Cache-Control: no-store` | Sign-in, sign-out and consent pages | Only own styles; pages with a Session or a pending authorization are not cached. |
| Own CSP, `Cross-Origin-Opener-Policy: same-origin` | Admin interface | See [admin.md](admin.md#security). |
| `Cache-Control: no-store` | Token, revocation, introspection, UserInfo responses | Tokens are not cached. |
| Session cookie `__Host-authora_session` | Browsers | `Secure`, `HttpOnly`, `SameSite=Lax`, no `Domain`, no expiry. The server enforces idle timeout and lifetime. |
| Antiforgery cookie `__Host-authora_antiforgery` | Browsers | `Secure`, `HttpOnly`, `SameSite=Strict`, no `Domain`. |

### CSRF, redirects, rate limits and tokens

- **CSRF:** all forms (sign-in, sign-out, consent) use antiforgery tokens. The admin API ignores cookies and only accepts Bearer tokens, so a signed-in browser cannot be abused for it. The OAuth `state` parameter protects Clients, and the admin interface checks it.
- **Redirects:** Redirect URIs must match a registered URI exactly; an invalid `redirect_uri` is shown as an error and never redirected to. The sign-in page only returns to local URLs. Redirect URIs must use HTTPS, or HTTP only for `127.0.0.1` and `[::1]`.
- **Rate limits:** sign-in attempts (10 per minute and IP address) and the token and revocation endpoints (300). A User is locked out after 5 failed sign-ins in a row. See [oauth-core.md](oauth-core.md#configuration).
- **Tokens:** codes, Access and Refresh Tokens and Session tokens are 256-bit random values; only their SHA-256 hash is stored. Codes are single-use, Refresh Tokens rotate, and reuse revokes the Grant. Access Tokens are opaque and checked on every request, so revocation takes effect immediately. PKCE `S256` is required for all Clients. ID Tokens are signed with RS256 and live 5 minutes.
- **Passwords:** PBKDF2-HMAC-SHA512 with 210,000 iterations; 12 to 128 characters.

## Database

Authora does not apply migrations at startup. Apply them before starting a new version, e.g. with a migration bundle or an idempotent SQL script:

```sh
dotnet ef migrations bundle --project src/Authora.Infrastructure --startup-project src/Authora.Api -o efbundle
./efbundle --connection "Host=…;Database=authora;Username=authora_migrator;Password=…"

dotnet ef migrations script --idempotent --project src/Authora.Infrastructure --startup-project src/Authora.Api -o migrate.sql
```

Use two database users: one that owns the schemas and runs the migrations, and one for the application with `SELECT`, `INSERT`, `UPDATE` and `DELETE` on the tables of the schemas `identity`, `auth` and `audit` and nothing else. The application needs `DELETE` on `audit.events` only for the cleanup after the retention; to make the audit log append-only, revoke `UPDATE` on `audit.events` from the application user. Use TLS for the connection when the database is not on the same host (`SSL Mode=VerifyFull` in the connection string).

The schemas:

| Schema | Content |
|---|---|
| `identity` | Users, Groups, Roles, Permissions, Scopes, Clients with their Redirect URIs and Secret hashes. |
| `auth` | Sessions, Authorization Codes, Grants, Access and Refresh Tokens (hashes only). |
| `audit` | Audit events. |

## Health checks

| Endpoint | Use |
|---|---|
| `GET /health/live` | Liveness: the process answers. Never checks the database. |
| `GET /health` | Readiness: `Healthy` (200), `Degraded` (200) or `Unhealthy` (503). Checks the database connection, pending migrations and the signing keys. |
| `GET /health/details` | The full report for monitoring, see [Security boundaries](#security-boundaries). |

`Degraded` means that changed key files could not be loaded and the previous keys are still in use, see [openid-connect.md](openid-connect.md#key-rotation-us-035).

## Backup and recovery

### What to back up

| What | How | Why |
|---|---|---|
| The PostgreSQL database | `pg_dump` (logical) or continuous archiving (point-in-time recovery). | Users, Clients, Roles and the audit log. |
| The signing keys | Separately from the database, e.g. in the secret store, encrypted. | Without them, ID Tokens cannot be signed. Never store them with the database backup: together they would allow forging ID Tokens. |
| The configuration | Infrastructure as code, secret store. | Connection string, Issuer, bootstrap settings. |

```sh
# Daily logical backup in the custom format, readable only by the backup user.
pg_dump --format=custom --file=authora-$(date +%F).dump "host=… dbname=authora user=authora_backup"
```

The database contains password hashes and token hashes, but no plain-text secrets; still, store backups encrypted and restrict access to them like access to the database itself.

### Restore

1. Stop all instances of Authora.
2. Restore the database, e.g. `pg_restore --clean --if-exists --dbname=authora authora-2026-10-14.dump`.
3. Apply the migrations of the running version, if the backup is older.
4. **Revoke what the backup brings back.** A backup also contains Sessions, Grants and tokens that were revoked or signed out after it was taken. End them all, so every User signs in again and every Client gets new tokens:

   ```sql
   TRUNCATE auth.access_tokens, auth.refresh_tokens, auth.grants, auth.authorization_codes, auth.sessions;
   ```

   Also check whether Users who were deactivated or deleted, Roles that were removed or Client Secrets that were revoked after the backup came back, using the audit log of the time after the backup if it still exists (e.g. from the application logs).
5. Start Authora and check `GET /health`.

If only the signing key is lost: create a new key and configure it as `SigningKeyPath`. ID Tokens signed with the old key can no longer be verified once Clients refresh the JWKS, which only affects sign-ins of the last few minutes; no data is lost.

## Automated tests

The integration tests (`tests/Authora.Api.IntegrationTests`, run in CI against PostgreSQL) cover the core flows and their security rules, among others:

| Area | Tests |
|---|---|
| Authorization Code flow with PKCE, ID Tokens, UserInfo, discovery | `IdTokenTests`, `UserInfoTests`, `OidcMetadataTests` |
| Introspection and resource server boundaries | `IntrospectionTests`, `ServeScopeTests` |
| CORS | `OAuthCorsTests` |
| Admin API authorization (every action needs the admin Scope and a Permission) | `AdminApiPolicyTests`, `AdminApiAccessTests` |
| Concurrency rules (last administrator, memberships) | `LastAdministratorTests`, `GroupTests` |
| Signing keys and their rotation | `SigningKeyFileTests` |
| Audit log | `AuditLogTests` |
| Admin interface sign-in and `/api/me` | `AdminUiTests` |
| Headers, cookies, rate limits, production checks | `SecurityHardeningTests` |
| Cleanup and health | `ExpiredDataCleanerTests`, `HealthEndpointTests` |
