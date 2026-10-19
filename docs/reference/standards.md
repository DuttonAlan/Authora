# Standards and limitations

## Supported

| Specification | Support |
|---|---|
| [OAuth 2.1](https://datatracker.ietf.org/doc/draft-ietf-oauth-v2-1/) | Authorization Code grant with PKCE, Refresh Token grant with rotation. No implicit or password grant. |
| [RFC 7636](https://www.rfc-editor.org/rfc/rfc7636) PKCE | Required for every Client; `S256` only. |
| [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) Token Revocation | Access and Refresh Tokens. |
| [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662) Token Introspection | For Confidential Clients that serve a Scope of the token. |
| [RFC 9207](https://www.rfc-editor.org/rfc/rfc9207) Authorization Server Issuer Identification | `iss` in every authorization response. |
| [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414) / [OpenID Connect Discovery 1.0](https://openid.net/specs/openid-connect-discovery-1_0.html) | `/.well-known/openid-configuration` |
| [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) | Authorization Code flow, ID Tokens (`RS256`), UserInfo, `nonce`, `prompt`, `max_age`, `auth_time`, `at_hash`, Scopes `openid`, `profile`, `email`. |
| [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517) JWK / [RFC 7638](https://www.rfc-editor.org/rfc/rfc7638) JWK Thumbprint | JWKS, `kid` as thumbprint. |
| [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) Problem Details | Errors of the admin API. |

Authora has not been certified by the OpenID Foundation.

## Not supported

Authora deliberately keeps a small feature set. Not supported today:

**Users**

- Self-registration, and changing or resetting one's own password. Administrators create Users and set passwords.
- Email verification; `email_verified` is always `false`.
- Multi-factor authentication and passkeys.
- Sign-in through external identity providers (e.g. Google).

**Protocols**

- Grant types other than `authorization_code` and `refresh_token`, e.g. `client_credentials` for machine-to-machine access or the device flow.
- RP-initiated logout, session management, front- and back-channel logout.
- Pushed authorization requests (PAR), JWT-secured authorization requests (JAR), the `request` and `request_uri` parameters.
- Response modes other than `query`, e.g. `form_post`.
- Dynamic Client registration.
- Pairwise subject identifiers.
- Audience-restricted Access Tokens ([RFC 8707](https://www.rfc-editor.org/rfc/rfc8707)); APIs are told apart by the Scopes they serve.
- JWT Access Tokens; Access Tokens are opaque and checked through introspection.
- Signing algorithms other than `RS256`.

**Access model**

- Roles, Groups and Permissions in tokens. They control access to Authora's own admin API only.
- Remembered consent: third-party Clients ask on every sign-in.

**Operations**

- Automatic key generation and scheduled rotation: keys are files the operator creates and replaces.

See the [roadmap in the README](../../README.md#roadmap) for what is planned.
