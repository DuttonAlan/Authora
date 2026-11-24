# Add an external identity provider

This guide shows how to let Users sign in to Authora through another OpenID Connect provider, e.g. your company's identity provider, in addition to their password. Authora is a Relying Party of the provider and signs the User in to an ordinary Authora Session, so applications connected to Authora need no change. For Google, follow [Sign in with Google](sign-in-with-google.md) instead.

You need the Permission `identity-providers.manage` (the `admin` Role has it), and an administrator account at the provider to register an application there.

## How it works for Users

- The sign-in page shows a button "Sign in with *Name*" for every enabled provider.
- A User can only sign in through a provider after linking their account there: signed in with their password, at **Account and security → Linked accounts** (`/account/security/external-accounts`). Linking needs a recent sign-in. An account that is not linked is turned away with "This account is not linked. Sign in with your password to link it."
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

## Change or remove a provider

- **Order of the buttons**: `sortOrder` (lower first), then the Name.
- **Disable** a provider to hide it and reject sign-ins through it at once, also those already running. Linked accounts are kept for when you enable it again.
- **Delete** only works while no accounts are linked through it (`409` otherwise): disable it instead, or ask the Users to unlink first.
- **Issuer**: a new Issuer is checked like on creation. Linked accounts stay linked by their `sub`, so only change the Issuer for the same provider (e.g. a new host name), never to another provider.

Whoever can manage providers can point one at an Issuer they control and sign in as every User who linked an account through it. Give `identity-providers.manage` only to administrators.
