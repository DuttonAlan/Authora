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

An API only learns about tokens that contain a Scope it serves. Without this, every token looks inactive to it. This keeps one API from learning about tokens meant for another.

## 4. Allow the Scope for the applications

Open each Client that calls the API and check **Allowed** for `orders.read` in its **Scopes** panel. The applications then request it, e.g. `scope=openid orders.read`.

## 5. Check tokens in the API

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
  "iss": "https://auth.example.com"
}
```

Anything else returns only `{"active": false}`, without a reason.

Then, in the API:

1. Reject the request with `401` if `active` is `false`.
2. Reject it with `403` if `scope` does not contain the Scope the endpoint needs.
3. Use `sub` (the User Id) to decide what this user may see; `client_id` tells you which application calls.

`scope` only contains the Scopes your API serves, even if the token has more.

Many frameworks have a ready-made OAuth 2.0 introspection handler; configure it with the introspection endpoint, the Client Id and the Secret.

## Caching

Each introspection is a request to Authora. You may cache the result for a short time, keyed by a hash of the token, but every second of caching is a second in which a revoked token still works. Keep it short (seconds, not minutes), and never cache longer than `exp`.

## Rotate the API's Secret

Secrets expire after one year by default. Before that, open the `Orders API` Client → **Secrets** → **Create Secret**, deploy the new Secret, then **Revoke** the old one. A Client can have several active Secrets at once, so there is no downtime.

## Related

- [Introspection rules](../reference/oauth-endpoints.md#introspection)
- [Tokens, Grants and Sessions](../explanation/tokens-and-sessions.md): why Access Tokens are opaque
