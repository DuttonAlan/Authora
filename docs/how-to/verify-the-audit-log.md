# Check the integrity of the audit log

Authora seals its audit events into a hash chain, so changes made directly in the database can be detected. This guide shows how to check the chain and what to do when the check fails. Why it works this way is described in [Audit log integrity](../explanation/audit-log-integrity.md).

You need the Permission `audit.verify`, which the `admin` Role has.

## 1. Find a checkpoint in your logs

After every batch of sealed events, Authora logs a checkpoint (event 11001):

```
Sealed 3 audit events. Checkpoint: the audit chain ends at Sequence 1234 with hash 9f2c…e41a.
```

These checkpoints are only useful if the logs live outside Authora's database, e.g. in your log platform. Pick a recent one.

## 2. Verify the chain

Call the verification endpoint with the checkpoint, through the [admin API](call-the-admin-api.md):

```sh
curl "https://auth.example.com/api/audit-events/verification?checkpointSequence=1234&checkpointHash=9f2c…e41a" \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

```json
{
  "isIntact": true,
  "firstSequence": 1,
  "lastSequence": 1250,
  "lastHash": "…",
  "eventsChecked": 1250,
  "brokenAtSequence": null,
  "problem": null,
  "unsealedEvents": 0,
  "checkpointMatches": true
}
```

Without the checkpoint parameters, Authora checks the chain on its own, and `checkpointMatches` is `null`.

## 3. Read the result

The chain is fine when `isIntact` is `true`, `unsealedEvents` is `0` and `checkpointMatches` is `true`.

| Result | What it means |
|---|---|
| `isIntact: false` with `brokenAtSequence` and `problem` | A sealed event or link was changed or deleted in the database, or the start of the chain was deleted by something other than the cleanup. Authora also logs critical event 11011. |
| `unsealedEvents` > 0 | Events older than the sealing interval plus a minute are not in the chain, e.g. inserted directly into the database with an old timestamp. |
| `checkpointMatches: false` | The chain no longer matches your checkpoint: a different hash means it was rebuilt after a change; a Sequence beyond the end means the end was cut off. |

## If the check fails

1. Treat it as a security incident: someone with write access to the database changed the audit log.
2. Keep the database as it is and take a copy for analysis.
3. Compare the events with your log system, which has the original security events and checkpoints.
4. Review who has write access to the database and rotate those credentials.

## Retention and the start of the chain

The cleanup deletes events older than `Authora:Cleanup:AuditRetention` (365 days by default) from the start of the chain. Before it does, it records `system.audit-chain-truncated` and logs event 8004. A chain that does not start at Sequence 1 only verifies if such an event names its first link. To detect a forged truncation, compare the `system.audit-chain-truncated` events with the events 8004 in your logs.
