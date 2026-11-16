# Standards and limitations

## Supported

| Specification | Support |
|---|---|
| [OAuth 2.1](https://datatracker.ietf.org/doc/draft-ietf-oauth-v2-1/) | Authorization Code grant with PKCE, Refresh Token grant with rotation. No implicit or password grant. |
| [RFC 7636](https://www.rfc-editor.org/rfc/rfc7636) PKCE | Required for every Client, unless an administrator makes it optional for a Confidential Client; `S256` only. |
| [RFC 7009](https://www.rfc-editor.org/rfc/rfc7009) Token Revocation | Access and Refresh Tokens. |
| [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662) Token Introspection | For Confidential Clients that serve a Scope of the token. |
| [RFC 9207](https://www.rfc-editor.org/rfc/rfc9207) Authorization Server Issuer Identification | `iss` in every authorization response. |
| [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414) / [OpenID Connect Discovery 1.0](https://openid.net/specs/openid-connect-discovery-1_0.html) | `/.well-known/openid-configuration` |
| [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) | Authorization Code flow, ID Tokens (`RS256`), UserInfo, `nonce`, `prompt`, `max_age`, `auth_time`, `at_hash`, Scopes `openid`, `profile`, `email`. |
| [OpenID Connect RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html) | `end_session_endpoint` with `id_token_hint` (also expired), `client_id`, `post_logout_redirect_uri` and `state`; Post-logout Redirect URIs registered per Client; confirmation page unless the hint belongs to the current Session. |
| [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517) JWK / [RFC 7638](https://www.rfc-editor.org/rfc/rfc7638) JWK Thumbprint | JWKS, `kid` as thumbprint. |
| [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) Problem Details | Errors of the admin API. |

## Conformance testing

Every pull request and every commit to `main` runs the [OpenID Foundation conformance suite](https://gitlab.com/openid/conformance-suite) (version `release-v5.3.4`) against the container built from it, with two certification test plans: **Basic OP** (`oidcc-basic-certification-test-plan`, discovery, static Clients) and **RP-Initiated Logout OP** (`oidcc-rp-initiated-logout-certification-test-plan`, `response_type=code`, static Clients). The [Conformance workflow](https://github.com/DuttonAlan/Authora/actions/workflows/conformance.yml) fails if a result changes. See [Run the conformance tests](../how-to/run-the-conformance-tests.md).

All 35 test modules of the Basic OP plan run. None fails. Those that warn or are skipped are recorded, with reasons, in `tests/conformance/expected-failures.json` and `expected-skips.json`:

| Test | Result | Reason |
|---|---|---|
| `oidcc-scope-address`, `oidcc-scope-phone`, `oidcc-scope-all` | Skipped | The `address` and `phone` Scopes are not supported. |
| `oidcc-unsigned-request-object-supported-correctly-or-rejected-as-unsupported` | Skipped | Request objects are not supported; the `request` parameter is rejected with `request_not_supported`. |
| `oidcc-scope-profile` | Warning | Users have only a first and last name, so `profile` returns `name`, `given_name` and `family_name`. |
| `oidcc-scope-email`, `oidcc-alternate-happy-flow` | Warning | ID Tokens contain the claims of the approved Scopes, not only UserInfo. Allowed, but the suite points out the privacy risk. |
| `oidcc-userinfo-post-body` | Warning | UserInfo accepts the Access Token only in the `Authorization` header. |
| `oidcc-ensure-request-with-acr-values-succeeds` | Warning | `acr_values` is ignored; there is no `acr` claim. |
| `oidcc-claims-essential` | Warning | The `claims` parameter is not supported. |

`oidcc-prompt-login`, `oidcc-max-age-1`, `oidcc-ensure-registered-redirect-uri` and `oidcc-ensure-request-object-with-redirect-uri` end with the result *Review*: they pass, and the suite's log contains the page Authora showed (sign-in or error page) for a person to check, as certification requires.

All 11 test modules of the RP-Initiated Logout OP plan run, without failures, warnings or skips. Eight of them end with *Review*, with the page Authora showed: the error page for an invalid, modified or foreign `id_token_hint`, a missing hint with a `post_logout_redirect_uri` and no `client_id`, or an unregistered `post_logout_redirect_uri`; the *You are signed out* page otherwise.

The test Clients are Confidential Clients for which [PKCE is optional](oauth-endpoints.md#pkce), because the Basic OP plan sends PKCE in only one of its tests.

Passing the tests is not a certification: Authora has not been certified by the OpenID Foundation.

## Not supported

Authora deliberately keeps a small feature set. Not supported today:

**Users**

- Self-registration, and changing or resetting one's own password. Administrators create Users and set passwords.
- Email verification; `email_verified` is always `false`.
- Multi-factor authentication and passkeys.
- Sign-in through external identity providers (e.g. Google).

**Protocols**

- Grant types other than `authorization_code` and `refresh_token`, e.g. `client_credentials` for machine-to-machine access or the device flow.
- Session management, front- and back-channel logout: applications are not told when a User signs out at Authora, and ID Tokens have no `sid` claim. The end-session endpoint ignores `logout_hint` and `ui_locales`.
- Pushed authorization requests (PAR), JWT-secured authorization requests (JAR), the `request` and `request_uri` parameters.
- Response modes other than `query`, e.g. `form_post`.
- The `claims` parameter, `acr_values` and the `acr` claim, the `address` and `phone` Scopes, and Access Tokens in the body of UserInfo requests.
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
