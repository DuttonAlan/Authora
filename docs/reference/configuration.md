# Configuration

Authora reads its settings from `appsettings.json`, `appsettings.{Environment}.json`, environment variables and command-line arguments, in this order; later sources win.

- **Environment variables** use `__` instead of `:`, e.g. `Authora__OAuth__Issuer`. List items get an index: `Authora__Oidc__AdditionalKeyPaths__0`.
- **Durations** use the format `[d.]hh:mm:ss`, e.g. `00:15:00` for 15 minutes or `90.00:00:00` for 90 days. A plain number is read as days (`15` is 15 days), and the upper limits make startup fail on that mistake.
- **Validation:** Authora checks every setting at startup and refuses to start with an invalid one, naming the setting.

## Required settings

| Setting | Description |
|---|---|
| `ConnectionStrings:Database` | PostgreSQL connection string, e.g. `Host=db;Database=authora;Username=authora_app;Password=…`. |
| `Authora:OAuth:Issuer` | The public URL of Authora, e.g. `https://auth.example.com`. HTTPS; HTTP only for loopback hosts in `Development` and `Testing`. No query, no fragment, no trailing `/`. All endpoint URLs and the `iss` claim are built from it. If it has a path, Authora must be served under that path. |
| `Authora:Oidc:SigningKeyPath` | PEM file with the RSA private key (at least 2048 bits, unencrypted) that signs ID Tokens. Relative paths are resolved against the content root. |
| `AllowedHosts` | Host names Authora answers to, separated by `;`, e.g. `auth.example.com`. Outside `Development` and `Testing`, it must be set and must not contain `*`. |

## OAuth

| Setting | Default | Limits | Description |
|---|---|---|---|
| `Authora:OAuth:AuthorizationCodeLifetime` | `00:01:00` | at most 10 minutes | Lifetime of Authorization Codes. |
| `Authora:OAuth:GrantLifetime` | `90.00:00:00` | at most 365 days | Absolute lifetime of a Grant, counted from the code exchange. No token of the Grant expires later, so rotating Refresh Tokens cannot keep it alive; afterwards the User has to approve the Client again. |
| `Authora:OAuth:CorsOriginsCacheDuration` | `00:01:00` | at most 1 hour | How long the allowed CORS origins (from the Redirect URIs of enabled Clients) are cached. `00:00:00` disables the cache. |

Lifetimes of Access and Refresh Tokens are set per Client, in the admin interface or API: Access Tokens 3600 seconds by default (at most 1 day), Refresh Tokens 86400 seconds by default (at most 90 days, and always longer than the Access Token lifetime).

## OpenID Connect

| Setting | Default | Limits | Description |
|---|---|---|---|
| `Authora:Oidc:SigningKeyPath` | – (required) | | See above. Startup fails if the file is missing, unreadable, encrypted, not RSA, shorter than 2048 bits or only a public key. |
| `Authora:Oidc:AdditionalKeyPaths` | `[]` | | PEM files with RSA keys that are only published in the JWKS, for key rotation. Public keys are enough. |
| `Authora:Oidc:IdTokenLifetime` | `00:05:00` | at most 1 hour | Lifetime of ID Tokens. |
| `Authora:Oidc:KeyReloadInterval` | `00:01:00` | 10 seconds to 1 hour | How often the key files are checked for changes. Changing it needs a restart. |

## Sign-in and Sessions

| Setting | Default | Limits | Description |
|---|---|---|---|
| `Authora:SignIn:SessionIdleTimeout` | `02:00:00` | at most 24 hours | A Session ends after this time without use. Keep it longer than the Access Token lifetime of the admin interface's Client, or active administrators are asked for their password again. |
| `Authora:SignIn:SessionLifetime` | `12:00:00` | at most 7 days, not shorter than the idle timeout | A Session ends after this time regardless of use. |
| `Authora:SignIn:MaxFailedAttempts` | `5` | 1 to 100 | Failed sign-ins in a row that lock a User out. |
| `Authora:SignIn:LockoutDuration` | `00:15:00` | at most 1 hour | How long a User stays locked out. Kept short, because anyone who knows an Email can trigger a lockout. |

## Rate limits

Per client IP address. Behind a reverse proxy, configure `Authora:ForwardedHeaders`, or all clients share one limit.

| Setting | Default | Limits | Description |
|---|---|---|---|
| `Authora:RateLimiting:SignInPermitsPerMinute` | `10` | 1 to 100000 | Sign-in attempts per minute. |
| `Authora:RateLimiting:TokenPermitsPerMinute` | `300` | 1 to 100000 | Requests to `/oauth/token`, `/oauth/revoke`, `/admin/callback` and `/admin/session/refresh` per minute. `/oauth/introspect` is not limited. |

