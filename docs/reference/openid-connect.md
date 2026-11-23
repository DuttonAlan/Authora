# OpenID Connect

Authora is an OpenID Connect provider on top of its [OAuth endpoints](oauth-endpoints.md). This page covers discovery, Scopes and claims, the authorization request parameters of OpenID Connect, ID Tokens, signing keys, UserInfo, logout and back-channel logout.

| Endpoint | Purpose |
|---|---|
| `GET /.well-known/openid-configuration` | Discovery document |
| `GET /.well-known/jwks.json` | Public signing keys (JWKS) |
| `GET/POST /oauth/userinfo` | Claims of the User of an Access Token |
| `GET/POST /oauth/logout` | End-session endpoint: RP-initiated logout, see [Logout](#logout) |

The discovery document and the JWKS are public, allow CORS from every origin (`GET`, no credentials) and may be cached for one hour (`Cache-Control: public, max-age=3600`). `/oauth/userinfo` allows CORS from the origins of the Redirect URIs of enabled Clients, `GET` and `POST`, the `Authorization` header and no credentials.

## Discovery

All URLs in the document are built from `Authora:OAuth:Issuer`, so they always match `issuer`. The document lists only what Authora supports:

| Member | Value |
|---|---|
| `response_types_supported` | `code` |
| `response_modes_supported` | `query` |
| `grant_types_supported` | `authorization_code`, `refresh_token`, `client_credentials` |
| `code_challenge_methods_supported` | `S256` |
| `token_endpoint_auth_methods_supported`, `revocation_endpoint_auth_methods_supported` | `client_secret_basic`, `client_secret_post`, `none` |
| `introspection_endpoint_auth_methods_supported` | `client_secret_basic`, `client_secret_post` |
| `id_token_signing_alg_values_supported` | `RS256` |
| `subject_types_supported` | `public` |
| `scopes_supported` | `openid`, `profile`, `email` |
| `prompt_values_supported` | `none`, `login`, `consent`, `select_account` |
| `acr_values_supported` | `urn:authora:acr:pwd`, `urn:authora:acr:mfa`, `phr` (passkeys) |
| `request_parameter_supported`, `request_uri_parameter_supported`, `claims_parameter_supported` | `false` |
| `authorization_response_iss_parameter_supported` | `true` |
| `end_session_endpoint` | `{Issuer}/oauth/logout` |
| `backchannel_logout_supported`, `backchannel_logout_session_supported` | `true`, see [Back-channel logout](#back-channel-logout) |

## Scopes and claims

| Scope | Claims |
|---|---|
| `openid` | `sub`: the User Id, the same for every Client |
| `profile` | `name` (first and last name), `given_name`, `family_name` |
| `email` | `email`, `email_verified`: `true` once the User confirmed the address with a verification link or an administrator marked it as verified; `false` otherwise, and again after the Email changes to a different address (see [Email verification](oauth-endpoints.md#email-verification)) |

`openid`, `profile` and `email` are System Scopes that Authora creates. Like every Scope, an administrator must allow them for a Client, and the User approves them on the consent screen (unless the Client is first-party or the User approved them before).

- ID Tokens and UserInfo return the same claims for the same Scopes.
- Claims Authora has no value for (e.g. `picture`, `birthdate`) are never returned, not even as `null`.
- Roles, Groups and Permissions are never released.
- The names `openid`, `profile` and `email` are reserved. Authora refuses to start if a regular (non-system) Scope with one of these names exists.

## Authorization request

In addition to the [OAuth parameters](oauth-endpoints.md#authorization-request):

| Parameter | Rule |
|---|---|
| `nonce` | Optional, at most 512 characters. Returned unchanged in the ID Token. |
| `prompt` | Optional. `none` must not be combined with other values. Unknown values are ignored. |
| `max_age` | Optional, a non-negative number of seconds. |
| `acr_values` | Optional, space-delimited levels of [Authentication levels](#authentication-levels), in order of preference. Unknown values are ignored. |
| `request`, `request_uri`, `registration` | Not supported: `request_not_supported`, `request_uri_not_supported`, `registration_not_supported`. |
| `display`, `ui_locales`, `login_hint`, `id_token_hint`, `claims_locales`, `claims` | Ignored. |

| `prompt` | Behavior |
|---|---|
| `none` | No page is shown. `login_required` if the User is not signed in or must sign in again; `interaction_required` if the Session lacks a factor the request needs (see [Step-up](#step-up-and-required-mfa)). Otherwise the code is sent if no consent is needed: the Client is first-party, or the User's Consent contains every requested Scope. If not, `consent_required`. |
| `login` | The User signs in again, even if signed in. |
| `select_account` | Same as `login`; there is no account chooser. |
| `consent` | The consent screen is shown, also for a first-party Client and when the User's Consent already contains the requested Scopes. |

If the sign-in is older than `max_age`, the User signs in again. This is checked when the consent screen is shown and again when it is approved.

`prompt`, `max_age` and `acr_values` travel through the browser, which can remove them. Clients that need a recent sign-in, e.g. before a sensitive action, must compare the `auth_time` claim with the time they sent the request; Clients that need a second factor must check `acr` or `amr`, or have an administrator turn on *Require MFA* for them.

## Authentication levels

The `acr` claim names the level the sign-in reached; a stronger level satisfies every weaker one.

| `acr` | Reached when the Session has | Strength |
|---|---|---|
| `urn:authora:acr:pwd` | the password only | weakest |
| `urn:authora:acr:mfa` | at least two factors, e.g. `pwd` and `otp` | |
| `phr` (OpenID EAP ACR Values 1.0) | a passkey (`hwk` or `swk`) | strongest |

The `amr` claim ([RFC 8176](https://www.rfc-editor.org/rfc/rfc8176)) lists the methods in the order they were completed: `pwd` (password), `otp` (code of an authenticator app or a recovery code), `hwk`/`swk` (passkeys), plus `mfa` when at least two were used.

## Step-up and required MFA

Before a code is issued, the Session of a signed-in User is checked:

| Rule | Session not strong enough, User has another factor | User has no second factor | `prompt=none` |
|---|---|---|---|
| `acr_values` (voluntary) | Asked for the factor that gets closest to a requested level (step-up) | Gets the strongest level they reach; the Client sees it in `acr` | `interaction_required` if a step-up would be needed |
| Client with *Require MFA* (see [Admin API](admin-api.md#clients)) | Step-up | Sets up an authenticator app first | `interaction_required` |
| `Authora:Mfa:RequireForAdministrators` and the request contains `authora.admin`, for a User who holds a Permission | Step-up | Sets up an authenticator app first | `interaction_required` |

`acr_values` is satisfied by its weakest known value: `urn:authora:acr:mfa urn:authora:acr:pwd` accepts a password-only Session. A step-up adds the factor to the existing Session (`auth_time` becomes the time of the step-up) and returns to the same authorization request. After setting up an authenticator app, the User completes it once more as a step-up. Codes issued without a second factor cannot be exchanged after the Client was set to *Require MFA*: `invalid_grant`.

## ID Tokens

Returned by `POST /oauth/token` when exchanging a code whose Scopes include `openid`. Refreshing returns no ID Token.

| Claim | Value |
|---|---|
| `iss` | `Authora:OAuth:Issuer` |
| `sub` | User Id |
| `aud` | `client_id` |
| `iat`, `exp` | Issue time, and issue time plus `Authora:Oidc:IdTokenLifetime` (5 minutes) |
| `auth_time` | When the User last authenticated in the Session: the sign-in, or a later step-up |
| `nonce` | The `nonce` of the authorization request, if sent |
| `at_hash` | Hash of the Access Token |
| `amr` | The methods of the Session, e.g. `["pwd", "otp", "mfa"]`, see [Authentication levels](#authentication-levels) |
| `acr` | The level of the Session, e.g. `urn:authora:acr:mfa`, always sent |
| `sid` | The Session ID: a random, opaque value that identifies the User's Session at Authora, always sent. The same for every Client in that Session. [Logout Tokens](#back-channel-logout) carry the same value. |
| identity claims | See [Scopes and claims](#scopes-and-claims) |

The header contains `alg: RS256`, `typ: JWT` and the `kid` of the signing key. Clients must validate the signature with the JWKS, and `iss`, `aud`, `exp` and `nonce`.

ID Tokens are never stored and are never accepted as Access Tokens.

## Signing keys and JWKS

ID Tokens are signed with an RSA key (at least 2048 bits) from the PEM file at `Authora:Oidc:SigningKeyPath`. Keys are never stored in the database.

- The JWKS contains the signing key and the keys of `Authora:Oidc:AdditionalKeyPaths`, with only `kty`, `use`, `alg`, `kid`, `n` and `e`.
- The `kid` of each key is its JWK Thumbprint ([RFC 7638](https://www.rfc-editor.org/rfc/rfc7638)), so it is the same on every instance and after restarts.
- Key files are re-read every `Authora:Oidc:KeyReloadInterval`. See [Rotate signing keys](../how-to/rotate-signing-keys.md).

## UserInfo

`GET` or `POST /oauth/userinfo` with `Authorization: Bearer <access token>`. The token must be active and contain `openid`. The response contains `sub` and the claims of the token's Scopes.

| Case | Response |
|---|---|
| No `Authorization: Bearer` header | `401`, `WWW-Authenticate: Bearer` |
| Unknown, expired or revoked token, or an ID Token | `401`, `WWW-Authenticate: Bearer error="invalid_token"` |
| Token without `openid` | `403`, `WWW-Authenticate: Bearer error="insufficient_scope", scope="openid"` |

Responses carry `Cache-Control: no-store`.

## Logout

`GET /oauth/logout` ([OpenID Connect RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html)): a Client sends the browser here to end the User's Session at Authora. `POST` with the parameters as a form also works; it is redirected (`303`) to the same request as GET, like at the [authorization endpoint](oauth-endpoints.md#authorization-request), so the browser sends the Session cookie.

| Parameter | Rule |
|---|---|
| `id_token_hint` | Recommended. An ID Token Authora issued to the Client: signed with a published key, header `typ: JWT`, `iss` is Authora. **Expired tokens are accepted**, as the specification asks, because Clients usually sign out long after the ID Token expired. |
| `client_id` | Optional. Must name an existing Client, and the `aud` of `id_token_hint` if both are sent. |
| `post_logout_redirect_uri` | Optional. Needs `client_id` or `id_token_hint`. Must exactly match a Post-logout Redirect URI registered for that Client, which must be enabled. |
| `state` | Optional. Returned unchanged with the redirect. |
| `logout_hint`, `ui_locales` | Ignored. |

| Request | Result |
|---|---|
| Invalid: a parameter sent twice, an `id_token_hint` Authora did not issue, an unknown, disabled or mismatching Client, an unregistered `post_logout_redirect_uri` | An error page (`400`). Never redirected, so Authora cannot be used as an open redirector. |
| The User is not signed in | Nothing to sign out: redirect, or the page *You are signed out*. |
| `id_token_hint` of the current Session: same `sub`, and `auth_time` of the current sign-in | Signed out without a question. |
| Anything else, e.g. no hint, or a hint from an earlier sign-in or of another User | A confirmation page: *Sign out of Authora?* with the Client's Name. *Sign out* ends the Session; *Stay signed in* keeps it. Both then return to the Client. |

After the decision, the browser is sent (`303`) to `post_logout_redirect_uri` with `state` if one was sent, otherwise Authora shows the result. The confirmation keeps other sites from signing Users out with a link; it needs an antiforgery token.

Signing out ends the Session at Authora, so every application asks the User to sign in again. It also revokes the Grants approved in that Session, with their Access and Refresh Tokens, of every Client, and notifies the Clients with a Back-Channel Logout URI, see [Back-channel logout](#back-channel-logout). Tokens of Grants from other Sessions and of the Client Credentials Grant stay valid. Signing out is recorded as the audit event `account.signed-out-by-client`.

Post-logout Redirect URIs are registered per Client in the admin interface or with `POST /api/clients/{id}/post-logout-redirect-uris`, with the same rules as Redirect URIs (absolute, no fragment, HTTPS or a loopback IP). Like Redirect URIs, none can be added while a Client is first-party (`409 Conflict`): no code or token is sent there, but the Client Id of the admin interface is public, so a URI of one's own would turn its logout into an open redirect on Authora's origin.

## Back-channel logout

[OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html): when a User's Session at Authora ends, Authora sends a Logout Token server to server to every Client that received tokens in that Session and has a Back-Channel Logout URI, so the Client can end its own session for the User even if the browser never visits it again. How to receive it: [Receive back-channel logout](../how-to/receive-back-channel-logout.md).

A Client registers its Back-Channel Logout URI with `PUT /api/clients/{id}` (`backchannelLogoutUri`, `backchannelLogoutSessionRequired`, see the [admin API](admin-api.md)) or in the admin interface. The URI must be absolute, without fragment or user information, and use HTTPS (plain HTTP only with `Authora:BackchannelLogout:AllowHttp`, which only the environment `Development` allows).

### When Clients are notified

Each of the following ends Sessions explicitly. It revokes them and the Grants approved in them (Access and Refresh Tokens, of every Client), and queues one notification per Session and Client with a Back-Channel Logout URI:

| Trigger | Sessions |
|---|---|
| Sign-out at `/account/logout` or `/oauth/logout` | The current Session |
| Admin API: revoke sessions, deactivate, delete, set the password, reset MFA, remove a passkey of a User | All Sessions of the User |
| Password reset with an email link | All Sessions of the User |
| Password change, removing the authenticator app or a passkey at `/account/security` | All other Sessions of the User |

A Session that ends because its lifetime is over (`Authora:SignIn:SessionLifetime`) or it was not used for `Authora:SignIn:SessionIdleTimeout` ends **without** a notification and keeps its Grants: Clients see the expiry of their own tokens. The Grants of the Client Credentials Grant have no Session and are never revoked by a logout.

Sessions from before Authora recorded the Session ID count as ended, so Users sign in again after the upgrade.

### Logout Token

`POST` to the Back-Channel Logout URI with `Content-Type: application/x-www-form-urlencoded` and the single parameter `logout_token`: a JWT signed like ID Tokens (`alg: RS256`, the `kid` of the current signing key, published in the JWKS), with the header `typ: logout+jwt`, so it is never accepted as an ID Token.

| Claim | Value |
|---|---|
| `iss` | `Authora:OAuth:Issuer` |
| `aud` | `client_id` |
| `iat`, `exp` | When the token was sent, and two minutes later. Each attempt gets a new token. |
| `jti` | A new unique value for every token, also for a retry of the same notification |
| `sub` | User Id |
| `sid` | The `sid` of the ID Tokens issued in the Session; always sent |
| `events` | `{"http://schemas.openid.net/event/backchannel-logout": {}}` |

A Logout Token never contains a `nonce`.

### Delivery

Notifications are written to a database outbox in the same transaction that ends the Session, so they survive restarts and are sent by any instance. Every instance looks for due notifications every `Authora:BackchannelLogout:PollInterval` (5 seconds); an instance claims a notification before sending it, so several instances never send it at the same time.

- Any `2xx` response counts as delivered. Redirects are not followed and count as failed, like other status codes, network errors and no response within `Authora:BackchannelLogout:Timeout` (5 seconds).
- A failed delivery is retried after 10 seconds, then with a delay that doubles every time, up to one hour.
- Authora gives up after `Authora:BackchannelLogout:MaxAttempts` attempts (10), or once the Session ended more than `Authora:BackchannelLogout:MaxAge` (1 day) ago, and records the audit event `client.backchannel-logout-failed` and the log event 18013.
- If the Client is deleted or its URI removed before the notification is sent, the notification is dropped. The current URI of the Client is used, not the one it had when the Session ended.

All settings: [Configuration](configuration.md#back-channel-logout).
