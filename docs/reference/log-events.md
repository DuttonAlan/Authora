# Log events

Authora writes structured logs through the standard .NET logging. Every message has a stable **Event Id**, so you can alert on it. Codes, tokens, Secrets, passwords, key material and nonces are never logged; objects appear only by Id.

## Ranges

| Range | Area |
|---|---|
| 1001 – 1014 | Users (1009: an administrator sent a password reset link; 1010 – 1013: email verification, see below; 1014: an administrator reset multi-factor authentication, see below) |
| 2001 – 2005 | Roles |
| 3001 – 3003 | Scopes |
| 4001 – 4022 | Clients |
| 5001 – 5026 | Sign-in, sign-out (also at the request of a Client), lockout, Sessions, password changes by the User (5011: the notification email could not be sent), password reset by email (5012 – 5016, see below), email verification (5017 – 5018 and 5025 – 5026, see below), second factor and step-up (5019 – 5024, see below) |
| 6001 – 6019 | Codes, Grants, refreshes, reuse detection, revocation, Client authentication, rejected authorization and logout requests, codes of ended Sessions, Grants of codes whose Consent was revoked meanwhile (6016), required multi-factor authentication and `acr_values` (6017 – 6019, see below) |
| 7001 – 7009 | Seeding at startup |
| 8001 – 8006 | Cleanup of expired data (including email tokens and, as 8005 and 8006, pending sign-ins and passkey challenges) and audit events |
| 9001 – 9005 | Signing keys |
| 10001 – 10007 | Groups |
| 11001 – 11011 | Hash chain of the audit log |
| 12001 – 12002 | Admin interface sign-in and refresh |
| 13001 | Admin API changes denied because they exceed the caller's rights |
| 14001 – 14006, 14011 – 14014, 14021 – 14026 | Email delivery; one-time tokens of email links; background delivery and background handling of reset requests |
| 15001 – 15002 | Consents granted on the consent screen and revoked by the User or an administrator |
| 16001 – 16010 | Multi-factor authentication: authenticator app and recovery codes, see below |
| 17001 – 17009 | Passkeys, see below |

## Email

Addresses, content, tokens and SMTP credentials are never logged. Emails appear by their `Message-ID`, tokens by their Id.

| Event Id | Level | Message (shortened) | Meaning |
|---|---|---|---|
| 14001 | Information | Email delivery is enabled through … with TLS mode … | Logged at startup. |
| 14002 | Information | Email delivery is disabled, because `Authora:Email:Host` is not set | Logged at startup. Features that send email are off. |
| 14003 | Information | The SMTP server accepted email … | The server took the email; delivery to the mailbox is up to it. |
| 14004 | Error | Email … could not be sent through …: … | The server was unreachable, its certificate was not trusted, authentication failed, it rejected the email (with the SMTP status code), or the timeout passed; the reason says which. The server's reply is not logged, because it can quote addresses. |
| 14005 | Warning | Email delivery is unencrypted (TLS mode None) | Only possible in `Development`. |
| 14006 | Warning | Closing the connection after email … failed | The server had already accepted the email, so it counts as sent. |
| 14011 | Information | Issued email token … for … to User … | A link was created; older unused links of the User for the same purpose stop working. |
| 14012 | Information | Email token … for … of User … was used | |
| 14013 | Warning | An unknown email token was presented for … | A mistyped or forged link. Many in a row suggest guessing. |
| 14014 | Warning | Email token … of User … was rejected for …: … | Expired, already used or replaced, for another purpose, or the User's address or security stamp (e.g. after a password change) changed since it was sent. Also logged when a reset link is only opened, not used. |
| 14021 | Error | The email queue is full (… emails). The email "…" is not sent | Emails sent in the background (notifications after a reset, verification links after a User was created or their Email changed) wait in memory; the mail server is too slow or unreachable. |
| 14022 | Error | The queued email "…" could not be sent: … | A background email failed and is not retried. 14004 has the details. |
| 14023 | Warning | … queued emails were not sent, because the server is stopping | Queued emails are kept only in memory. |
| 14024 | Error | The queue of password reset requests is full (… requests). The request is dropped | Requests at `/account/forgot-password` wait in memory until the background handling looks up the address and sends the link. Nothing is stored or counted for a dropped request. |
| 14025 | Error | A queued password reset request failed and is not retried | E.g. the database was unavailable. The exception has the details. |
| 14026 | Warning | … queued password reset requests were not handled, because the server is stopping | Queued requests are kept only in memory. |

