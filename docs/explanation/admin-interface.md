# The admin interface

The admin interface at `/admin/` lets administrators manage Users, Groups, Roles, Clients and Scopes and read the audit log in the browser. This page explains how it signs in and why it is built the way it is.

## An application like any other

The admin interface has no special access. It is registered as a Client, `Authora Admin UI`, which Authora creates at startup, and it signs in through the same Authorization Code flow with PKCE as every other application. The admin API then checks its Access Token exactly like any other Client's.

This means the admin API has a single way in. There is no separate admin login and no session-based shortcut that would need its own protection.

## Tokens never reach the browser

Single-page applications often keep their tokens in JavaScript. Then any script that runs on the page, e.g. through a cross-site scripting bug or a compromised dependency, can read the tokens and use them from anywhere, long after the page is closed.

The admin interface avoids this with a small **backend for frontend** inside Authora:

```mermaid
sequenceDiagram
    participant B as Browser (admin interface)
    participant A as Authora
    B->>A: GET /api/me (X-Authora-Admin: 1)
    A-->>B: 401, no cookie yet
    B->>A: GET /admin/signin
    A-->>B: Redirect to /oauth/authorize (state and PKCE kept on the server)
    Note over B,A: Sign-in page if needed; no consent screen (first-party)
    A-->>B: Redirect to /admin/callback?code=…
    Note over A: Checks state and iss, exchanges the code on the server
    A-->>B: Cookie __Host-authora_admin (tokens, encrypted, HttpOnly)
    B->>A: /api/… with the cookie and X-Authora-Admin: 1
    Note over A: The cookie becomes "Authorization: Bearer …"
```

- The server runs the flow, keeps `state` and the PKCE verifier, and exchanges the code itself.
- The tokens live only in an encrypted, `HttpOnly` cookie. JavaScript cannot read it.
- For each `/api/*` request, Authora turns the cookie into a Bearer token before authentication, so the admin API code is unchanged.

A malicious script could still act as the administrator while the page is open, but it cannot take the tokens away. The strict Content Security Policy is what keeps such scripts out in the first place.

## Protection against cross-site requests

A cookie that grants API access invites cross-site request forgery. The admin API therefore accepts the cookie only with the header `X-Authora-Admin: 1`. Browsers cannot send a custom header to another origin without a CORS preflight, and `/api/*` never allows one. The cookie is also `SameSite=Strict`, and requests whose `Sec-Fetch-Site` header says they come from another site are ignored.

## Bound to the Session

The admin interface's tokens are tied to the Authora Session the administrator signed in with. A refresh only works while that Session is valid. So the admin interface ends when the Session ends: after the idle timeout or the absolute lifetime, on signing out of Authora in another tab, or when the User is signed out everywhere or deactivated.

## Refreshing safely across tabs

All tabs share one cookie, and a Refresh Token used twice revokes the whole Grant. The interface therefore refreshes in one tab at a time (using the Web Locks API), and the server skips a refresh if another tab did one in the last ten seconds.

## Why no consent screen

The admin interface is a *first-party* Client, so administrators are not asked for consent on every sign-in. See [Security model](security-model.md#first-party-clients) for why that is safe and what protects it.

## Hiding what you may not do

`GET /api/me` returns the User's Permissions, and the interface hides buttons for actions the User may not perform. This is only for convenience. The API checks every Permission on every request, whatever the interface shows.

## No build step

The interface is plain HTML, CSS and JavaScript modules in `src/Authora.Api/wwwroot/admin`, with no framework and no build step. All content is inserted as text, never as HTML, so names from the API cannot inject markup. The small code base and the absence of third-party scripts keep the attack surface small.

## Turning it off

Set `Authora:AdminUi:Enabled` to `false` to serve `404` for `/admin/*`. The admin API keeps working for other Clients allowed `authora.admin`.
