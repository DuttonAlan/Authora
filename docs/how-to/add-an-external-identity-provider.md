# Add an external identity provider

This guide shows how to let Users sign in to Authora through another OpenID Connect provider, e.g. your company's identity provider, in addition to their password. Authora is a Relying Party of the provider and signs the User in to an ordinary Authora Session, so applications connected to Authora need no change. For Google, follow [Sign in with Google](sign-in-with-google.md) instead.

You need the Permission `identity-providers.manage` (the `admin` Role has it), and an administrator account at the provider to register an application there.

## How it works for Users

- The sign-in page shows a button "Sign in with *Name*" for every enabled provider.
- A User signs in through a provider once their account there is linked. Accounts are matched by the provider's `sub`, never by Email alone. An account that is not linked yet is handled in this order:
  1. **Its verified Email belongs to a User**: Authora shows "An account with this email already exists. Sign in to link it." The account is linked only after the person signs in to that User with its existing methods (password and second factor, if set up); then the sign-in continues where it started, e.g. at the application. Abandoning it links nothing.
  2. **Sign-up is allowed** for the provider, the provider verified the Email (`email_verified=true`) and its domain is allowed: a new User is created on the spot, see [Let people sign up](#let-people-sign-up).
  3. **Otherwise** the sign-in is refused with "Sign-in with *Name* is not possible for this account." The reason is only in the audit log and the logs.
- Signed-in Users can also link an account themselves at **Account and security → Linked accounts** (`/account/security/external-accounts`), after a recent sign-in, and unlink it there. A User without a password cannot unlink their last account.
- The provider replaces the password, not the second factor: a User with an authenticator app or a passkey still completes it after the provider. ID Tokens name `fed` in `amr`.
- Signing out of Authora does not sign the User out of the provider.

## 1. Choose a Slug and note the Redirect URI

The Slug identifies the provider in URLs, e.g. `company`: lowercase letters, digits and hyphens. It cannot be changed later, because it is part of the Redirect URI:

```text
{Issuer}/account/external/{slug}/callback
```

For example `https://auth.example.com/account/external/company/callback`, with `Authora:OAuth:Issuer` as the Issuer.

## 2. Register Authora at the provider

At the provider, register a web application (often called a *Client* or *App registration*) with:

- **Redirect URI**: the URI from step 1, exactly.
- **Grant type / flow**: Authorization Code. Authora always sends PKCE (`S256`).
- **Scopes**: `openid`, plus `email` and `profile` if you want the provider's Email shown next to the link.
- **Client authentication**: a Client Secret with `client_secret_basic` (Authora falls back to `client_secret_post` if the provider's discovery document lists only that), or none if the provider treats Authora as a Public Client.
- **ID Token signing**: `RS256`, `ES256` or `PS256`.

Note the **Issuer** (the `issuer` of the provider's `/.well-known/openid-configuration`), the **Client Id** and the **Client Secret**.

## 3. Create the provider in Authora

In the admin interface, open **Security → Identity providers → New provider**, choose the kind *OpenID Connect provider*, and enter the values from steps 1 and 2. Or with the [admin API](../reference/admin-api.md#identity-providers):

```sh
curl -X POST https://auth.example.com/api/identity-providers \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
    "name": "Company login",
    "slug": "company",
    "preset": "Generic",
    "issuer": "https://login.example.com",
    "clientId": "authora",
    "clientSecret": "…"
  }'
```

Authora fetches `{issuer}/.well-known/openid-configuration` once and refuses the provider (`400`) if the document cannot be fetched, names another `issuer`, lacks an endpoint or key set, or the Issuer is not HTTPS. The response contains the `redirectUri`; compare it with the one you registered.

The Client Secret is encrypted with the Data Protection keys and never shown again. Keep the keys: without them the provider fails until you set the secret again, see [Persist Data Protection keys](persist-data-protection-keys.md). To replace it, use **Set secret** on the provider's page or `PUT /api/identity-providers/{id}/secret`.

Your servers must be able to reach the provider over HTTPS (discovery, key set and token endpoint; the default proxy settings of the host apply), within `Authora:ExternalSignIn:Timeout`, see [Configuration](../reference/configuration.md#external-identity-providers).

## 4. Link an account and try it

1. Sign in to Authora with your password and open `/account/security/external-accounts`.
2. Choose **Link Company login**, sign in at the provider, and come back to "The account was linked".
3. Sign out, and sign in again with **Sign in with Company login**.

Failed sign-ins are recorded as `account.external-sign-in-failed` with the reason, see [Audit events](../reference/audit-events.md), and logged as event 20006, see [Log events](../reference/log-events.md#external-identity-providers).

## Let people sign up

By default only existing Users sign in through a provider. With sign-up turned on, a person whose account at the provider is not linked, and whose Email belongs to no User, gets a User on their first sign-in (just-in-time provisioning):

- the Email reported by the provider, marked as verified;
- the First and Last Name from `given_name` and `family_name`, or the part of the Email before `@` if the provider sends none;
- **no password**: they sign in through the provider, and can set a password later at **Account and security → Set a password** (after a recent sign-in) or with **Forgot password?**;
- membership in the provider's **default Groups**, and through them their Roles;
- the account at the provider linked, and a Session, so the sign-in continues right away.

Only an Email the provider marks as verified (`email_verified=true`) counts. **Allowed email domains** limit sign-up to Emails of these domains, e.g. `example.com`; a domain matches exactly, so list subdomains separately. Without domains, anyone with an account at the provider can sign up: only leave the list empty for a provider whose accounts are all yours, e.g. your company's own identity provider, never for a public one.

In the admin interface, open the provider and use **Sign-up**. Or with the [admin API](../reference/admin-api.md#identity-providers):

```sh
curl -X PUT https://auth.example.com/api/identity-providers/$PROVIDER_ID \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
    "allowSignUp": true,
    "allowedEmailDomains": ["example.com"],
    "defaultGroupIds": ["0199d1a2-7c3e-7b4a-9f10-3c963f66afa6"]
  }'
```

A deleted Group is removed from the default Groups. Default Groups that grant Permissions make every new User an administrator of that kind, and if multi-factor authentication is required for administrators, they set up a second factor right after signing up. New Users are recorded as `user.provisioned`, refused sign-ups as `account.external-sign-in-failed` with the reason (sign-up turned off, no verified Email, domain not allowed), see [Audit events](../reference/audit-events.md).

## Change or remove a provider

- **Order of the buttons**: `sortOrder` (lower first), then the Name.
- **Disable** a provider to hide it and reject sign-ins through it at once, also those already running. Linked accounts are kept for when you enable it again.
- **Delete** only works while no accounts are linked through it (`409` otherwise): disable it instead, or ask the Users to unlink first.
- **Issuer**: a new Issuer is checked like on creation. Linked accounts stay linked by their `sub`, so only change the Issuer for the same provider (e.g. a new host name), never to another provider.

Whoever can manage providers can point one at an Issuer they control and sign in as every User who linked an account through it, or turn on sign-up with default Groups that grant Permissions. Give `identity-providers.manage` only to administrators.