## Password reset

| Event Id | Level | Message (shortened) | Meaning |
|---|---|---|---|
| 5012 | Information | A password reset link was sent to User … | Requested at `/account/forgot-password`; logged by the background handling once the mail server accepted the email. |
| 5013 | Warning | A password reset was requested for an unknown Email | Nothing was sent. Many in a row suggest probing for accounts. |
| 5014 | Warning | No password reset link was sent to User …: … | The User cannot sign in, the address got `Authora:PasswordReset:MaxEmailsPerHour` links in the last hour, or the email could not be sent (14004 has the reason). |
| 5015 | Information | User … set a new password with a reset link | All Sessions and tokens of the User were revoked, a lockout ended. |
| 5016 | Warning | A password reset link was not accepted: … | 14013 or 14014 has the reason. |
| 1009 | Information | A password reset link was sent to User … at the request of an administrator | `POST /api/users/{id}/password-reset`. |

## Email verification

| Event Id | Level | Message (shortened) | Meaning |
|---|---|---|---|
| 1010 | Information | An email verification link was sent to User … at the request of an administrator | `POST /api/users/{id}/email-verification`; logged once the mail server accepted the email. |
| 1011 | Information | An email verification link for User … was queued | The User was created or their Email changed. A delivery failure is logged later as 14022. |
| 1012 | Warning | No email verification link was sent to User …, because the email queue is full | 14021 has the details. An administrator can send the link again. |
| 1013 | Information | The Email of User … was marked as verified by an administrator | `POST /api/users/{id}/email-verified`. |
| 5017 | Information | User … confirmed their Email with a verification link | `email_verified` is `true` from now on. |
| 5018 | Warning | An email verification link was not accepted: … | 14013 or 14014 has the reason. |
| 5025 | Information | User … asked for a new email verification link, which was sent | At `/account/email-verification`; logged once the mail server accepted the email. |
| 5026 | Warning | No email verification link was sent to User … at their request: … | Too many links in the last hour (`Authora:EmailVerification:MaxEmailsPerHour`), or the email could not be sent (14004 has the reason). |

## Second factor and step-up

| Event Id | Level | Message (shortened) | Meaning |
|---|---|---|---|
| 5019 | Information | User … entered the correct password; sign-in … waits for a second factor | A pending sign-in started; no Session yet. |
| 5020 | Warning | Second factor … of User … was wrong in sign-in … | Counts towards the lockout like a wrong password. Many in a row suggest that someone knows the password. |
| 5021 | Information | Pending sign-in … was not completed: … | It was already completed, expired, its User cannot sign in or has a new security stamp, no factor is left, or the Session of a step-up ended. The User signs in again. |
| 5022 | Information | User … started a step-up … of Session … | At `/account/step-up`. |
| 5023 | Information | User … completed … for Session …, which got a new token | The step-up is complete. |
| 5024 | Information | User … holds a Permission and must set up a second factor … | `Authora:Mfa:RequireForAdministrators` is on; the User is sent to set up an authenticator app after the password. |
| 6017 | Information | Authorization request of Client … requires multi-factor authentication, which the Session of User … lacks: … | `StepUp` or `Enrollment`: the Client or the admin Scope requires a second factor. |
| 6018 | Information | Authorization request of Client … asks for acr …, so User … is asked for another factor | `acr_values` asks for a level the User can reach with a factor the Session lacks. |
| 6019 | Information | Authorization Code … was rejected, because Client … requires multi-factor authentication … | The code was issued before *Require MFA* was turned on for the Client. |
| 8005 | Information | Deleted … pending sign-ins that expired before … | Written by the cleanup, without the retention. |
| 8006 | Information | Deleted … passkey challenges that expired before … | Written by the cleanup, without the retention. |

## Multi-factor authentication

Codes, recovery codes and secrets are never logged.

