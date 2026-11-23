# HTTP security headers and cookies

## Headers

| Header | Where | Purpose |
|---|---|---|
| `X-Content-Type-Options: nosniff` | All responses | Browsers do not run JSON as a script or page. |
| `X-Frame-Options: DENY`, CSP `frame-ancestors 'none'` | All responses | No clickjacking. |
| `Referrer-Policy: no-referrer` | All responses | Codes and Ids in URLs do not leak to other sites. |
| `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'; base-uri 'none'` | API responses | A response can never run as a page. |
| CSP `default-src 'none'; style-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'`, `Cache-Control: no-store` | Sign-in, sign-out, consent and account pages | Only Authora's own styles and no scripts; pages with a Session or a pending authorization are not cached. |
| CSP `default-src 'none'; script-src 'self'; connect-src 'self'; style-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'`, `Cache-Control: no-store` | Pages that use passkeys: `/account/second-factor` and `/account/security/passkeys` | The WebAuthn calls run in the static module `/js/passkeys.js`, which fetches the options from the page itself; no inline or foreign scripts. |
| CSP `default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'` | Admin interface | No inline or foreign scripts and styles. |
| `Cross-Origin-Opener-Policy: same-origin` | Admin interface | Other windows cannot reach it through `window.opener`. |
| `Cache-Control: no-store` | Token, revocation, introspection and UserInfo responses; admin session endpoints | Tokens are never cached. |
| `Cache-Control: no-cache` | Admin interface files | Browsers load the new version after an update. |
| `Strict-Transport-Security: max-age=31536000` | All responses outside `Development`, except for `localhost` | Browsers only use HTTPS. |

Plain HTTP requests are redirected to HTTPS when Kestrel knows the HTTPS port.

## Cookies

All cookies use the `__Host-` prefix: they are `Secure`, have the path `/` and no `Domain`, so subdomains cannot set or read them.

| Cookie | Attributes | Content |
|---|---|---|
| `__Host-authora_session` | `HttpOnly`, `SameSite=Lax`, no expiry | The Session token. The server enforces the idle timeout and the lifetime. |
| `__Host-authora_pending_sign_in` | `HttpOnly`, `SameSite=Strict`, 5 minutes (`Authora:SignIn:PendingSignInLifetime`) | The token of a pending sign-in between the password and the second factor, or of a step-up. Not a Session: the browser is not signed in with it. |
| `__Host-authora_antiforgery` | `HttpOnly`, `SameSite=Strict` | Antiforgery token of the sign-in, sign-out and consent forms. |
| `__Host-authora_admin` | `HttpOnly`, `SameSite=Strict`, no expiry | The admin interface's Access and Refresh Token, encrypted with Data Protection. |
| `__Host-authora_admin_signin` | `HttpOnly`, `SameSite=Lax`, 10 minutes | `state` and PKCE verifier of a running admin sign-in, encrypted. |

Because the cookies are `Secure` and the antiforgery system refuses to issue its cookie over plain HTTP, the sign-in only works over HTTPS.

## CORS

| Endpoint | Allowed origins | Methods | Headers |
|---|---|---|---|
| `/.well-known/openid-configuration`, `/.well-known/jwks.json` | Any | `GET` | – |
| `/oauth/token`, `/oauth/revoke` | Origins of the Redirect URIs of enabled Clients | `POST` | `Content-Type` |
| `/oauth/userinfo` | Origins of the Redirect URIs of enabled Clients | `GET`, `POST` | `Authorization` |
| everything else, including `/oauth/introspect`, `/oauth/authorize`, `/account/*`, `/api/*` | none | | |

No endpoint allows credentials (cookies) in cross-origin requests.
