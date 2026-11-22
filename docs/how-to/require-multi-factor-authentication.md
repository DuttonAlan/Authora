# Require multi-factor authentication

This guide shows how to get your Users, above all your administrators, to sign in with an authenticator app in addition to their password, how to check who has done so, and how to help someone who lost their phone. Each admin task names the Permission it needs; the `admin` Role has all of them.

> Authora cannot yet enforce multi-factor authentication: each User decides whether to set up an authenticator app, and ID Tokens do not yet tell applications whether a sign-in used one (no `amr` claim). Until then, "require" means asking Users to set it up and checking that they did. See [Standards and limitations](../reference/standards.md#not-supported).

## Before you start

The secrets of authenticator apps are encrypted with the ASP.NET Core Data Protection keys. If those keys are lost, every authenticator app stops working at once.

1. Persist the keys and back them up: [Persist Data Protection keys](persist-data-protection-keys.md) and [Back up and restore](back-up-and-restore.md). The Docker Compose setup and the container image already keep them in a volume at `/var/lib/authora/keys`; include that volume in your backups.
2. Optionally, set the name the apps show next to the User's Email, e.g. your company:

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

## Check who has set it up

- **One User:** open the User in the admin interface (`users.read`). The panel **Two-step verification** shows whether the authenticator app is on, when a code was last used and how many recovery codes are left. The API returns the same from `GET /api/users/{id}/mfa`, see [Admin API](../reference/admin-api.md#users).
- **Everyone who set it up recently:** search the audit log (`audit.read`) for the type `mfa.enrolled`:

  ```sh
  curl -H "Authorization: Bearer $TOKEN" "https://auth.example.com/api/audit-events?type=mfa.enrolled&from=2026-11-01T00:00:00Z"
  ```

  `type=mfa` lists all events of the area, including removals (`mfa.removed`) and resets by administrators (`user.mfa-reset` is in the area `user`). See [Audit events](../reference/audit-events.md).

## Help a User who lost their phone

1. **The User still has recovery codes:** they enter one instead of the code on the second step of the sign-in. Each code works once. Afterwards they remove the old app on their account page (with another recovery code) and set up the new phone, or generate a fresh set of codes under **Recovery codes** if they keep the app (a sign-in with a recovery code counts as having the app).
2. **The User has no recovery code left:** reset their multi-factor authentication (`users.reset-mfa`). Open the User → **Two-step verification** → **Reset two-step verification**, or call:

   ```sh
   curl -X DELETE -H "Authorization: Bearer $TOKEN" "https://auth.example.com/api/users/$USER_ID/mfa"
   ```

   This removes the app and the recovery codes and signs the User out everywhere. They then sign in with the password alone and set up the new phone.

Before you reset, make sure the request really comes from the User, e.g. by calling them back on a number you already know: a reset removes the protection the second factor gives. Only administrators can reset an administrator, and nobody can reset a User who has more rights than they do, see [Nobody grants more than they hold](../reference/admin-api.md#nobody-grants-more-than-they-hold).

## Watch for warning signs

These log events are worth an alert, see [Log events](../reference/log-events.md#events-worth-alerting-on):

- **5020 / 16004** – wrong or reused codes after a correct password: someone may know the password. Repeated failures lock the User out like wrong passwords.
- **16005** – a recovery code was used: expected after a lost phone, suspicious otherwise.
- **16009** – a secret cannot be decrypted: the Data Protection keys are missing. Restore them before anything else.
