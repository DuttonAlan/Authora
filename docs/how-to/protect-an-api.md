# Protect an API with introspection

This guide shows how an API (a *resource server*) accepts Access Tokens issued by Authora. Authora's Access Tokens are opaque: they carry no readable content, so the API asks Authora about each token through the introspection endpoint ([RFC 7662](https://www.rfc-editor.org/rfc/rfc7662)).

The example protects an orders API that applications may read with the Scope `orders.read`.

## 1. Create a Scope for the API

In the admin interface, open **Scopes** → **New Scope**:

| Field | Value |
|---|---|
| Name | `orders.read` (what applications request) |
| Display name | `Read your orders` (shown on the consent screen) |
| Description | optional, also shown on the consent screen |

Write the display name for users: it is what they approve.

## 2. Register the API as a Confidential Client

The API needs credentials to call the introspection endpoint. Open **Clients** → **Register Client**:

- **Name:** `Orders API`
- **Type:** `Confidential`
- No Redirect URIs and no allowed Scopes: the API never signs users in.

Copy the **Client Secret** shown after registration; it is shown only once. Copy the **Client Id** too.

## 3. Let the API serve the Scope

Open the `Orders API` Client. In the **Scopes** panel, check **Served** for `orders.read`.

An API only learns about tokens that are meant for it and contain a Scope it serves; it sees only those Scopes. Without this, every token looks inactive to it. A token requested without `resource` (see the next step) is meant for every API that serves one of its Scopes *when the token is issued*: tokens issued before the API started serving the Scope stay invisible to it.

## 4. Give the API a Resource Identifier

In the **API** panel of the `Orders API` Client, set the **Resource Identifier**, e.g. `https://orders.example.com`, and save. It is an absolute HTTPS URI without query or fragment, usually the API's base URL, and unique among all Clients. Applications name it with the `resource` parameter ([RFC 8707](https://www.rfc-editor.org/rfc/rfc8707)) to get a token that is meant for this API only: other APIs that serve the same Scopes see it as inactive, and it keeps only the Scopes this API serves.

With **Require the resource parameter**, the API no longer sees tokens requested without `resource`, only those that name it. Turn it on once all applications send `resource`.

This step is optional: without a Resource Identifier, the API sees every token requested without `resource` that has one of its Scopes.

## 5. Allow the Scope for the applications

Open each Client that calls the API and check **Allowed** for `orders.read` in its **Scopes** panel. The applications then request it, e.g. `scope=openid orders.read`, and name the API:

```text
https://auth.example.com/oauth/authorize?client_id=…&response_type=code&scope=openid%20orders.read
  &resource=https%3A%2F%2Forders.example.com&resource=https%3A%2F%2Fauth.example.com%2Fapi&…
```

`resource` may repeat. Every requested Scope other than `openid`, `profile`, `email` and `authora.admin` must be served by one of the named APIs, and every named API must serve one of the Scopes, otherwise Authora answers `invalid_target`. `https://auth.example.com/api` (the Issuer followed by `/api`) is Authora itself: name it too if the application calls UserInfo with the token. Without it, `openid` is left out of the Access Token (the ID Token is still issued) and UserInfo rejects it.

At the token endpoint, `resource` can narrow the audience to some of the approved APIs, e.g. one token per API:

```sh
curl https://auth.example.com/oauth/token \
  -d grant_type=refresh_token \
  -d client_id="$APP_CLIENT_ID" \
  -d refresh_token="$REFRESH_TOKEN" \
  -d resource=https://orders.example.com
```

The response's `scope` shows the Scopes the token keeps.

## 6. Check tokens in the API

For each request, take the Bearer token and introspect it with the API's credentials:

```sh
curl https://auth.example.com/oauth/introspect \
  -u "$ORDERS_API_CLIENT_ID:$ORDERS_API_SECRET" \
  -d token="$ACCESS_TOKEN"
```

An active token:

```json
{
  "active": true,
  "scope": "orders.read",
  "client_id": "01a1542d-905a-7baf-82b2-e2db33513106",
  "sub": "01a1542c-e0b2-7896-b701-241c6b7e3b7b",
  "token_type": "Bearer",
  "exp": 1792417234,
  "iat": 1792413634,
  "iss": "https://auth.example.com",
  "aud": ["https://orders.example.com"]
}
```

Anything else returns only `{"active": false}`, without a reason. `aud` lists the APIs the token is meant for (an API without Resource Identifier appears by its Client Id).

Then, in the API:

1. Reject the request with `401` if `active` is `false`.
2. Reject it with `403` if `scope` does not contain the Scope the endpoint needs.
3. Use `sub` (the User Id) to decide what this user may see; `client_id` tells you which application calls. If `sub` equals `client_id`, no user is involved: a service called with a token it obtained for itself (see [Call the API from a backend service](#call-the-api-from-a-backend-service)). Do not treat such a token as any user's.

`scope` only contains the Scopes your API serves, even if the token has more.

Many frameworks have a ready-made OAuth 2.0 introspection handler; configure it with the introspection endpoint, the Client Id and the Secret.

## Call the API from a backend service

A service without a user, e.g. a nightly job that reads orders, gets a token for itself with the Client Credentials Grant (`grant_type=client_credentials`).

1. Register the service as a Client: **Clients** → **Register Client**, Type `Confidential`, no Redirect URIs. Under **Grant Types**, check only **Client Credentials**. Copy the Client Id and the Secret.
2. In its **Scopes** panel, check **Allowed** for `orders.read`. Only Scopes you allow can be requested; System Scopes such as `openid` never can, because they need a user.
3. Request a token with the service's credentials:

   ```sh
   curl https://auth.example.com/oauth/token \
     -u "$REPORTS_JOB_CLIENT_ID:$REPORTS_JOB_SECRET" \
     -d grant_type=client_credentials \
     -d scope=orders.read
   ```

   ```json
   {
     "access_token": "…",
     "token_type": "Bearer",
     "expires_in": 3600,
     "scope": "orders.read"
   }
   ```

   There is no Refresh Token: request a new token when this one expires. Without `scope`, the token gets all Scopes the service is allowed. Add `-d resource=https://orders.example.com` to get a token meant only for the orders API; without `scope`, it then gets all allowed Scopes that API serves.

4. Call the API with `Authorization: Bearer <access_token>`. Introspection reports the service's Client Id as both `client_id` and `sub`.

Each issued token is recorded in the audit log as `client.token-issued`. Disabling the Client invalidates its tokens immediately.

## Caching

Each introspection is a request to Authora. You may cache the result for a short time, keyed by a hash of the token, but every second of caching is a second in which a revoked token still works. Keep it short (seconds, not minutes), and never cache longer than `exp`. Introspection shares the rate limit of the token endpoint per IP address (`Authora:RateLimiting:TokenPermitsPerMinute`, 300 per minute by default); over it, Authora answers `429 Too Many Requests`. A busy API needs a cache or a higher limit, see [Configuration](../reference/configuration.md#rate-limits).

## Rotate the API's Secret

Secrets expire after one year by default. Before that, open the `Orders API` Client → **Secrets** → **Create Secret**, deploy the new Secret, then **Revoke** the old one. A Client can have several active Secrets at once, so there is no downtime.

## Related

- [Introspection rules](../reference/oauth-endpoints.md#introspection)
- [Resources and audiences](../reference/oauth-endpoints.md#resources)
- [Tokens, Grants and Sessions](../explanation/tokens-and-sessions.md): why Access Tokens are opaque
