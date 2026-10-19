# Health checks

| Endpoint | Access | Use |
|---|---|---|
| `GET /health/live` | Public | Liveness: the process answers. Never touches the database. |
| `GET /health` | Public | Readiness: only the overall status. |
| `GET /health/details` | Access Token with `authora.admin` and Permission `health.read` | Full report for monitoring. |

## Status

| Status | HTTP | Meaning |
|---|---|---|
| `Healthy` | `200` | Everything works. |
| `Degraded` | `200` | Authora works, but needs attention (see below). |
| `Unhealthy` | `503` | Authora cannot serve requests correctly. |

`/health` and `/health/live` return only `{"status": "Healthy"}`. The details are not public, because they reveal e.g. the schema version.

## Checks

| Name | `Unhealthy` when | `Degraded` when |
|---|---|---|
| `database` | The database cannot be reached. | – |
| `migrations` | Migrations are pending: the database is older than this version. | The database has migrations this version does not know: it is newer, e.g. after a rollback. |
| `signing-keys` | – | Changed key files could not be loaded; the previous keys stay in use. See [Rotate signing keys](../how-to/rotate-signing-keys.md). |

## Details

```json
{
  "status": "Healthy",
  "durationMs": 12.4,
  "checks": [
    { "name": "database", "status": "Healthy", "description": null, "durationMs": 8.1, "data": {} },
    { "name": "migrations", "status": "Healthy", "description": "The database schema is up to date.", "durationMs": 3.2,
      "data": { "current": "20261014…", "latest": "20261014…", "pending": [], "unknown": [] } },
    { "name": "signing-keys", "status": "Healthy", "description": "ID Tokens are signed with key Iz6V….", "durationMs": 0.1, "data": {} }
  ]
}
```
