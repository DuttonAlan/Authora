# Deploy to production

This guide shows how to run Authora for real users: with its own database users, keys stored as secrets, TLS, and the settings Authora checks before it starts. It assumes you run containers. Without containers the steps are the same; see [Run Authora from source](run-from-source.md) for building it.

> **Status:** Authora has not had an independent security audit yet. Read the [security model](../explanation/security-model.md) and the [limitations](../reference/standards.md#not-supported) before you put it in front of users.

## Checklist

- [ ] `ASPNETCORE_ENVIRONMENT=Production` (the default when it is not set).
- [ ] `Authora__OAuth__Issuer` is the public HTTPS URL, e.g. `https://auth.example.com`.
- [ ] `AllowedHosts` lists the public host names, e.g. `auth.example.com`.
- [ ] TLS is terminated by Kestrel, or by a reverse proxy that Authora trusts ([Configure a reverse proxy](configure-a-reverse-proxy.md)).
- [ ] The connection string comes from a secret store, and its database user has only the permissions below.
- [ ] The signing key is a mounted secret, readable only by Authora, and backed up separately from the database.
- [ ] Migrations ran before the server started.
- [ ] After the first sign-in of the bootstrap administrator: a new password is set and `Authora__Bootstrap__Administrator__Password` is removed.
- [ ] Health checks are wired to the orchestrator, and the logs are shipped to a separate log system.
- [ ] With several instances, or without a persistent home directory: Data Protection keys are persisted and encrypted ([Persist Data Protection keys](persist-data-protection-keys.md)).
- [ ] Database backups run and a restore was tested ([Back up and restore](back-up-and-restore.md)).

## 1. Get the images

Authora ships as two images, both built from the [`Dockerfile`](../../Dockerfile):

| Image | Target | Purpose |
|---|---|---|
| `ghcr.io/duttonalan/authora` | `runtime` | The server. Listens on plain HTTP port 8080 by default. |
| `ghcr.io/duttonalan/authora-migrator` | `migrator` | Applies the database migrations, then exits. |

Use a version tag, e.g. `:1.2.0`, never `latest`, so the server and the migrator always have the same version. To build the images yourself:

```sh
docker build --target runtime  -t authora .
docker build --target migrator -t authora-migrator .
```

## 2. Prepare the database

Authora needs PostgreSQL (it is tested with 17). Use two database users: one that owns the schema and runs migrations, and one with fewer rights for the server. As a superuser:

```sql
CREATE ROLE authora_migrator LOGIN PASSWORD '…';
CREATE ROLE authora_app      LOGIN PASSWORD '…';
CREATE DATABASE authora OWNER authora_migrator;
```

Run the migrator once with the owner's connection string:

```sh
docker run --rm \
  -e ConnectionStrings__Database="Host=db.internal;Database=authora;Username=authora_migrator;Password=…;SSL Mode=VerifyFull" \
  ghcr.io/duttonalan/authora-migrator:1.2.0
```

It ends with `Done.` and has created the schemas `identity`, `auth` and `audit`. Then, connected to the database `authora` as `authora_migrator`, give the server user what it needs:

```sql
GRANT USAGE ON SCHEMA identity, auth, audit TO authora_app;
GRANT SELECT ON public."__EFMigrationsHistory" TO authora_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity, auth TO authora_app;
GRANT SELECT, INSERT, DELETE ON ALL TABLES IN SCHEMA audit TO authora_app;

-- Tables that later migrations create get the same rights.
ALTER DEFAULT PRIVILEGES IN SCHEMA identity, auth GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO authora_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA audit GRANT SELECT, INSERT, DELETE ON TABLES TO authora_app;
```

The server user cannot `UPDATE` the audit tables, so it can only add audit events, never change them. `DELETE` is only needed for the cleanup after the retention period. Read access to `__EFMigrationsHistory` lets the health check report pending migrations.

Use TLS for the connection whenever the database is on another host (`SSL Mode=VerifyFull`).

## 3. Create the keys

**Signing key** for ID Tokens (RSA, at least 2048 bits, PEM, unencrypted):

```sh
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out signing-key.pem
```

Store it as a secret (Docker or Kubernetes secret, or a file readable only by the user that runs Authora, `chmod 600`). The image runs as the non-root user `app` (UID `1654`), so the file must be readable by that user. Authora logs event 9004 when other users can read or write the file. Never put the key into the image or the database, and back it up separately from the database; together they would allow forging ID Tokens.

**Data Protection certificate**, if you run several instances or the container has no persistent home directory: see [Persist Data Protection keys](persist-data-protection-keys.md).

## 4. Configure Authora

Authora reads `appsettings.json` and environment variables. Environment variables use `__` as the separator. A minimal production configuration:

```sh
ASPNETCORE_ENVIRONMENT=Production
AllowedHosts=auth.example.com
ConnectionStrings__Database=Host=db.internal;Database=authora;Username=authora_app;Password=…;SSL Mode=VerifyFull

Authora__OAuth__Issuer=https://auth.example.com
Authora__Oidc__SigningKeyPath=/run/secrets/authora-signing-key.pem

# Only for the first start; remove it afterwards (step 6).
Authora__Bootstrap__Administrator__Email=you@example.com
Authora__Bootstrap__Administrator__Password=…

# Behind a reverse proxy that terminates TLS:
Authora__ForwardedHeaders__KnownNetworks__0=10.0.0.0/24

# Shared, encrypted Data Protection keys:
Authora__DataProtection__KeysPath=/var/lib/authora/keys
Authora__DataProtection__CertificatePath=/run/secrets/authora-data-protection.pfx
Authora__DataProtection__CertificatePassword=…
```

All settings are listed in the [configuration reference](../reference/configuration.md).

### What Authora checks before it starts

Authora refuses to start when a setting is invalid, with a message that names the setting. In every environment except `Development` and `Testing` (so also in e.g. `Staging`) it also requires:

| Setting | Requirement |
|---|---|
| `Authora:OAuth:Issuer` | HTTPS. |
| `AllowedHosts` | Set, and without `*`. `appsettings.json` contains `*` for development, so you must override it. |
| `Authora:ForwardedHeaders` | Valid addresses and networks, none broader than `/8` (IPv4) or `/7` (IPv6). Checked in every environment. |

Swagger UI and the OpenAPI document are only served in `Development`.

## 5. Run the server

The server must be reached over HTTPS: its cookies are `Secure`, and the sign-in page does not work over plain HTTP. Choose one:

- **Behind a reverse proxy** (recommended): the proxy terminates TLS and forwards to port 8080. Follow [Configure a reverse proxy](configure-a-reverse-proxy.md).
- **Kestrel terminates TLS:** mount a certificate and set
  `ASPNETCORE_URLS=https://+:8443`, `Kestrel__Certificates__Default__Path=/run/secrets/tls.crt` and `Kestrel__Certificates__Default__KeyPath=/run/secrets/tls.key`.

Wire the health checks to your orchestrator:

| Probe | Endpoint |
|---|---|
| Liveness | `GET /health/live` |
| Readiness | `GET /health` (`503` when the database is unreachable or migrations are pending) |

See [Health checks](../reference/health-checks.md).

## 6. Secure the first administrator

On the first start, Authora creates the administrator from `Authora:Bootstrap:Administrator` (and logs event 7004). Then:

1. Sign in at `https://auth.example.com/admin/`.
2. Open **Users** → your User → **Set password**, and set a new password.
3. Remove `Authora__Bootstrap__Administrator__Password` from the configuration and restart. Until you do, Authora logs event 7007 on every start.

## 7. Ship the logs

Send the application logs to a system outside Authora's database, e.g. your log platform or a SIEM. They contain the security events (sign-ins, lockouts, token reuse, key changes) and the checkpoints of the audit log's hash chain, which are the only way to detect a rebuilt chain. See [Check the integrity of the audit log](verify-the-audit-log.md) and the [log events](../reference/log-events.md).

## Next steps

- [Back up and restore](back-up-and-restore.md)
- [Rotate signing keys](rotate-signing-keys.md)
- [Apply database migrations and upgrade](upgrade-authora.md)
