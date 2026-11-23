# Require multi-factor authentication

This guide shows how to make your Users, above all your administrators, sign in with a second factor in addition to their password, an authenticator app or a passkey: by requiring it for administrators, for single applications, or by letting an application ask for it. It also shows how to check who has set it up and how to help someone who lost their phone or security key. Each admin task names the Permission it needs; the `admin` Role has all of them.

## Before you start

The secrets of authenticator apps are encrypted with the ASP.NET Core Data Protection keys. If those keys are lost, every authenticator app stops working at once. Passkeys store only public keys and do not depend on them.

1. Persist the keys and back them up: [Persist Data Protection keys](persist-data-protection-keys.md) and [Back up and restore](back-up-and-restore.md). The Docker Compose setup and the container image already keep them in a volume at `/var/lib/authora/keys`; include that volume in your backups.
2. Decide on the final host name of the Issuer (`Authora:OAuth:Issuer`) before Users add passkeys. A passkey is bound to that host (the WebAuthn relying party Id) and only works on pages of the Issuer's origin over HTTPS; after a move to another host, every passkey stops working and Users add them again.
3. Optionally, set the name the apps and passkey dialogs show, e.g. your company:

   ```sh
   Authora__Mfa__IssuerName=Example Corp
   ```

   It defaults to `Authora`. See [Configuration](../reference/configuration.md#multi-factor-authentication).

## Ask Users to set up an authenticator app

Send your Users the link to their account page on Authora, e.g. `https://auth.example.com/account/security`. No Permission is needed. There they choose **Set up an authenticator app** and:

1. Sign in again if they signed in more than 10 minutes ago (`Authora:SignIn:RecentAuthenticationWindow`).
2. Scan the QR code with an authenticator app, e.g. the one built into their password manager, or type in the key shown below it.
3. Enter the code the app shows and select **Turn on**.
4. Save the 10 recovery codes shown next, e.g. in their password manager. They are shown only once.

From then on, they enter a code of the app after their password. Each code works once.

Start with the administrators: an administrator account is the most valuable one to protect, and its owners can best help others afterwards.

## Ask Users to add a passkey

A passkey is the stronger second factor: it only works on Authora's own site, so it cannot be phished like a code. On the same account page, Users choose **Add a passkey** and:

1. Sign in again if they signed in more than 10 minutes ago. If they already have an authenticator app but signed in without it, they first enter a code of the app.
2. Give the passkey a name, e.g. *Phone* or *Security key*, and select **Add a passkey**.
3. Confirm in the dialog of the browser, e.g. with their fingerprint, face, screen lock or by touching the security key.

From then on, the second step of the sign-in offers **Use a passkey**, next to the code of the app if they have both. Users can add up to 20 passkeys and should add at least two (e.g. the phone and a security key, or one synced by their password manager): there are no recovery codes for passkeys. They rename and remove passkeys on the same page; removing one signs them out everywhere else.

A Session signed in with a passkey records `hwk` (a device-bound passkey, e.g. on a security key) or `swk` (a passkey that may be synced to other devices), and reaches the level `phr`. The page needs JavaScript; Authora loads it only from its own origin, see [HTTP security](../reference/http-security.md#headers).

## Require it for administrators

Turn on the requirement for every User who holds a Permission of the admin API (any Role with a Permission, not only `admin`):

```sh
Authora__Mfa__RequireForAdministrators=true
```

See [Configuration](../reference/configuration.md#multi-factor-authentication). After a restart:

- An administrator without a second factor is sent to set up an authenticator app (or add a passkey instead) right after entering their password, so nobody is locked out, the bootstrap administrator included. They then complete it once more to open the admin interface.
- An administrator whose Session has the password only, e.g. because it started before they set up the app, completes a second factor before the admin interface (or any Client asking for `authora.admin`) gets a code.
- The admin API rejects Access Tokens of these Users that were issued for a sign-in with the password only, whichever Client holds them, with `403` and `insufficient_user_authentication`. The admin interface then sends the User through the sign-in again. Scripts that call the API as an administrator need a token from a sign-in with two factors.

Users without any Permission are not affected. The setting is off by default so upgrades keep working; [Deploy to production](deploy-to-production.md) recommends it.

## Require it for an application

For an application that handles sensitive data, require a second factor for everyone who uses it (`clients.update`): open the Client in the admin interface and select **Require MFA**, or call:

```sh
curl -X PUT -H "Authorization: Bearer $TOKEN" "https://auth.example.com/api/clients/$CLIENT_ID/mfa-required"
```

From then on, a User whose Session has only the password completes a second factor before the application gets a code, and a User without one sets one up first. Requests with `prompt=none` get `interaction_required` instead. The change is recorded as `client.mfa-required`. **Allow without MFA** (`DELETE …/mfa-required`) turns it off again.

## Let an application ask for it

An application can ask for a second factor itself with `acr_values`, without an administrator, and check the result in the ID Token:

```text
/oauth/authorize?…&acr_values=urn:authora:acr:mfa
```

A User who has an authenticator app but signed in with the password only enters a code first. A User without one is not stopped: the request is voluntary, and the application sees `"acr": "urn:authora:acr:pwd"` in the ID Token and decides itself, e.g. by asking the User to set up an app. Every ID Token contains `amr` (e.g. `["pwd", "otp", "mfa"]`) and `acr`, see [OpenID Connect](../reference/openid-connect.md#authentication-levels).

For a phishing-resistant sign-in, ask for `acr_values=phr`: a User with a passkey who signed in without it uses it first, and the ID Token then contains `"acr": "phr"` and `amr` with `hwk` or `swk`.

## Check who has set it up

- **One User:** open the User in the admin interface (`users.read`). The panel **Two-step verification** shows whether the authenticator app is on, when a code was last used, how many recovery codes are left, and each passkey with its name, whether it is synced, and when it was added and last used. The API returns the same from `GET /api/users/{id}/mfa`, see [Admin API](../reference/admin-api.md#users).
- **Everyone who set it up recently:** search the audit log (`audit.read`) for the type `mfa.enrolled` (authenticator apps) or `passkey.registered` (passkeys):

  ```sh
  curl -H "Authorization: Bearer $TOKEN" "https://auth.example.com/api/audit-events?type=mfa.enrolled&from=2026-11-01T00:00:00Z"
  ```

  `type=mfa` and `type=passkey` list all events of the areas, including removals (`mfa.removed`, `passkey.removed`) and changes by administrators (`user.mfa-reset` and `user.passkey-removed` are in the area `user`). See [Audit events](../reference/audit-events.md).

## Help a User who lost their phone

1. **The User still has recovery codes:** they enter one instead of the code on the second step of the sign-in. Each code works once. Afterwards they remove the old app on their account page (with another recovery code) and set up the new phone, or generate a fresh set of codes under **Recovery codes** if they keep the app (a sign-in with a recovery code counts as having the app).
2. **The User has no recovery code left:** reset their multi-factor authentication (`users.reset-mfa`). Open the User → **Two-step verification** → **Reset two-step verification**, or call:

   ```sh
   curl -X DELETE -H "Authorization: Bearer $TOKEN" "https://auth.example.com/api/users/$USER_ID/mfa"
   ```

   This removes the app, the recovery codes and all passkeys and signs the User out everywhere. They then sign in with the password alone and set up the new phone.
3. **The User lost a security key or a device with a passkey, but has another second factor:** they remove the lost passkey themselves under **Passkeys** after signing in with the other one. If they cannot, remove just that passkey (`users.reset-mfa`): open the User → **Two-step verification** → **Remove** next to the passkey, or call:

   ```sh
   curl -X DELETE -H "Authorization: Bearer $TOKEN" "https://auth.example.com/api/users/$USER_ID/passkeys/$PASSKEY_ID"
   ```

   Their other passkeys and the authenticator app stay; the User is signed out everywhere. If the lost passkey was their only second factor, reset as in step 2.

Before you reset, make sure the request really comes from the User, e.g. by calling them back on a number you already know: a reset removes the protection the second factor gives. Only administrators can reset an administrator, and nobody can reset a User who has more rights than they do, see [Nobody grants more than they hold](../reference/admin-api.md#nobody-grants-more-than-they-hold).

## Watch for warning signs

These log events are worth an alert, see [Log events](../reference/log-events.md#events-worth-alerting-on):

- **5020 / 16004** – wrong or reused codes after a correct password: someone may know the password. Repeated failures lock the User out like wrong passwords.
- **16005** – a recovery code was used: expected after a lost phone, suspicious otherwise.
- **16009** – a secret cannot be decrypted: the Data Protection keys are missing. Restore them before anything else.
- **17006** – a passkey response was rejected after a correct password, e.g. a replayed response or one from another site.
- **17007** – the signature counter of a passkey did not increase: the authenticator may have been cloned. The sign-in is rejected; ask the User whether they recognize it and remove the passkey if in doubt.
