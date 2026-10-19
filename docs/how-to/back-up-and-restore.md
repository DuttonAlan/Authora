# Back up and restore

This guide shows what to back up, how, and how to restore Authora without bringing revoked sessions and tokens back to life.

## What to back up

| What | How | Why |
|---|---|---|
| The PostgreSQL database | `pg_dump`, or continuous archiving for point-in-time recovery | Users, Clients, Roles, Scopes and the audit log. |
| The signing keys | Separately from the database, e.g. in your secret store, encrypted | Without them, ID Tokens cannot be signed. Never store them with the database backup: together they would allow forging ID Tokens. |
| The configuration | Infrastructure as code and your secret store | Connection string, Issuer, proxy and key settings. |

The Data Protection keys and certificate need no backup: losing them only signs administrators out.

The database holds password hashes and token hashes, but no plain-text secrets. Still, encrypt backups and restrict access to them like access to the database itself.

## Back up the database

A daily logical backup in the custom format:

```sh
pg_dump --format=custom --file=authora-$(date +%F).dump "host=db.internal dbname=authora user=authora_backup"
```

The backup user needs read access to the schemas `identity`, `auth` and `audit`.

## Restore

1. **Stop all instances** of Authora.
2. **Restore the database:**

   ```sh
   pg_restore --clean --if-exists --dbname=authora authora-2026-10-14.dump
   ```

3. **Apply the migrations** of the version you run, if the backup is older. See [Apply database migrations and upgrade](upgrade-authora.md).
4. **Revoke what the backup brings back.** A backup also contains Sessions, Grants and tokens that were signed out or revoked after it was taken. End them all, so every User signs in again and every Client gets new tokens:

   ```sql
   TRUNCATE auth.access_tokens, auth.refresh_tokens, auth.grants, auth.authorization_codes, auth.sessions;
   ```

5. **Check for changes the backup undid.** Users who were deactivated or deleted, Roles that were removed or Client Secrets that were revoked after the backup was taken are back. Use the audit events of that period, if you still have them in your log system, and repeat those changes.
6. **Start Authora** and check `GET /health`.
