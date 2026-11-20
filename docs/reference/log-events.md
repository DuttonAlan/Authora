# Log events

Authora writes structured logs through the standard .NET logging. Every message has a stable **Event Id**, so you can alert on it. Codes, tokens, Secrets, passwords, key material and nonces are never logged; objects appear only by Id.

## Ranges

| Range | Area |
|---|---|
| 1001 – 1008 | Users |
| 2001 – 2005 | Roles |
| 3001 – 3003 | Scopes |
| 4001 – 4020 | Clients |
| 5001 – 5008 | Sign-in, sign-out (also at the request of a Client), lockout, Sessions |
| 6001 – 6015 | Codes, Grants, refreshes, reuse detection, revocation, Client authentication, rejected authorization and logout requests, codes of ended Sessions |
| 7001 – 7009 | Seeding at startup |
| 8001 – 8004 | Cleanup of expired data (including email tokens) and audit events |
| 9001 – 9005 | Signing keys |
| 10001 – 10007 | Groups |
| 11001 – 11011 | Hash chain of the audit log |
| 12001 – 12002 | Admin interface sign-in and refresh |
| 13001 | Admin API changes denied because they exceed the caller's rights |
| 14001 – 14006, 14011 – 14014 | Email delivery; one-time tokens of email links |

## Email

Addresses, content, tokens and SMTP credentials are never logged. Emails appear by their `Message-ID`, tokens by their Id.

| Event Id | Level | Message (shortened) | Meaning |
|---|---|---|---|
| 14001 | Information | Email delivery is enabled through … with TLS mode … | Logged at startup. |
| 14002 | Information | Email delivery is disabled, because `Authora:Email:Host` is not set | Logged at startup. Features that send email are off. |
| 14003 | Information | The SMTP server accepted email … | The server took the email; delivery to the mailbox is up to it. |
| 14004 | Error | Email … could not be sent through …: … | The server was unreachable, its certificate was not trusted, authentication failed, it rejected the email (with the SMTP status code), or the timeout passed; the reason says which. The server's reply is not logged, because it can quote addresses. |
| 14005 | Warning | Email delivery is unencrypted (TLS mode None) | Only possible in `Development` and `Testing`. |
| 14006 | Warning | Closing the connection after email … failed | The server had already accepted the email, so it counts as sent. |
| 14011 | Information | Issued email token … for … to User … | A link was created; older unused links of the User for the same purpose stop working. |
| 14012 | Information | Email token … for … of User … was used | |
| 14013 | Warning | An unknown email token was presented for … | A mistyped or forged link. Many in a row suggest guessing. |
| 14014 | Warning | Email token … of User … was rejected for …: … | Expired, already used or replaced, for another purpose, or the User's address changed since it was sent. |

## Events worth alerting on

| Event Id | Level | Message (shortened) | Meaning |
|---|---|---|---|
| 5004 | Warning | User … was locked out … | Possible password guessing. |
| 6004 | Warning | Authorization Code … was used more than once | Possible code theft; the Grant was revoked. |
| 6005 | Warning | Refresh Token … was used more than once | Possible token theft; the Grant was revoked. |
| 6006 | Warning | Client … presented a code or token that was issued to Client … | A Client used another Client's token. |
| 6007 | Warning | Authentication of Client … failed | Wrong or expired Client Secret. |
| 7006 | Warning | No administrator who can sign in exists | Nobody can manage Authora. |
| 7007 | Warning | … a bootstrap password is still configured | Remove `Authora:Bootstrap:Administrator:Password`. |
| 7009 | Warning | The Client of the admin interface was not created | The Issuer gives an invalid Redirect URI; the admin interface cannot be used. |
| 8002 | Error | The cleanup of expired data failed | Retried with the next run. |
| 9003 | Error | The changed key files could not be loaded | The previous keys stay in use; health check `signing-keys` is `Degraded`. |
| 9004 | Warning | The signing key file … can be read or written by other users | Restrict the file to the user that runs Authora. |
| 14004 | Error | Email … could not be sent through … | The email did not reach the User. Check the SMTP settings and the server. |
| 13001 | Warning | User … was denied a change of … | A User tried to give out or act on more rights than they hold, see [Admin API](admin-api.md#nobody-grants-more-than-they-hold). Possibly a compromised account. |
| 11002 | Error | Sealing the audit events failed | Retried with the next run. |
| 11011 | Critical | The audit chain is broken at Sequence … | The audit log was changed in the database. |

## Events to keep

| Event Id | Level | Message (shortened) | Why |
|---|---|---|---|
| 11001 | Information | Sealed … audit events. Checkpoint: the audit chain ends at Sequence … with hash … | Checkpoints to verify the audit log against. Keep them outside Authora's database. |
| 8004 | Information | The hash chain of the audit log starts at Sequence … | Written only by the cleanup; compare with `system.audit-chain-truncated` events. |
| 9001, 9002 | Information, Warning | ID Tokens are signed with key … | Which key signs, after startup and after each change. |
| 7004, 7005, 7008 | Warning, Information | Created the first administrator / a Client | Changes made by the seeding at startup. |

## Log level

`appsettings.json` sets `Information` as the default level, and `Warning` for `Microsoft.AspNetCore`. At `Information`, Entity Framework Core also logs its SQL commands (without parameter values). To reduce the volume:

```sh
Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning
```
