# Audit events

Authora records security-relevant events in the table `audit.events`. Events are only ever added: neither the API nor the admin interface can change or delete them, and a [hash chain](../explanation/audit-log-integrity.md) detects changes made in the database. The cleanup deletes them after `Authora:Cleanup:AuditRetention` (365 days by default).

## Event types

| Area | Types |
|---|---|
| Sign-in and account | `account.sign-in` (succeeded, with the Session and its authentication methods in the Details, e.g. `Session … (pwd otp)`, or failed with the reason: unknown Email, wrong password, locked out, cannot sign in, wrong second factor), `account.sign-in-pending` (the password was correct and the sign-in waits for the User's second factor, or a signed-in User started a step-up at `/account/step-up`; the Details name the pending sign-in), `account.step-up` (a signed-in User completed an additional factor for their Session, which got a new token; the Details name the Session and its methods. Failed with the reason: wrong second factor, locked out), `account.locked-out` (after failed sign-ins, wrong second factors or wrong current passwords), `account.signed-out` (at `/account/logout`; the Grants approved in the Session were revoked), `account.signed-out-by-client` (RP-initiated logout; the Details name the Client and whether the User confirmed or an `id_token_hint` of the Session was sent; the Grants approved in the Session were revoked), `account.password-changed` (a User changed their own password at `/account/password`; the Details name the Session that stays valid, all others and all tokens of the User were revoked. Failed with the reason: wrong current password, locked out), `account.password-reset-requested` (someone asked for a reset link at `/account/forgot-password`; succeeded when a link was sent, with its expiry in the Details. Failed with the reason: unknown Email (without a target), the User cannot sign in, too many reset links for the address in the last hour, the email could not be sent. Recorded by the background handling of the request, with the IP address of the request that queued it), `account.password-reset` (a User set a new password with a reset link; all Sessions and tokens of the User were revoked and a lockout ended. Failed with the reason: unknown, expired, used or replaced link (without a target), the User cannot sign in), `account.email-verified` (a User confirmed their Email with a verification link at `/account/verify-email`. Failed with the reason: unknown, expired, used or replaced link (without a target), the User cannot sign in), `account.email-verification-requested` (a signed-in User asked for a new verification link at `/account/email-verification` and it was sent, with its expiry in the Details. Failed with the reason: too many links in the last hour, the email could not be sent) |
| Multi-factor authentication | `mfa.enrolled` (a User set up an authenticator app and got 10 recovery codes), `mfa.removed` (a User removed it at `/account/security/authenticator/remove`; all other Sessions and tokens ended. Failed with the reason: wrong code, locked out), `mfa.verified` (a code of the authenticator app was accepted, or failed with the reason: wrong code, code already used, wrong or already used recovery code, no app set up, secret cannot be decrypted. Recorded for the second step of a sign-in or step-up and for the removal, in addition to their own events), `mfa.recovery-code-used` (a recovery code stood in for the app; the Details say how many are left), `mfa.recovery-codes-regenerated` (a User generated a new set; the previous codes stopped working). All have the User as their target. A reset by an administrator is `user.mfa-reset`. Codes and secrets are never recorded. |
| Passkeys | `passkey.registered` (a User registered a passkey at `/account/security/passkeys`; the Details name it with its method `hwk` or `swk` and the AAGUID of the authenticator. Failed with the reason: malformed response, unknown, used or expired challenge, wrong origin, already registered), `passkey.renamed`, `passkey.removed` (a User removed a passkey; all other Sessions and tokens ended), `passkey.used` (a passkey was accepted for the second step of a sign-in or step-up, in addition to their own events), `passkey.failed` (a passkey response was rejected, with the reason: malformed response, unknown, used or expired challenge, not a passkey of the User, wrong signature or origin, or a signature counter that did not increase, a sign of a cloned authenticator). All have the User as their target and name the passkey by its Id. Removals by administrators are `user.passkey-removed` and `user.mfa-reset`. Public keys, challenges and responses are never recorded. |
| External sign-in | `account.sign-in` with method `fed` (a linked User signed in through an external identity provider; the Details name the Session, e.g. `Session … (fed)`, and the provider by Slug and Id. With a second factor, `account.sign-in-pending` comes first, naming the provider, and the completed sign-in has `fed otp` or similar), `account.external-sign-in-failed` (outcome `Failed`: no flow cookie or it expired, wrong `state`, an `iss` parameter of another Issuer, an error returned by the provider (its code only), no code, a Client Secret that cannot be decrypted, the token request failed, an invalid ID Token (wrong signature or unknown key, `iss`, `aud`, `azp`, expired, `iat` outside the sign-in, missing `nonce`), the provider was disabled, the account is not linked and sign-up is turned off, sign-up refused because the provider reported no verified Email or the domain (for Google: the Workspace domain `hd`) is not allowed, the account is not linked but has the Email of a User who must sign in to link it (target: that User), the pending link was completed by another User, the User cannot sign in or is locked out, a link conflicts with an existing one or arrived in another Session. The target is the User if known, otherwise the provider. Tokens, codes and the Subject at the provider are never recorded), `external-identity.linked` (an account at a provider was linked; the Details name the link, the provider and how: `via account page` when the signed-in User linked it at `/account/security/external-accounts`, `via sign-in prompt` when the account had the User's verified Email and the User then signed in with their existing methods to link it), `external-identity.unlinked` (the link was removed, `by the User` or `by an administrator`; existing Sessions stay). The last two have the User as their target. |
| Identity providers | `identity-provider.created`, `identity-provider.updated` (also when the Client Secret was set or removed, which the Details say; the secret itself is never recorded), `identity-provider.deleted`, `identity-provider.enabled`, `identity-provider.disabled`. All have the provider as their target and name its Slug. |
| Consents | `consent.granted` (a User approved Scopes for a third-party Client on the consent screen that their Consent did not contain yet; the Details name the Client and the added Scopes), `consent.revoked` (by the User at `/account/consents` or by an administrator; the Details name the Client, the number of revoked Grants and the Scopes). Both have the User as their target. `actorClientId` of `consent.granted` is the approved Client; of `consent.revoked` it is the Client the revocation came through, e.g. the admin interface, and empty for the account page. |
| Attacks on tokens | `oauth.authorization-code-reused`, `oauth.refresh-token-reused` |
| Users | `user.created`, `user.provisioned` (a User was created on their first sign-in through an identity provider that allows sign-up, without a password and with the account linked; the Details name the provider, the link and the default Groups the User joined), `user.updated`, `user.deleted`, `user.activated`, `user.deactivated`, `user.unlocked`, `user.sessions-revoked`, `user.password-set`, `user.mfa-reset` (an administrator removed the authenticator app, the recovery codes and the passkeys; all Sessions and tokens ended. The Details say what was removed), `user.passkey-removed` (an administrator removed one passkey; all Sessions and tokens ended. The Details name the passkey), `user.password-reset-sent` (an administrator sent a reset link; recorded once the mail server accepted the email, or failed if it could not be sent), `user.email-verification-sent` (a verification link was sent. By an administrator: recorded once the mail server accepted the email, or failed if it could not be sent. By itself after `user.created` or a `user.updated` that changed the Email to a different address: recorded once queued, with `Sent automatically, because …` in the Details, or failed if the queue was full), `user.email-marked-verified` (an administrator marked the Email as verified), `user.role-assigned`, `user.role-removed` |
| Groups | `group.created`, `group.updated`, `group.deleted`, `group.member-added`, `group.member-removed`, `group.role-assigned`, `group.role-removed` |
| Roles | `role.created`, `role.updated`, `role.deleted`, `role.permission-granted`, `role.permission-revoked` |
| Scopes | `scope.created`, `scope.updated`, `scope.deleted` |
| Clients | `client.registered`, `client.updated` (the Details name the Client, its Back-Channel Logout URI, and its Resource Identifier with whether the resource parameter is required), `client.deleted`, `client.enabled`, `client.disabled`, `client.redirect-uri-added`, `client.redirect-uri-removed`, `client.post-logout-redirect-uri-added`, `client.post-logout-redirect-uri-removed`, `client.scope-allowed`, `client.scope-disallowed`, `client.scope-served`, `client.scope-no-longer-served`, `client.secret-created`, `client.secret-revoked`, `client.marked-first-party`, `client.marked-third-party`, `client.token-issued` (an Access Token for the Client itself, `client_credentials`; the Details name the Grant, the expiry, the audience as Client Ids, with Authora as the nil UUID `00000000-0000-0000-0000-000000000000`, or only their number beyond 10, and the Scopes), `client.pkce-made-optional`, `client.pkce-required`, `client.mfa-required`, `client.mfa-no-longer-required`, `client.backchannel-logout-failed` (Authora gave up sending a Logout Token to the Client's Back-Channel Logout URI after the last attempt, or without sending once older than `Authora:BackchannelLogout:MaxAge` or for an HTTP URI while HTTP is not allowed; outcome `Failed`, without an actor; the Details name the User, the number of attempts and the last error. The Client may still consider the User signed in) |
| Authora | `system.administrator-bootstrapped`, `system.client-bootstrapped`, `system.signing-keys-changed`, `system.audit-chain-truncated` |

## Fields

| Field | Content |
|---|---|
| `occurredAt` | Time (UTC). |
| `type` | One of the types above. |
| `outcome` | `Succeeded` or `Failed`. |
| `actorUserId`, `actorClientId` | The User and Client of the Access Token or Session that caused the event. At the token endpoint, the Client that called it. Empty for events of Authora itself. |
| `ipAddress` | The client's IP address. Behind a reverse proxy, only correct with `Authora:ForwardedHeaders`. |
| `targetType`, `targetId` | The object: `user`, `group`, `role`, `scope`, `client`, `identity-provider` or `signing-key`, and its Id (`kid` for keys). |
| `details` | A short description, e.g. the name of an assigned Role. |

The Email of the acting User and the name of the acting Client are looked up when reading, so they are current and not copied into the log.

## Rules

- Changes are recorded in the same database transaction as the change itself, so an event exists if and only if the change was saved.
- Requests that change nothing (e.g. assigning a Role that is already assigned) and rejected requests record nothing, except for the cases below.
- Back-channel logout notifications are not recorded when they are queued or delivered, only when Authora gives up (`client.backchannel-logout-failed`); the event that ended the Session, e.g. `account.signed-out` or `user.sessions-revoked`, stands for the notification. Deliveries are in the [log](log-events.md#back-channel-logout).
- Failed sign-ins, failed password changes, password reset requests, reset and verification links that send or change nothing, `user.password-reset-sent` and `user.email-verification-sent` (after the email is sent or queued), token reuse and admin API changes denied because they exceed the caller's rights (see [Nobody grants more than they hold](admin-api.md#nobody-grants-more-than-they-hold)) are recorded immediately, independent of other changes, with outcome `Failed`. A denied change has the type of the attempted change, e.g. `user.role-assigned`, and the reason in the Details.
- Sign-in events have the User as their *target*, because nobody is signed in yet when they occur. Search by `targetId` to see a User's sign-ins. `account.password-changed` also has the User as its target, and as its actor. So do `account.password-reset-requested`, `account.password-reset` and `account.email-verified`, without an actor, and `account.email-verification-requested`, with the signed-in User as its actor.
- **Never recorded:** passwords, Session tokens, codes, reset and verification links and their tokens, Access and Refresh Tokens, Client Secrets, private keys. The Email of a failed sign-in or reset request for an unknown account is not recorded either, because it may be a password typed into the wrong field, or the address of someone without an account.

## Search

`GET /api/audit-events`, Permission `audit.read`. The newest events come first.

| Parameter | Matches |
|---|---|
| `type` | A type (`user.created`) or an area (`user` matches `user.*`). |
| `actorUserId` | Events caused by this User. |
| `targetType`, `targetId` | Events about this object. |
| `outcome` | `Succeeded` or `Failed`. |
| `from`, `to` | `from` ≤ time < `to`, ISO 8601 with offset, e.g. `2026-10-01T00:00:00Z`. |
| `page`, `pageSize` | Page from 1; 1 to 200 events per page, default 50. |

## Verification

`GET /api/audit-events/verification`, Permission `audit.verify`. Recomputes the hash chain from its first remaining link.

| Parameter | Description |
|---|---|
| `checkpointSequence`, `checkpointHash` | Optional. A checkpoint from log event 11001, to detect a rebuilt or cut-off chain. |

| Response member | Description |
|---|---|
| `isIntact` | `true` if the chain verifies. |
| `firstSequence`, `lastSequence`, `lastHash` | The range of the chain and the hash of its last link. |
| `eventsChecked` | Number of links checked. |
| `brokenAtSequence`, `problem` | Where and why the chain is broken, if it is. |
| `unsealedEvents` | Events older than the sealing interval plus a minute that are not in the chain. Should be `0`. |
| `checkpointMatches` | Whether the chain matches the given checkpoint; `null` without one. |

See [Check the integrity of the audit log](../how-to/verify-the-audit-log.md).
