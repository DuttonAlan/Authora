# Sign in with Google

This guide shows how to let Users sign in to Authora with their Google account, in addition to their password. It is the Google variant of [Add an external identity provider](add-an-external-identity-provider.md), which explains how the sign-in through a provider works for Users.

You need the Permission `identity-providers.manage` (the `admin` Role has it) and a Google Cloud project.

## 1. Create an OAuth client at Google

1. In the [Google Cloud console](https://console.cloud.google.com/), open **APIs & Services → OAuth consent screen** and set it up: the app name Users see at Google, and the Scopes `openid`, `email` and `profile`. These Scopes need no verification by Google.
2. Open **APIs & Services → Credentials → Create credentials → OAuth client ID**, with the application type **Web application**.
3. Under **Authorized redirect URIs**, add the Redirect URI of Authora, with the Slug `google`:

   ```text
   https://auth.example.com/account/external/google/callback
   ```

   Use your `Authora:OAuth:Issuer` in place of `https://auth.example.com`. Google requires HTTPS, except for `http://localhost`.
4. Create it and note the **Client ID** and the **Client secret**.

## 2. Create the provider in Authora

In the admin interface, open **Security → Identity providers → New provider**, choose the kind **Google**, enter the Name `Google`, the Slug `google`, the Client ID and the Client secret, and create it. The Issuer is always `https://accounts.google.com`.

Or with the [admin API](../reference/admin-api.md#identity-providers):

```sh
curl -X POST https://auth.example.com/api/identity-providers \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
    "name": "Google",
    "slug": "google",
    "preset": "Google",
    "clientId": "1234567890-abc.apps.googleusercontent.com",
    "clientSecret": "GOCSPX-…"
  }'
```

Authora fetches Google's discovery document once to check it can reach it; your servers need outgoing HTTPS to `accounts.google.com` and `oauth2.googleapis.com`. Compare the `redirectUri` of the response with the one you registered at Google.

The secret is encrypted with the Data Protection keys and never shown again, see [Persist Data Protection keys](persist-data-protection-keys.md).

## 3. Link a Google account and try it

1. Sign in to Authora with your password and open `/account/security/external-accounts`.
2. Choose **Link Google**, pick your Google account, and come back to "The account was linked".
3. Sign out, and sign in again with **Sign in with Google**.

Authora identifies the Google account by its `sub`, not by its Email, so a User whose Google address matches their Authora Email still has to link it once. While the consent screen of your Google project is in *Testing*, only the test users you added there can sign in; publish it for everyone else.
