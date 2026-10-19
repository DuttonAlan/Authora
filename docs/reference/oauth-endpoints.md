# OAuth endpoints

Authora is an OAuth 2.1 authorization server. This page lists its endpoints and the exact rules for their parameters. OpenID Connect additions (discovery, ID Tokens, UserInfo) are in [OpenID Connect](openid-connect.md).

| Endpoint | Called by | Purpose |
|---|---|---|
| `GET /oauth/authorize` | Browser | Authorization endpoint. |
| `POST /oauth/authorize` | Browser | Submits the consent form. Not called by Clients. |
| `POST /oauth/token` | Client | Token endpoint: `authorization_code` and `refresh_token`. |
| `POST /oauth/revoke` | Client | Revocation ([RFC 7009](https://www.rfc-editor.org/rfc/rfc7009)). |
| `POST /oauth/introspect` | Resource server | Introspection ([RFC 7662](https://www.rfc-editor.org/rfc/rfc7662)). |
| `GET/POST /account/login` | Browser | Sign-in page. |
| `GET/POST /account/logout` | Browser | Sign-out page. Ends the Session at Authora. |

## Common rules

- `/oauth/token`, `/oauth/revoke` and `/oauth/introspect` take `application/x-www-form-urlencoded` bodies.
- Errors are JSON: `{"error": "…", "error_description": "…"}`. A failed Client authentication returns `401` with `WWW-Authenticate: Basic`; all other errors return `400`.
- Responses carry `Cache-Control: no-store`.
- **CORS:** `/oauth/token` and `/oauth/revoke` allow the origins (scheme, host and port) of the Redirect URIs of enabled Clients, method `POST`, the header `Content-Type`, and no credentials. `/oauth/introspect`, `/oauth/authorize` and `/account/*` send no CORS headers.

## Client authentication

| Client type | Authentication at `/oauth/token`, `/oauth/revoke`, `/oauth/introspect` |
|---|---|
| Confidential | `client_secret_basic` (HTTP Basic) or `client_secret_post` (`client_id` and `client_secret` in the body). One active, unexpired Secret must match. Sending both methods is rejected. |
| Public | `client_id` in the body, no Secret. |

A disabled Client cannot obtain tokens, but can still revoke its tokens.

## Authorization request

`GET /oauth/authorize`

| Parameter | Rule |
|---|---|
| `client_id` | Required. Unknown or disabled Clients are rejected on an error page. |
| `redirect_uri` | Must exactly match a registered Redirect URI. May be omitted only if exactly one is registered. A wrong value is shown as an error and never redirected to. |
| `response_type` | Required, must be `code`. |
| `code_challenge` | Required. Base64url-encoded SHA-256 hash of the code verifier. |
| `code_challenge_method` | Required, must be `S256`. `plain` is rejected. |
| `scope` | Required. Space-separated; every Scope must be allowed for the Client. |
| `state` | Optional. Returned unchanged. |
| `response_mode` | Optional. Only `query`. |
| `nonce`, `prompt`, `max_age` | OpenID Connect parameters, see [OpenID Connect](openid-connect.md#authorization-request). |

- Unknown parameters are ignored; parameters without a value count as omitted; a parameter sent twice is rejected.
- Until `client_id` and `redirect_uri` are valid, errors are shown to the User. After that, they are sent to the Redirect URI with `error`, `error_description`, `state` and `iss`.
- The request is validated before the User signs in, and again when the consent form is submitted.

### Consent

| Client | Behavior |
|---|---|
| Third-party (default) | The User is asked on every authorization request. The consent screen shows the Client Name, the Display Name and Description of each Scope, the signed-in User, and where the browser will be sent. Decisions are not remembered. Denying sends `error=access_denied`. |
| First-party | Not asked: a signed-in User gets the code right away, also with `prompt=none`. `prompt=consent` still shows the consent screen. |

Marking a Client as first-party needs the Permission `clients.manage-first-party`. While a Client is first-party, no Redirect URI and no Scope can be added to it (`409 Conflict`). See [Security model](../explanation/security-model.md#first-party-clients).

### Response

A `303 See Other` redirect to the Redirect URI with `code`, `state` (if sent) and `iss` ([RFC 9207](https://www.rfc-editor.org/rfc/rfc9207)).

Error codes: `invalid_request`, `unauthorized_client`, `access_denied`, `unsupported_response_type`, `invalid_scope`, and the OpenID Connect codes `login_required`, `consent_required`, `request_not_supported`, `request_uri_not_supported`, `registration_not_supported`.

## Token request

`POST /oauth/token`

### `grant_type=authorization_code`

| Parameter | Rule |
|---|---|
| `code` | Required. Single use; valid for `Authora:OAuth:AuthorizationCodeLifetime` (1 minute). |
| `code_verifier` | Required. Must match the `code_challenge`. |
| `redirect_uri` | Required if it was sent in the authorization request, and then identical. |
| Client authentication | See above. Must be the Client the code was issued to. |

### `grant_type=refresh_token`

| Parameter | Rule |
|---|---|
| `refresh_token` | Required. Single use: the response contains a new Refresh Token. |
| `scope` | Optional. Narrows the Scopes of the new Access Token; can never widen them. |

The Client must be allowed the Grant Type `refresh_token`.

### Response

```json
{
  "access_token": "…",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "…",
  "scope": "openid orders.read",
  "id_token": "…"
}
```

- `refresh_token` only if the Client is allowed the Grant Type `refresh_token`.
- `id_token` only when exchanging a code whose Scopes include `openid`, and `openid` is still allowed for the Client. Refreshing returns no ID Token.
- Scopes an administrator disallowed for the Client since the code was issued are dropped.

Error codes: `invalid_request`, `invalid_client`, `invalid_grant`, `unauthorized_client`, `unsupported_grant_type`, `invalid_scope`.

## Revocation

`POST /oauth/revoke` with `token` and optionally `token_type_hint` (`access_token` or `refresh_token`), plus Client authentication.

- Revoking a **Refresh Token** revokes its whole Grant: all Access and Refresh Tokens issued from it.
- Revoking an **Access Token** revokes only that token.
- A Client can only revoke its own tokens. Unknown tokens and tokens of other Clients are answered with `200` and left unchanged, so the endpoint does not reveal whether a token exists.

## Introspection

`POST /oauth/introspect` with `token` and optionally `token_type_hint`, authenticated as an enabled **Confidential** Client.

| Token | Reported active to |
|---|---|
| Access Token | The Client it was issued to, with all its Scopes; and any Client that serves at least one of its Scopes, with only the served Scopes in `scope`. |
| Refresh Token | Only the Client it was issued to. |

Active response members: `active`, `scope`, `client_id`, `sub` (the User Id), `token_type`, `exp`, `iat`, `iss`. Every other case returns only `{"active": false}`.

A token is active only if:

1. it exists, is not expired and not revoked;
2. its Grant is neither revoked nor expired;
3. its Client exists and is enabled;
4. its User can sign in (active, has a password) and still has the security stamp the Grant was created with.

## Sign-in

`/account/login` accepts an Email and a password.

- Every failure shows the same message. Unknown Emails still verify a password, so the response time does not reveal whether an account exists.
- After `Authora:SignIn:MaxFailedAttempts` (5) failures in a row, the User is locked out for `Authora:SignIn:LockoutDuration` (15 minutes).
- Sign-in attempts are rate-limited per IP address (10 per minute).
- Every sign-in starts a new Session and ends the previous Session of the browser.

`/account/logout` revokes the Session on the server and deletes the cookie.

## Events that invalidate tokens

| Event | Effect |
|---|---|
| A token is revoked at `/oauth/revoke` | That Access Token, or the whole Grant for a Refresh Token |
| An Authorization Code or Refresh Token is used a second time | The whole Grant |
| The Grant reaches `Authora:OAuth:GrantLifetime` | The whole Grant |
| A Client is disabled | All Grants of the Client. Enabling it again does not restore them. |
| A Client is deleted | All codes and Grants of the Client |
| A User is deactivated or deleted, gets a new password, or is signed out everywhere | All Sessions and tokens of the User |
