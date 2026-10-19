# Apply database migrations and upgrade

Authora never changes the database schema at startup. Every new version may bring migrations, and you apply them before you start the new version. This guide shows three ways to do that and the order of an upgrade.

## Upgrade steps

1. Read the [changelog](../../CHANGELOG.md) for the new version, especially the sections *Changed*, *Removed* and *Security*.
2. [Back up the database](back-up-and-restore.md).
3. Apply the migrations of the new version (below).
4. Deploy the new server version.
5. Check `GET /health`. It reports `Unhealthy` (`503`) while migrations are pending.

Authora does not promise that a running server version keeps working with the schema of a newer version. Stop all instances before step 3, unless the changelog says that a release can be applied while the previous version runs.

## Option 1: the migrator image

The image `authora-migrator` contains a migration bundle for its version. Run it with the connection string of the user that owns the schema:

```sh
docker run --rm \
  -e ConnectionStrings__Database="Host=…;Database=authora;Username=authora_migrator;Password=…" \
  ghcr.io/duttonalan/authora-migrator:1.2.0
```

It applies all pending migrations under a lock, so it is safe to run from several deployment jobs at once, and ends with `Done.`

## Option 2: a migration bundle

A bundle is a single executable that contains the migrations. Build it from the sources of the version you deploy:

```sh
dotnet tool install --global dotnet-ef
ConnectionStrings__Database="Host=localhost;Database=bundle" \
  dotnet ef migrations bundle --project src/Authora.Infrastructure --startup-project src/Authora.Api -o efbundle
```

The connection string while building is a placeholder; the build never connects. Run the bundle with the real one:

```sh
AllowedHosts=migrator \
ConnectionStrings__Database="Host=…;Database=authora;Username=authora_migrator;Password=…" \
  ./efbundle
```

The bundle starts Authora's host configuration to find the database, so Authora's startup checks apply. `AllowedHosts` only needs a value other than `*` for that; the bundle serves no requests.

## Option 3: an SQL script

To review the SQL or let a database administrator run it:

```sh
ConnectionStrings__Database="Host=localhost;Database=script" \
  dotnet ef migrations script --idempotent --project src/Authora.Infrastructure --startup-project src/Authora.Api -o migrate.sql
```

The script is idempotent: it only applies migrations that are missing.

## After the migrations

If you use a separate, restricted database user for the server (see [Deploy to production](deploy-to-production.md#2-prepare-the-database)), new tables get its permissions through the default privileges. Check the changelog for migrations that need a manual grant.
