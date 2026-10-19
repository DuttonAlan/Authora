# Call the admin API

Everything the admin interface does goes through the admin API under `/api/*`, so you can script it. This guide shows how to get a token for it and make a call.

## How access works

The admin API accepts Authora's own Access Tokens that contain the Scope `authora.admin`. Such a token always belongs to a User, and every call is checked against that User's Permissions, exactly like in the admin interface. There is no service account: a script acts as the User who signed in.

## 1. Register a Client for your scripts

In the admin interface, **Clients** → **Register Client**:

| Field | Value |
|---|---|
| Name | e.g. `Admin scripts` |
| Type | `Public` |
| Redirect URIs | `http://127.0.0.1:8080/callback` |
| Allowed Scopes | `authora.admin` |

Only allow `authora.admin` for Clients you control. Users still have to approve it on the consent screen (*Manage Authora*).

Alternatively, let Authora create such a Client at startup with `Authora__Bootstrap__Client__RedirectUris__0=http://127.0.0.1:8080/callback` (see [Bootstrap](../reference/configuration.md#bootstrap)). Its Client Id is logged as event 7005.

## 2. Get an Access Token

Run the Authorization Code flow with PKCE, with `scope=authora.admin`. A command-line tool or library that supports it works; by hand, follow steps 2 to 5 of [Your first application](../tutorials/first-application.md#2-create-a-pkce-pair), with your new Client Id and `scope=authora.admin`.

The token response contains an `access_token`, valid for one hour by default, and a `refresh_token` to get a new one.

## 3. Call the API

```sh
curl https://auth.example.com/api/users?search=jane \
  -H "Authorization: Bearer $ACCESS_TOKEN"
```

```json
{
  "items": [ { "id": "01a1…", "email": "jane@example.com", "firstName": "Jane", "lastName": "Doe", "state": "Active", … } ],
  "total": 1
}
```

Request bodies are JSON:

```sh
curl https://auth.example.com/api/groups \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "support", "displayName": "Support"}'
```

## Errors

Errors use [problem details](https://www.rfc-editor.org/rfc/rfc9457):

| Status | Meaning |
|---|---|
| `400` | Invalid input; `errors` lists the fields. |
| `401` | No token, or the token is expired or revoked. |
| `403` | The token has no `authora.admin`, or the User lacks the Permission. |
| `404` | The object does not exist. |
| `409` | The change conflicts with the current state, e.g. a name that already exists. |

## Browse the API

The full API, with every request and response, is described by OpenAPI. Swagger UI is only served in `Development`: run Authora from source ([Run Authora from source](run-from-source.md)) and open `https://localhost:7178/swagger`. The [admin API reference](../reference/admin-api.md) lists every endpoint and its Permission.
