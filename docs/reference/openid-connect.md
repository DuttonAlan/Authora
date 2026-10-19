# OpenID Connect

Authora is an OpenID Connect provider on top of its [OAuth endpoints](oauth-endpoints.md). This page covers discovery, Scopes and claims, the authorization request parameters of OpenID Connect, ID Tokens, signing keys and UserInfo.

| Endpoint | Purpose |
|---|---|
| `GET /.well-known/openid-configuration` | Discovery document |
| `GET /.well-known/jwks.json` | Public signing keys (JWKS) |
| `GET/POST /oauth/userinfo` | Claims of the User of an Access Token |

The discovery document and the JWKS are public, allow CORS from every origin (`GET`, no credentials) and may be cached for one hour (`Cache-Control: public, max-age=3600`). `/oauth/userinfo` allows CORS from the origins of the Redirect URIs of enabled Clients, `GET` and `POST`, the `Authorization` header and no credentials.

## Discovery

All URLs in the document are built from `Authora:OAuth:Issuer`, so they always match `issuer`. The document lists only what Authora supports:

| Member | Value |
|---|---|
| `response_types_supported` | `code` |
| `response_modes_supported` | `query` |
| `grant_types_supported` | `authorization_code`, `refresh_token` |
| `code_challenge_methods_supported` | `S256` |
| `token_endpoint_auth_methods_supported`, `revocation_endpoint_auth_methods_supported` | `client_secret_basic`, `client_secret_post`, `none` |
| `introspection_endpoint_auth_methods_supported` | `client_secret_basic`, `client_secret_post` |
| `id_token_signing_alg_values_supported` | `RS256` |
| `subject_types_supported` | `public` |
| `scopes_supported` | `openid`, `profile`, `email` |
| `prompt_values_supported` | `none`, `login`, `consent`, `select_account` |
| `request_parameter_supported`, `request_uri_parameter_supported`, `claims_parameter_supported` | `false` |
| `authorization_response_iss_parameter_supported` | `true` |

## Scopes and claims

| Scope | Claims |
|---|---|
| `openid` | `sub`: the User Id, the same for every Client |
| `profile` | `name` (first and last name), `given_name`, `family_name` |
| `email` | `email`, `email_verified` (always `false`; Authora does not verify Emails yet) |

`openid`, `profile` and `email` are System Scopes that Authora creates. Like every Scope, an administrator must allow them for a Client, and the User approves them on the consent screen (unless the Client is first-party).

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
| `request`, `request_uri`, `registration` | Not supported: `request_not_supported`, `request_uri_not_supported`, `registration_not_supported`. |
| `display`, `ui_locales`, `login_hint`, `id_token_hint`, `acr_values`, `claims_locales`, `claims` | Ignored. |

| `prompt` | Behavior |
|---|---|
| `none` | No page is shown. `login_required` if the User is not signed in or must sign in again. Otherwise a first-party Client gets the code; any other Client gets `consent_required`, because consent is asked on every request. |
| `login` | The User signs in again, even if signed in. |
| `select_account` | Same as `login`; there is no account chooser. |
| `consent` | The consent screen is shown, also for a first-party Client. |

If the sign-in is older than `max_age`, the User signs in again. This is checked when the consent screen is shown and again when it is approved.

`prompt` and `max_age` travel through the browser, which can remove them. Clients that need a recent sign-in, e.g. before a sensitive action, must compare the `auth_time` claim with the time they sent the request.

## ID Tokens

Returned by `POST /oauth/token` when exchanging a code whose Scopes include `openid`. Refreshing returns no ID Token.

| Claim | Value |
|---|---|
| `iss` | `Authora:OAuth:Issuer` |
| `sub` | User Id |
| `aud` | `client_id` |
| `iat`, `exp` | Issue time, and issue time plus `Authora:Oidc:IdTokenLifetime` (5 minutes) |
| `auth_time` | When the User signed in (start of the Session) |
| `nonce` | The `nonce` of the authorization request, if sent |
| `at_hash` | Hash of the Access Token |
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