| Id | Level | Message | Meaning |
|---|---|---|---|
| 16001 | Information | User … started to set up an authenticator app | At `/account/security/authenticator`; not a second factor until confirmed. |
| 16002 | Information | User … set up an authenticator app and got … recovery codes | From now on the User enters a code after the password. |
| 16003 | Information | User … entered a valid code of their authenticator app | In the second step of a sign-in or step-up, or to remove the app. |
| 16004 | Warning | A code of User … was rejected: … | Wrong code, code already used (a replay), wrong or already used recovery code, no app set up, or the secret cannot be decrypted. Counts towards the lockout. |
| 16005 | Warning | User … used a recovery code; … left | The User may have lost their phone, or someone else has the codes. |
| 16006 | Information | User … generated new recovery codes | The previous codes no longer work. |
| 16007 | Information | User … removed their authenticator app in Session … | All other Sessions and all tokens of the User were revoked. |
| 16008 | Warning | Removing the authenticator app of User … failed: … | Wrong code, or the User is locked out. |
| 16009 | Error | The authenticator secret of User … cannot be decrypted | The Data Protection key ring was lost or replaced, see [Persist Data Protection keys](../how-to/persist-data-protection-keys.md). The User can only sign in with a recovery code or after a reset. |
| 16010 | Information | User … must complete the authenticator app in Session … before generating new recovery codes | The Session was signed in with the password alone, e.g. before the app was set up; the User is sent to the step-up. |
| 1014 | Information | The multi-factor authentication of User … was reset by an administrator | `DELETE /api/users/{id}/mfa`. All Sessions and tokens of the User were revoked. |

## Passkeys

Challenges, public keys and responses are never logged, only the Ids of passkeys.

| Id | Level | Message | Meaning |
|---|---|---|---|
| 17001 | Information | User … registered passkey … | At `/account/security/passkeys`. From now on the User can use it after the password. |
| 17002 | Warning | Registering a passkey for User … failed: … | Malformed response, unknown, used or expired challenge, wrong origin, already registered, or too many passkeys. |
| 17003 | Information | User … renamed passkey … | |
| 17004 | Information | User … removed passkey … in Session … | All other Sessions and all tokens of the User were revoked. |
| 17005 | Information | User … verified with passkey … | In the second step of a sign-in or step-up. |
| 17006 | Warning | A passkey response of User … was rejected: … | Malformed response, unknown, used or expired challenge (e.g. a replay), not a passkey of the User, wrong signature or origin. Counts towards the lockout. |
| 17007 | Warning | The signature counter of passkey … of User … did not increase … | The authenticator may have been cloned. The response is rejected; the passkey stays registered until the User or an administrator removes it. |
| 17008 | Information | Passkey … of User … was removed by an administrator | `DELETE /api/users/{id}/passkeys/{passkeyId}`. All Sessions and tokens of the User were revoked. |
| 17009 | Information | User … must complete a second factor in Session … before changing their passkeys | The Session was signed in with the password alone, e.g. before the User had a second factor; the User is sent to the step-up. |

## Events worth alerting on

| Event Id | Level | Message (shortened) | Meaning |
|---|---|---|---|
| 5004 | Warning | User … was locked out … | Possible password guessing. |
| 5020 | Warning | Second factor … of User … was wrong … | The password was correct; someone may know it. |
| 16005 | Warning | User … used a recovery code … | Unexpected for the User if they still have their phone. |
| 16009 | Error | The authenticator secret of User … cannot be decrypted | The Data Protection key ring is missing; Users with an authenticator app cannot sign in with it. |
| 17006 | Warning | A passkey response of User … was rejected … | The password was correct; the response was replayed, came from another site, or was signed by another key. |
| 17007 | Warning | The signature counter of passkey … did not increase … | The authenticator may have been cloned; the sign-in was rejected. |
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
| 14021 | Error | The email queue is full | Notifications are dropped until the mail server catches up. |
| 14024 | Error | The queue of password reset requests is full | Requests at `/account/forgot-password` are dropped until the background handling catches up. |
| 14025 | Error | A queued password reset request failed | E.g. the database was unavailable; the User asks again. |
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
