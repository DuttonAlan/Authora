# OAuth endpoints

Authora is an OAuth 2.1 authorization server. This page lists its endpoints and the exact rules for their parameters. OpenID Connect additions (discovery, ID Tokens, UserInfo) are in [OpenID Connect](openid-connect.md).

| Endpoint | Called by | Purpose |
|---|---|---|
| `GET /oauth/authorize` | Browser | Authorization endpoint. |
| `POST /oauth/authorize` | Browser | Submits the consent form. A POST without the consent form's fields is an authorization request sent as a form, see below. |
| `POST /oauth/token` | Client | Token endpoint: `authorization_code` and `refresh_token`. |
| `POST /oauth/revoke` | Client | Revocation ([RFC 7009](https://www.rfc-editor.org/rfc/rfc7009)). |
| `POST /oauth/introspect` | Resource server | Introspection ([RFC 7662](https://www.rfc-editor.org/rfc/rfc7662)). |
| `GET/POST /account/login` | Browser | Sign-in page. |
| `GET/POST /account/second-factor` | Browser | Second step of the sign-in for Users with a second factor, and of a step-up. See [Sign-in](#sign-in). |
| `GET /account/step-up` | Browser | A signed-in User completes an additional factor for the current Session. See [Sign-in](#sign-in). |
| `GET /account/security` | Browser | *Account and security*: overview of a signed-in User, with how they signed in and links to the password change, the connected applications and the second factors. |
| `GET/POST /account/logout` | Browser | Sign-out page. Ends the Session at Authora. |
| `GET/POST /account/password` | Browser | A signed-in User changes their own password. See [Password change](#password-change). |
| `GET/POST /account/forgot-password` | Browser | A User who forgot their password asks for a reset link by email. See [Password reset](#password-reset). |
| `GET/POST /account/reset-password` | Browser | The page of a reset link, where the User chooses a new password. See [Password reset](#password-reset). |
| `GET/POST /account/consents` | Browser | *Connected applications*: a signed-in User sees and withdraws their Consents. See [Consent](#consent). |
| `GET/POST /oauth/logout` | Browser | End-session endpoint: a Client signs the User out (RP-initiated logout). See [Logout](openid-connect.md#logout). |

## Common rules

- `/oauth/token`, `/oauth/revoke` and `/oauth/introspect` take `application/x-www-form-urlencoded` bodies.
- Errors are JSON: `{"error": "…", "error_description": "…"}`. A failed Client authentication returns `401` with `WWW-Authenticate: Basic`; all other errors return `400`.
- Responses carry `Cache-Control: no-store`.
- **Rate limit:** `/oauth/token`, `/oauth/revoke` and `/oauth/introspect` share one limit per IP address (`Authora:RateLimiting:TokenPermitsPerMinute`, 300 per minute); requests over it get `429 Too Many Requests`. A resource server that introspects tokens should cache the result for a short time.
- **CORS:** `/oauth/token` and `/oauth/revoke` allow the origins (scheme, host and port) of the Redirect URIs of enabled Clients, method `POST`, the header `Content-Type`, and no credentials. `/oauth/introspect`, `/oauth/authorize`, `/oauth/logout` and `/account/*` send no CORS headers.

## Client authentication

| Client type | Authentication at `/oauth/token`, `/oauth/revoke`, `/oauth/introspect` |
|---|---|
| Confidential | `client_secret_basic` (HTTP Basic) or `client_secret_post` (`client_id` and `client_secret` in the body). One active, unexpired Secret must match. Sending both methods is rejected. |
| Public | `client_id` in the body, no Secret. |

A disabled Client cannot obtain tokens, but can still revoke its tokens.

## Authorization request

`GET /oauth/authorize`, or `POST /oauth/authorize` with the parameters as a form (OpenID Connect Core 1.0, Section 3.1.2.1). A POST is answered with a `303 See Other` redirect to the same request as GET: the browser leaves the Session cookie out of a cross-site POST, but sends it with the GET. The parameters then appear in the URL, like those of a GET request.

| Parameter | Rule |
|---|---|
| `client_id` | Required. Unknown or disabled Clients are rejected on an error page. |
| `redirect_uri` | Must exactly match a registered Redirect URI. May be omitted only if exactly one is registered. A wrong value is shown as an error and never redirected to. |
| `response_type` | Required, must be `code`. |
| `code_challenge` | Required, unless PKCE is optional for the Client (see [PKCE](#pkce)). Base64url-encoded SHA-256 hash of the code verifier. |
| `code_challenge_method` | Required with `code_challenge`, must be `S256`. `plain` is rejected. |
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
| Third-party (default) | The User is asked the first time. The consent screen shows the Client Name, the Display Name and Description of each Scope, the signed-in User, and where the browser will be sent. *Allow* is remembered as the User's Consent for the Client; later requests for Scopes it contains get the code right away, also with `prompt=none`. A request with a Scope the Consent does not contain shows the screen again, and approving adds the new Scopes. Denying sends `error=access_denied` and changes nothing. |
| First-party | Not asked: a signed-in User gets the code right away, also with `prompt=none`. No Consent is stored, also not when the User approves after `prompt=consent`. |

`prompt=consent` always shows the consent screen. A Consent lasts until it is revoked: by the User at `/account/consents` (*Connected applications*) or by an administrator (`DELETE /api/users/{id}/consents/{clientId}`). Revoking it also revokes the Grants of the User for the Client, so its Access and Refresh Tokens for the User stop working, and an Authorization Code issued before is rejected at the token endpoint (`invalid_grant`), even if the User approves the Client again in the meantime: each code is bound to the Consent it was issued under. Deleting a Scope removes it from every Consent; deleting the User or the Client deletes the Consents.

Marking a Client as first-party needs the Permission `clients.manage-first-party`. While a Client is first-party, no Redirect URI, Post-logout Redirect URI or Scope can be added to it, PKCE cannot be made optional and `client_credentials` cannot be newly allowed (`409 Conflict`). See [Security model](../explanation/security-model.md#first-party-clients).

### PKCE

Every Client must send PKCE by default. An administrator can make it optional for a Confidential Client (`PUT /api/clients/{id}/pkce-optional`, Permission `clients.update`), for OpenID Connect libraries that do not send it. Such a Client must protect its codes with the `nonce` instead (OAuth 2.1, Section 7.5.1), which Authora does not enforce. Public Clients always need PKCE, and it cannot be made optional while a Client is first-party (`409 Conflict`).

| Authorization request of a Client with optional PKCE | Token request |
|---|---|
| With `code_challenge` | Checked as for any other Client: `code_verifier` required and must match. |
| Without `code_challenge` | `code_verifier` must not be sent (`invalid_grant`), so a code obtained without PKCE cannot be injected into a flow that uses it. If PKCE was required again in the meantime, the code is rejected (`invalid_grant`). |

### Response

A `303 See Other` redirect to the Redirect URI with `code`, `state` (if sent) and `iss` ([RFC 9207](https://www.rfc-editor.org/rfc/rfc9207)).

Error codes: `invalid_request`, `unauthorized_client`, `access_denied`, `unsupported_response_type`, `invalid_scope`, and the OpenID Connect codes `login_required`, `consent_required`, `request_not_supported`, `request_uri_not_supported`, `registration_not_supported`.

## Token request

`POST /oauth/token`

### `grant_type=authorization_code`

| Parameter | Rule |
|---|---|
| `code` | Required. Single use; valid for `Authora:OAuth:AuthorizationCodeLifetime` (1 minute), and only while the Session in which the User approved the request is active: after the User signs out, the code gets `invalid_grant`. |
| `code_verifier` | Required if the authorization request had a `code_challenge`, and must match it. Otherwise must not be sent, see [PKCE](#pkce). |
| `redirect_uri` | Required if it was sent in the authorization request, and then identical. |
| Client authentication | See above. Must be the Client the code was issued to. |

### `grant_type=refresh_token`

| Parameter | Rule |
|---|---|
| `refresh_token` | Required. Single use: the response contains a new Refresh Token. |
| `scope` | Optional. Narrows the Scopes of the new Access Token; can never widen them. |

The Client must be allowed the Grant Type `refresh_token`.

### `grant_type=client_credentials`

A **Confidential** Client obtains an Access Token for itself, without a User ([RFC 6749, Section 4.4](https://www.rfc-editor.org/rfc/rfc6749#section-4.4)), e.g. a backend service that calls an API.

| Parameter | Rule |
|---|---|
| `scope` | Optional. Every Scope must be allowed for the Client and must not be a System Scope (`openid`, `profile`, `email`, `authora.admin`), because they all need a User. Without `scope`, the token gets all Scopes the Client is allowed, except System Scopes; if their names are longer than 4000 characters together, the request fails with `invalid_scope`. |
| Client authentication | Required, with a Secret (`client_secret_basic` or `client_secret_post`). |

- The Client must be allowed the Grant Type `client_credentials`. It is never allowed by default, only Confidential Clients can be allowed it, and not while they are first-party.
- The response has no `refresh_token` and no `id_token`: the Client asks for a new token when the old one expires. The token lives for the Client's Access Token lifetime.
- Every issued token is recorded as the audit event `client.token-issued`.
- Introspection reports the Client as `sub`. UserInfo (`401 invalid_token`) and the admin API (`401`) reject the token.

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

- `refresh_token` only if the Client is allowed the Grant Type `refresh_token`, and never for `client_credentials`.
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

Active response members: `active`, `scope`, `client_id`, `sub` (the User Id, or the Client Id for a token the Client obtained for itself with `client_credentials`, then equal to `client_id`), `token_type`, `exp`, `iat`, `iss`. Every other case returns only `{"active": false}`.

A token is active only if:

1. it exists, is not expired and not revoked;
2. its Grant is neither revoked nor expired;
3. its Client exists and is enabled;
4. its User, if it has one, can sign in (active, has a password) and still has the security stamp the Grant was created with.

## Sign-in

`/account/login` accepts an Email and a password.

- Every failure shows the same message. Unknown Emails still verify a password, so the response time does not reveal whether an account exists.
- After `Authora:SignIn:MaxFailedAttempts` (5) failures in a row, the User is locked out for `Authora:SignIn:LockoutDuration` (15 minutes).
- Sign-in attempts are rate-limited per IP address (10 per minute).
- Every sign-in starts a new Session and ends the previous Session of the browser. The Session records its authentication methods (RFC 8176, `pwd` for the password) and when the User last authenticated, which every Authorization Code issued in it carries on (`auth_time` in the ID Token).

A User who has enrolled a second factor gets no Session for the password alone. The correct password starts a pending sign-in in the cookie `__Host-authora_pending_sign_in` and redirects to `/account/second-factor`, with the same `returnUrl`:

- The pending sign-in lasts `Authora:SignIn:PendingSignInLifetime` (5 minutes), can be completed only once, and only while the User's security stamp is unchanged. Otherwise the page asks the User to sign in again.
- A wrong second factor counts towards the same lockout as a wrong password; the failed attempts are only reset once the sign-in is complete. Submissions share the sign-in rate limit.
- Once the factor is correct, the Session starts with both methods, e.g. `pwd otp`, and the browser returns to `returnUrl`.
- A password reset by email never signs in and never skips the second factor.

`/account/step-up` starts the same second step for a signed-in User whose Session lacks one of their enrolled factors, and returns to a local `returnUrl` afterwards. Completing it adds the method to the Session, sets its authentication time to now and gives it a new Session token; the old token stops working. Without a missing factor, the page says there is nothing to confirm.

Account changes that need a recent authentication (enrolling or removing a factor) send a User who authenticated longer than `Authora:SignIn:RecentAuthenticationWindow` (10 minutes) ago to the sign-in page and back.

No second factor can be enrolled yet, so all Users currently sign in with the password alone.

`/account/logout` revokes the Session on the server and deletes the cookie. Applications sign Users out through the end-session endpoint `/oauth/logout` instead, see [Logout](openid-connect.md#logout).

## Password change

`/account/password` lets a signed-in User change their own password. Without a Session, it redirects to the sign-in page and back. An optional local `returnUrl` is offered as a link afterwards (the admin interface uses it).

- The User enters the current password and the new one twice. The new password follows the same policy as the admin API: 12 to 128 characters.
- A wrong current password counts as a failed sign-in: after `Authora:SignIn:MaxFailedAttempts` (5) in a row, the User is locked out for `Authora:SignIn:LockoutDuration`, here and at sign-in.
- Attempts share the sign-in rate limit per IP address (`Authora:RateLimiting:SignInPermitsPerMinute`).
- The new password rotates the User's security stamp: every other Session and every Grant of the User, with its Access and Refresh Tokens, ends at once. The Session the password was changed in stays valid.
- If email delivery is configured, the User gets an email about the change. A failed delivery does not undo the change (log event 5011).
- Recorded as audit event `account.password-changed`.

## Password reset

A User who forgot their password follows **Forgot password?** on the sign-in page to `/account/forgot-password` and enters their Email. The link in the email opens `/account/reset-password?token=…`, where they choose a new password. Administrators can send the same link with `POST /api/users/{id}/password-reset`, see [Admin API](admin-api.md#users). Settings: [Password reset](configuration.md#password-reset).

- Offered only with email delivery configured and `Authora:PasswordReset:SelfServiceEnabled` (default `true`); otherwise **Forgot password?** is hidden and `/account/forgot-password` returns `404`.
- Every address gets the same answer, whether or not an account has it. The page only queues the request; the address is looked up and the email sent in the background, so neither the page nor the response time reveals whether an account exists. Deactivated Users get no link. While the queue is full (1,000 requests), requests are dropped without counting towards the limit per address (log event 14024).
- At most `Authora:PasswordReset:MaxEmailsPerHour` (3) links per address and hour. Submissions of both pages share a rate limit per IP address (`Authora:RateLimiting:PasswordResetPermitsPerMinute`, 5).
- The link works for `Authora:PasswordReset:LinkLifetime` (30 minutes) and only once. A newer link replaces older ones, and a link stops working if the User's Email or security stamp changes (password changed or set by an administrator, deactivation, "sign out everywhere"). Opening the link does not use it up, so mail scanners that follow links do not break it; setting the password does. An invalid new password does not use it up either.
- The new password follows the usual policy (12 to 128 characters). It rotates the User's security stamp: every Session and every Grant of the User, with its Access and Refresh Tokens, ends. A lockout ends too. The User then signs in with the new password.
- The User gets an email that the password was reset.
- Recorded as audit events `account.password-reset-requested` and `account.password-reset` (and `user.password-reset-sent` for links sent by an administrator).

## Email verification

A User confirms that they receive email at their address with a link to `/account/verify-email?token=…`. Once confirmed, `email_verified` is `true` in ID Tokens and at UserInfo, see [OpenID Connect](openid-connect.md#scopes-and-claims). Settings: [Email verification](configuration.md#email-verification).

- With email delivery configured, the link is sent in the background when an administrator creates a User and when they change a User's Email to a different address, unless `Authora:EmailVerification:SendAutomatically` is `false` or the User is deactivated. A delivery failure does not fail the change; it is only logged (log event 14004). Administrators can send another link with `POST /api/users/{id}/email-verification`, or mark the address as verified without a link with `POST /api/users/{id}/email-verified`, see [Admin API](admin-api.md#users).
- Changing the Email to a different address makes it unverified again; a change of upper and lower case only keeps the verification. Deleting a User removes it.
- Opening the link only shows a **Confirm email address** button, so mail scanners that follow links do not use it up; submitting it does. No sign-in is needed: the link itself proves that whoever opens it receives the email.
- The link works for `Authora:EmailVerification:LinkLifetime` (24 hours) and only once. A newer link replaces older ones, and a link stops working if the User's Email or security stamp changes (password changed or set by an administrator, deactivation, "sign out everywhere"), the User is deactivated, or an administrator marks the address as verified.
- Submissions share the rate limit of the password reset pages (`Authora:RateLimiting:PasswordResetPermitsPerMinute`, 5 per IP address).
- Recorded as audit events `user.email-verification-sent` and `account.email-verified` (and `user.email-marked-verified` when an administrator marks the address).

## Events that invalidate tokens

| Event | Effect |
|---|---|
| A token is revoked at `/oauth/revoke` | That Access Token, or the whole Grant for a Refresh Token |
| An Authorization Code or Refresh Token is used a second time | The whole Grant |
| The Grant reaches `Authora:OAuth:GrantLifetime` | The whole Grant |
| A Client is disabled | All Grants of the Client. Enabling it again does not restore them. |
| The User or an administrator revokes the User's Consent for a Client | All Grants of the User for that Client, and codes issued for it before |
| A Client is deleted | All codes and Grants of the Client |
| A User is deactivated or deleted, gets a new password, or is signed out everywhere | All Sessions and tokens of the User |
