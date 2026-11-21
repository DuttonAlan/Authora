# Connect an application

This guide shows how to let users of your application sign in with Authora through OpenID Connect. Any certified OpenID Connect library works; this guide gives the settings Authora needs and examples for ASP.NET Core and for single-page applications.

## 1. Register the Client

In the admin interface, open **Clients** → **Register Client**:

| Your application | Type | Redirect URI |
|---|---|---|
| Server-side web app (ASP.NET Core, Spring, Django, Node with sessions, …) | `Confidential` | e.g. `https://app.example.com/signin-oidc` |
| Single-page app, mobile or desktop app | `Public` | e.g. `https://app.example.com/callback`, or `http://127.0.0.1:{port}/callback` for desktop apps |

- Allow the Scopes the application needs, at least `openid`. Add `profile` for the name and `email` for the Email address.
- Keep the Grant Type `Refresh Token` only if the application needs tokens while the user is away.
- Redirect URIs must match exactly, including the path and a trailing slash. They must use HTTPS; plain HTTP is only allowed for `127.0.0.1` and `[::1]`.
- To sign users out of Authora from the application, also enter a **Post-logout Redirect URI**: where Authora sends the user back after signing out, e.g. `https://app.example.com/signout-callback-oidc` for ASP.NET Core. Same rules as Redirect URIs.

For a Confidential Client, Authora shows the **Client Secret once**, right after registration. Store it in your application's secret store. Secrets expire after one year by default; create a new one under **Secrets** before that, deploy it, then revoke the old one.

## 2. Configure the library

| Setting | Value |
|---|---|
| Authority / issuer | Your Issuer, e.g. `https://auth.example.com` |
| Discovery | `https://auth.example.com/.well-known/openid-configuration` |
| Client ID | The **Client Id** shown in the admin interface |
| Client Secret | Confidential Clients only. Sent as `client_secret_basic` or `client_secret_post`. |
| Response type | `code` |
| Response mode | `query` (the only one Authora supports) |
| PKCE | Required, method `S256`. Only if the library cannot send it: an administrator can make it optional for a Confidential Client, which must then check the `nonce` (see [PKCE](../reference/oauth-endpoints.md#pkce)). |
| Scopes | e.g. `openid profile email` |
| ID Token signature | `RS256` |

Things libraries sometimes assume that Authora does not do:

- **No `form_post` response mode.** Some libraries use it by default; switch them to `query`.
- **No `offline_access` Scope.** A Client gets a Refresh Token whenever its Grant Types include `Refresh Token`.
- **Refresh does not return a new ID Token.** Keep the claims from sign-in, or call UserInfo.
- **`email_verified` is always `false`**, because Authora does not verify Email addresses yet. Do not use the Email to link accounts; use `sub`.
- **No Roles or Permissions in tokens.** Authora's Roles and Permissions only control access to Authora's own admin API; see [the access model](../explanation/access-model.md).

## Example: ASP.NET Core

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.Authority = "https://auth.example.com";
        options.ClientId = builder.Configuration["Authora:ClientId"];
        options.ClientSecret = builder.Configuration["Authora:ClientSecret"];

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.Query; // the default, form_post, is not supported
        options.UsePkce = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.SaveTokens = true;
    });
```

Register `https://app.example.com/signin-oidc` (the handler's default callback path) as the Redirect URI, and `https://app.example.com/signout-callback-oidc` as the Post-logout Redirect URI.

To sign out, sign out of both schemes. The OpenID Connect handler then sends the browser to Authora's `end_session_endpoint` with the saved ID Token as `id_token_hint` (that is why `SaveTokens` is on), Authora ends its Session, and the browser comes back to `/signout-callback-oidc` and then to `RedirectUri`:

```csharp
await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
await HttpContext.SignOutAsync(
    OpenIdConnectDefaults.AuthenticationScheme,
    new AuthenticationProperties { RedirectUri = "/" });
```

To sign out of the application only and keep the Authora Session, sign out of the cookie scheme alone.

## Example: single-page application

With [`oidc-client-ts`](https://github.com/authts/oidc-client-ts):

```js
import { UserManager } from 'oidc-client-ts';

const userManager = new UserManager({
  authority: 'https://auth.example.com',
  client_id: '…',
  redirect_uri: 'https://app.example.com/callback',
  post_logout_redirect_uri: 'https://app.example.com/',
  response_type: 'code',
  scope: 'openid profile email',
});

await userManager.signinRedirect();                 // on "Sign in"
const user = await userManager.signinCallback();    // on the /callback page
await userManager.signoutRedirect();                // on "Sign out": ends the Authora Session too
```

`signoutRedirect` sends the ID Token as `id_token_hint`, so Authora signs the user out without asking. Register the `post_logout_redirect_uri` as a Post-logout Redirect URI of the Client.

Authora allows cross-origin calls to `/oauth/token`, `/oauth/revoke` and `/oauth/userinfo` from the origins of the Redirect URIs of enabled Clients, so a browser app can call them directly.

## 3. Test it

Sign in through your application. The first time, Authora shows a consent screen that lists the requested Scopes. Afterwards it remembers the approval and asks again only for Scopes the user has not approved yet, or when your application sends `prompt=consent`. Users withdraw it under *Connected applications* (`/account/consents`). In the admin interface, the **Audit log** shows the sign-in.

## Troubleshooting

| Symptom | Cause |
|---|---|
| Authora shows *The request cannot be processed* | Unknown or disabled Client, or a `redirect_uri` that does not exactly match a registered one. Authora never redirects to an unregistered URI, so it shows the error itself. |
| `error=invalid_scope` at the Redirect URI | The application asked for a Scope that is not allowed for the Client. |
| `error=invalid_request` mentioning PKCE | `code_challenge` missing, or the method is not `S256`. |
| `invalid_client` from `/oauth/token` | Wrong or expired Secret, or both `client_secret_basic` and `client_secret_post` sent. |
| `invalid_grant` from `/oauth/token` | The code expired (after 1 minute), was already used, or the `code_verifier` or `redirect_uri` does not match. A `code_verifier` sent for a code requested without `code_challenge` is rejected too. |
| *The request cannot be processed* on sign-out | The `post_logout_redirect_uri` is not registered as a Post-logout Redirect URI of the Client, or the `id_token_hint` was not issued by this Authora. |
| Authora asks *Sign out of Authora?* | The sign-out request had no `id_token_hint`, or one from an earlier sign-in. Send the ID Token of the current sign-in to skip the question. |
| Sign-in page error about cookies or antiforgery | Authora is reached over HTTP, or a proxy is not trusted; see [Configure a reverse proxy](configure-a-reverse-proxy.md). |

The exact rules for each parameter are in the [OAuth endpoints reference](../reference/oauth-endpoints.md).