Requests over the limit get `429 Too Many Requests`.

## Reverse proxy

| Setting | Default | Limits | Description |
|---|---|---|---|
| `Authora:ForwardedHeaders:KnownProxies` | `[]` | valid IP addresses | Proxies whose `X-Forwarded-For` and `X-Forwarded-Proto` headers are trusted. |
| `Authora:ForwardedHeaders:KnownNetworks` | `[]` | CIDR, at most `/8` (IPv4) or `/7` (IPv6) broad, not containing `::ffff:0:0/96` | Networks whose forwarded headers are trusted. |
| `Authora:ForwardedHeaders:ForwardLimit` | `1` | 1 to 10 | Number of proxies in front of Authora. |

The forwarded headers are only read when one of the lists is set. See [Configure a reverse proxy](../how-to/configure-a-reverse-proxy.md).

## Data Protection

| Setting | Default | Description |
|---|---|---|
| `Authora:DataProtection:KeysPath` | not set | Directory for the keys that encrypt antiforgery tokens and admin interface cookies, shared by all instances. Without it, keys are kept in the home directory of the user that runs Authora, or only in memory. The container image sets `/var/lib/authora/keys`. |
| `Authora:DataProtection:CertificatePath` | not set | PKCS#12 file (`.pfx`) with a certificate and its private key that encrypts the keys at rest. Requires `KeysPath`. |
| `Authora:DataProtection:CertificatePassword` | not set | Password of the certificate file. |

See [Persist Data Protection keys](../how-to/persist-data-protection-keys.md).

## Bootstrap

Used when Authora starts. Nothing is created a second time.

| Setting | Default | Description |
|---|---|---|
| `Authora:Bootstrap:Administrator:Email` | not set | Email of the first administrator. If no administrator who can sign in exists and this is set, Authora creates one with the `admin` Role. |
| `Authora:Bootstrap:Administrator:Password` | not set | Initial password, 12 to 128 characters. Required while the administrator still has to be created. Remove it afterwards; Authora logs event 7007 while it is set. |
| `Authora:Bootstrap:Administrator:FirstName` | `Authora` | |
| `Authora:Bootstrap:Administrator:LastName` | `Administrator` | |
| `Authora:Bootstrap:Client:Name` | `Authora Admin` | Name of an optional Public Client for the admin API. |
| `Authora:Bootstrap:Client:RedirectUris` | `[]` | If set, Authora creates that Client, allowed the Scope `authora.admin`, as long as no enabled Client other than the admin interface's own is allowed `authora.admin`. Useful for scripts, see [Call the admin API](../how-to/call-the-admin-api.md). |

## Admin interface

| Setting | Default | Description |
|---|---|---|
| `Authora:AdminUi:Enabled` | `true` | When `false`, `/admin/*` answers `404` and its Client is not created. An existing Client is kept. |

## Cleanup

A background service deletes expired data.

| Setting | Default | Limits | Description |
|---|---|---|---|
| `Authora:Cleanup:Interval` | `01:00:00` | 1 minute to 1 day | How often the cleanup runs. The first run is one interval after startup. |
| `Authora:Cleanup:Retention` | `7.00:00:00` | at least 1 day | How long Authorization Codes, Grants, tokens and Sessions are kept after they expired or were revoked. Reuse of a consumed code or Refresh Token is only detected while its row exists. |
| `Authora:Cleanup:BatchSize` | `1000` | 1 to 10000 | Rows deleted per statement. |
| `Authora:Cleanup:AuditRetention` | `365.00:00:00` | at least 30 days | How long audit events are kept. |

## Audit log

| Setting | Default | Limits | Description |
|---|---|---|---|
| `Authora:Audit:SealInterval` | `00:00:05` | 1 second to 5 minutes | How often new audit events are sealed into the hash chain. |
| `Authora:Audit:BatchSize` | `1000` | 1 to 10000 | Events sealed or verified per batch. |

## ASP.NET Core settings

Standard ASP.NET Core settings work as usual, for example:

| Setting | Use |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (default), `Development`. Only `Development` serves Swagger UI and relaxes the production checks. |
| `ASPNETCORE_URLS`, `ASPNETCORE_HTTP_PORTS` | Where Kestrel listens. The container image listens on HTTP port 8080. |
| `Kestrel:Certificates:Default:Path`, `…:KeyPath`, `…:Password` | TLS certificate when Kestrel terminates TLS. |
| `Logging:LogLevel:*` | Log levels. |
