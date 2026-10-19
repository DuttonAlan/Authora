# Admin API

The admin API under `/api/*` manages Authora. The admin interface uses it, and scripts can too; see [Call the admin API](../how-to/call-the-admin-api.md).

- **Authentication:** an Access Token of Authora with the Scope `authora.admin`, as `Authorization: Bearer …`. ID Tokens and Session cookies are not accepted. The admin interface's own cookie is accepted only together with the header `X-Authora-Admin: 1` from the same origin.
- **Authorization:** every action needs the Permission in the tables below. Permissions come from the User's Roles, directly or through Groups, and are loaded on every request.
- **Format:** JSON in and out; enums as strings. Errors are [problem details](https://www.rfc-editor.org/rfc/rfc9457): `400` (validation), `401`, `403`, `404`, `409` (conflict with the current state).
- **Lists:** `GET /api/users` and `GET /api/audit-events` are paged: they take `page` (from 1) and `pageSize` and return `{"items": […], "total": n}`. The other lists return a plain JSON array.
- **No CORS:** the admin API cannot be called from other origins in a browser.
- **Schema:** the OpenAPI document and Swagger UI (`/swagger`) are served in `Development`.

## Current User

| Endpoint | Permission | Description |
|---|---|---|
| `GET /api/me` | – | The signed-in User and their Permissions. |

## Users

| Endpoint | Permission | Description |
|---|---|---|
| `GET /api/users` | `users.read` | List; filter by `state` and `search` (part of the Email, first or last name). `pageSize` 1 to 100, default 25. |
| `GET /api/users/{id}` | `users.read` | Details. |
| `POST /api/users` | `users.create` | Create, with `email`, `firstName`, `lastName` and an initial `password` (12 to 128 characters). |
| `PUT /api/users/{id}` | `users.update` | Change Email or name. |
| `DELETE /api/users/{id}` | `users.delete` | Delete. |
| `POST /api/users/{id}/deactivate` | `users.manage-access` | Block sign-in and end all Sessions and tokens. |
| `POST /api/users/{id}/activate` | `users.manage-access` | Allow sign-in again. |
| `POST /api/users/{id}/unlock` | `users.manage-access` | End a lockout after failed sign-ins. |
| `POST /api/users/{id}/revoke-sessions` | `users.manage-access` | Sign out everywhere: end all Sessions and tokens. |
| `PUT /api/users/{id}/password` | `users.set-password` | Set a new password; ends all Sessions and tokens. |
| `GET /api/users/{id}/roles` | `users.read` | Directly assigned Roles and Roles from Groups. |
| `PUT /api/users/{id}/roles/{roleId}` | `users.assign-roles` | Assign a Role. |
| `DELETE /api/users/{id}/roles/{roleId}` | `users.assign-roles` | Remove a Role. |
| `GET /api/users/{id}/permissions` | `users.read` | Effective Permissions and their source. |
| `GET /api/users/{id}/groups` | `users.read` | Groups the User is a member of. |

## Groups

| Endpoint | Permission | Description |
|---|---|---|
| `GET /api/groups` | `groups.read` | List. |
| `GET /api/groups/{id}` | `groups.read` | Details. |
| `POST /api/groups` | `groups.create` | Create, with `name` (`a-z`, `0-9`, `.`, `_`, `-`), `displayName`, `description`. |
| `PUT /api/groups/{id}` | `groups.update` | Change. |
| `DELETE /api/groups/{id}` | `groups.delete` | Delete. |
| `PUT /api/groups/{id}/roles/{roleId}` | `groups.assign-roles` | Assign a Role (not `admin`). |
| `DELETE /api/groups/{id}/roles/{roleId}` | `groups.assign-roles` | Remove a Role. |
| `GET /api/groups/{id}/members` | `groups.read` | Members. |
| `PUT /api/groups/{id}/members/{userId}` | `groups.manage-members` | Add a member. |
| `DELETE /api/groups/{id}/members/{userId}` | `groups.manage-members` | Remove a member. |

## Roles and Permissions

| Endpoint | Permission | Description |
|---|---|---|
| `GET /api/roles` | `roles.read` | List. |
| `GET /api/roles/{id}` | `roles.read` | Details with Permissions. |
| `POST /api/roles` | `roles.create` | Create, with `name` (`a-z`, `0-9`, `.`, `_`, `-`), `displayName`, `description`. |
| `PUT /api/roles/{id}` | `roles.update` | Change. |
| `DELETE /api/roles/{id}` | `roles.delete` | Delete (not System Roles). |
| `PUT /api/roles/{id}/permissions/{permissionId}` | `roles.grant-permissions` | Grant a Permission (not on `admin`). |
| `DELETE /api/roles/{id}/permissions/{permissionId}` | `roles.grant-permissions` | Revoke a Permission (not on `admin`). |
| `GET /api/permissions` | `permissions.read` | All Permissions. |

## Scopes

| Endpoint | Permission | Description |
|---|---|---|
| `GET /api/scopes` | `scopes.read` | List. |
| `GET /api/scopes/{id}` | `scopes.read` | Details. |
| `POST /api/scopes` | `scopes.create` | Create, with `name`, `displayName` (shown on the consent screen), `description`. |
| `PUT /api/scopes/{id}` | `scopes.update` | Change the display name or description. |
| `DELETE /api/scopes/{id}` | `scopes.delete` | Delete. `409` while Clients use it, unless `?force=true`, which removes it from them. System Scopes cannot be deleted. |

## Clients

| Endpoint | Permission | Description |
|---|---|---|
| `GET /api/clients` | `clients.read` | List. |
| `GET /api/clients/{id}` | `clients.read` | Details, including Redirect URIs, Scopes, Secrets (without values) and `isFirstParty`. |
| `POST /api/clients` | `clients.create` | Register, with `name`, `type` (`Public` or `Confidential`), and optionally `grantTypes`, `accessTokenTTL`, `refreshTokenTTL` (seconds), `redirectUris`, `scopeIds`. For a Confidential Client, the response contains its first Secret, once. |
| `PUT /api/clients/{id}` | `clients.update` | Change name, Grant Types or token lifetimes. |
| `DELETE /api/clients/{id}` | `clients.delete` | Delete, with all its codes and Grants. |
| `POST /api/clients/{id}/enable` | `clients.update` | Enable. |
| `POST /api/clients/{id}/disable` | `clients.update` | Disable; revokes all its Grants. |
| `PUT /api/clients/{id}/first-party` | `clients.manage-first-party` | Mark as first-party: no consent screen. |
| `DELETE /api/clients/{id}/first-party` | `clients.manage-first-party` | Mark as third-party again. |
| `POST /api/clients/{id}/redirect-uris` | `clients.update` | Add a Redirect URI (not while first-party). |
| `DELETE /api/clients/{id}/redirect-uris` | `clients.update` | Remove a Redirect URI. |
| `PUT /api/clients/{id}/scopes/{scopeId}` | `clients.update` | Allow a Scope (not while first-party). |
| `DELETE /api/clients/{id}/scopes/{scopeId}` | `clients.update` | Disallow a Scope. |
| `PUT /api/clients/{id}/served-scopes/{scopeId}` | `clients.update` | Let this Confidential Client introspect tokens with the Scope. |
| `DELETE /api/clients/{id}/served-scopes/{scopeId}` | `clients.update` | Stop serving the Scope. |
| `POST /api/clients/{id}/secrets` | `clients.manage-secrets` | Create a Secret, optionally with `expiresAt` (default 1 year, at most 2 years). The value is returned once. |
| `POST /api/clients/{id}/secrets/{secretId}/revoke` | `clients.manage-secrets` | Revoke a Secret. |

## Audit log and health

| Endpoint | Permission | Description |
|---|---|---|
| `GET /api/audit-events` | `audit.read` | Search the audit log, see [Audit events](audit-events.md#search). |
| `GET /api/audit-events/verification` | `audit.verify` | Verify the hash chain, see [Audit events](audit-events.md#verification). |
| `GET /health/details` | `health.read` | Full health report, see [Health checks](health-checks.md). |

## Permissions

All Permissions, as returned by `GET /api/permissions`. The System Role `admin` has all of them.

| Area | Permissions |
|---|---|
| Users | `users.read`, `users.create`, `users.update`, `users.delete`, `users.manage-access`, `users.set-password`, `users.assign-roles` |
| Groups | `groups.read`, `groups.create`, `groups.update`, `groups.delete`, `groups.manage-members`, `groups.assign-roles` |
| Roles | `roles.read`, `roles.create`, `roles.update`, `roles.delete`, `roles.grant-permissions` |
| Permissions | `permissions.read` |
| Scopes | `scopes.read`, `scopes.create`, `scopes.update`, `scopes.delete` |
| Clients | `clients.read`, `clients.create`, `clients.update`, `clients.delete`, `clients.manage-secrets`, `clients.manage-first-party` |
| Health | `health.read` |
| Audit | `audit.read`, `audit.verify` |
