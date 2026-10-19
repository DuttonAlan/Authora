# Audit log integrity

An audit log is only useful if it can be trusted after an incident. This page explains how Authora makes its audit log hard to change unnoticed, and where that protection ends.

## Three layers

**1. The application cannot change events.** The API and the admin interface can only add audit events; there is no endpoint to edit or delete one. With the recommended database permissions, the application's database user has no `UPDATE` right on the audit tables at all.

**2. Changes in the database are detectable.** Every few seconds, a background service *seals* new events into a hash chain in the table `audit.chain`. Each link stores:

- a sequence number without gaps,
- the Id of the event,
- the hash of the previous link, and
- a SHA-256 hash over its sequence number, the previous hash and every field of the event.

Changing, deleting or inserting a sealed event breaks the chain from that point on, and the verification endpoint reports where.

**3. Rebuilding the chain is detectable too.** Someone with full write access to the database could change an event and then recompute every following link. To catch that, Authora logs a *checkpoint* after each batch: the last sequence number and its hash (log event 11001). If the logs go to a system outside Authora's database, a checkpoint from yesterday will not match a chain that was rebuilt today.

## Why not more?

A hash chain inside the same database cannot *prevent* changes; it can only reveal them. Truly immutable storage, such as write-once object storage or an external SIEM, is outside what a lightweight, self-hosted service should require. Authora's approach gives strong detection with nothing more than a log shipper, and lets operators who need more forward the logs to such storage.

## Retention without breaking the chain

Old events must eventually be deleted (after 365 days by default). The cleanup only deletes from the start of the chain, together with their links, and always keeps the last link so the chain continues.

A chain that does not start at sequence 1 would look like tampering. So before deleting, the cleanup records a `system.audit-chain-truncated` event naming the new first link, and that event is itself sealed into the chain. The verification accepts a missing start only if such an event exists. The cleanup also logs event 8004, which gives a second, external record to compare against.

## Where the protection ends

- Events can be changed in the seconds between being written and being sealed.
- Without checkpoints in an external log system, a rebuilt chain cannot be detected.
- Someone with write access to the database can insert a forged `system.audit-chain-truncated` event before cutting off the start of the chain. Only comparing these events with the logged events 8004 reveals that.

To use the chain, see [Check the integrity of the audit log](../how-to/verify-the-audit-log.md).
