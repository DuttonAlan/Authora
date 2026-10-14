# Authora – OpenID Connect

This document describes the OpenID Connect provider of Authora (Phase 5, US-022 – US-026): discovery, ID Tokens, signing keys and their rotation (US-035), UserInfo and the identity claims. It builds on the OAuth 2.1 server described in [oauth-core.md](oauth-core.md).

- Application logic: `src/Authora.Core/Application/Oidc/`
- Endpoints: `src/Authora.Api/Controllers/OidcController.cs` and `src/Authora.Api/Pages/OAuth/Authorize.cshtml.cs`
- Signing keys: `src/Authora.Infrastructure/Security/PemSigningKeyProvider.cs`

## Endpoints

| Endpoint | Used by | Purpose |
|---|---|---|
| `GET /.well-known/openid-configuration` | Client | Discovery document (US-022). |
| `GET /.well-known/jwks.json` | Client | Public signing keys (US-024). |
| `GET/POST /oauth/userinfo` | Client | Claims of the User of an Access Token (US-025). |

The discovery document and the JWKS are public, allow CORS from every origin (only `GET`, no credentials) and may be cached for one hour (`Cache-Control: public, max-age=3600`). `/oauth/userinfo` allows CORS from the origins of the Redirect URIs of enabled Clients, `GET` and `POST`, only the `Authorization` header and no credentials.

## Discovery (US-022)

All endpoint URLs are built from `Authora:OAuth:Issuer`, so they always match `issuer`. The Issuer must not end with `/`. If it has a path (e.g. `https://example.com/auth`), Authora must be served under that path.

The document only lists what Authora supports: `response_type` `code`, `response_mode` `query`, PKCE `S256`, `RS256`, public subject identifiers, the Scopes `openid`, `profile` and `email`, and the `prompt` values below. `request_parameter_supported`, `request_uri_parameter_supported` and `claims_parameter_supported` are sent as `false`, because the default of `request_uri_parameter_supported` would be `true`. `authorization_response_iss_parameter_supported` is `true` (RFC 9207).

## Scopes and claims (US-026)

`openid`, `profile` and `email` are seeded as System Scopes. Like every Scope, an administrator must allow them for a Client (`PUT /api/clients/{id}/scopes/{scopeId}`), and the User approves them on the consent screen. This is how administrators decide which identity attributes an application receives.

The names are reserved from this version on. If a regular Scope named `openid`, `profile` or `email` already exists, Authora does not start, because promoting it would silently hand its Clients identity claims. Delete that Scope before upgrading (`DELETE /api/scopes/{id}?force=true` on the previous version, or in the database), then start the new version.

| Scope | Claims |
|---|---|
| `openid` | `sub` (the User Id, the same for every Client) |
| `profile` | `name` (First and Last Name), `given_name`, `family_name` |
| `email` | `email`, `email_verified` (always `false`, Authora does not verify Emails yet) |

ID Tokens and UserInfo use the same code (`IdentityClaims`), so their claims are always consistent. Claims Authora has no value for (e.g. `picture`, `birthdate`, `updated_at`) are never returned, not even as `null`. Roles and Permissions are never released (see TD-006).

## Authorization request

In addition to the parameters in [oauth-core.md](oauth-core.md#authorization-request):

| Parameter | Rule |
|---|---|
| `nonce` | Optional, at most 512 characters. Returned unchanged in the ID Token. |
| `prompt` | Optional. `none` must not be combined with other values. Unknown values are ignored. |
| `max_age` | Optional, a non-negative number of seconds. |
| `request`, `request_uri`, `registration` | Not supported. Answered with `request_not_supported`, `request_uri_not_supported` or `registration_not_supported`. |
| `display`, `ui_locales`, `login_hint`, `id_token_hint`, `acr_values`, `claims_locales`, `claims` | Ignored. |

| `prompt` | Behaviour |
|---|---|
| `none` | No page is shown: `login_required` if the User is not signed in or must sign in again, otherwise `consent_required`, because consent is asked on every request. |
| `login` | The User signs in again, even if signed in. |
| `select_account` | Same as `login`, there is no account chooser. |
| `consent` | No change, consent is always asked. |

If the sign-in is older than `max_age`, the User signs in again. This is checked both when the consent screen is shown and when the User approves it, so a consent screen left open too long does not issue a code. After that sign-in, the request continues without `login`, `select_account` and `max_age`, so the User is not sent back to the sign-in page in a loop. The ID Token always contains `auth_time`, so Clients can check `max_age` themselves.

`prompt` and `max_age` are front-channel parameters: whoever controls the browser can remove them from the URL, and Authora cannot tell that apart from a request without them (only signed requests, PAR or JAR, would prevent it, and Authora supports neither). Clients that need a recent sign-in, e.g. for step-up before a sensitive action, must compare `auth_time` with the time they sent the request, and must not rely on `prompt=login` or `max_age` alone.

## ID Tokens (US-023)

An ID Token is returned by `POST /oauth/token` when an Authorization Code whose Scopes include `openid` is exchanged, and `openid` is still allowed for the Client. Refreshing returns no ID Token (OpenID Connect leaves this optional).

```json
{
  "access_token": "…",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "…",
  "scope": "openid profile",
  "id_token": "eyJhbGciOiJSUzI1NiIsImtpZCI6Ii4uLiIsInR5cCI6IkpXVCJ9.…"
}
```

| Claim | Value |
|---|---|
| `iss` | `Authora:OAuth:Issuer` |
| `sub` | User Id |
| `aud` | `client_id` |
| `iat`, `exp` | Issue time, plus `Authora:Oidc:IdTokenLifetime` |
| `auth_time` | When the User signed in (start of the Session) |
| `nonce` | The `nonce` of the authorization request, if sent |
| `at_hash` | Hash of the Access Token (OpenID Connect Core 1.0, Section 3.1.3.6) |
| identity claims | See [Scopes and claims](#scopes-and-claims-us-026) |

The header contains `alg: RS256` and the `kid` of the signing key. Clients must validate the signature with the JWKS, `iss`, `aud`, `exp` and, if they sent one, `nonce`.

ID Tokens are never stored and cannot be used as Access Tokens: the UserInfo endpoint, introspection and the admin API only accept the opaque Access Tokens, which are looked up by their hash. Access Tokens stay opaque.

## Signing keys and JWKS (US-024)

ID Tokens are signed with an RSA key (at least 2048 bits) from a PEM file. Keys are not stored in the database, so a copy of the database cannot be used to forge ID Tokens. Create a key with:

```sh
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out signing-key.pem
```

Mount it into the container (e.g. as a Docker or Kubernetes secret) and point `Authora:Oidc:SigningKeyPath` at it. Keys can be replaced while Authora is running, see [Key rotation](#key-rotation-us-035). `*.pem` is in `.gitignore`; never commit a key. For development, `appsettings.Development.json` uses `signing-key.pem` in `src/Authora.Api`.

The `kid` of every key is its JWK Thumbprint (RFC 7638), so it is unique per key and the same on every instance and after restarts. The JWKS only contains `kty`, `use`, `alg`, `kid`, `n` and `e`; the type used for it has no members for private parameters.

### Key rotation (US-035)

Every instance checks the key files and the paths every `Authora:Oidc:KeyReloadInterval` (default 1 minute) and uses changed keys from then on, without a restart. Only a hash of the files is compared, so unchanged files are not parsed again. If a changed file cannot be loaded (e.g. it is only half written, or the new signing key has no private part), the instance keeps the previous keys, logs event 9003 and reports the health check `signing-keys` as `Degraded` until the files are fixed. A restart with an invalid file fails, as before.

Routine rotation:

1. Create the new key and add it to `Authora:Oidc:AdditionalKeyPaths`. It is published, but does not sign.
2. Wait at least `KeyReloadInterval` plus one hour (the cache duration of the JWKS), so all instances publish the new key and Clients know it.
3. Make the new key the `SigningKeyPath`, and move the old key to `AdditionalKeyPaths`. A file with only the public key is enough for it.
4. Wait at least `KeyReloadInterval` plus `IdTokenLifetime`, then remove the old key.

Change files by writing a new file and renaming it over the old one (Kubernetes does this for mounted secrets), so no instance reads a half-written file. Changing the paths in `appsettings.json` is picked up as well; environment variables are only read at startup.

Every change of the signing key or the published keys is recorded in the audit log as `system.signing-keys-changed`, with the `kid` of the signing key as target and the `kid`s of all published keys in the details. Each instance compares its keys with the newest such event, so a rotation by restart is recorded too, and several instances record the same change once (unless they check at the same moment).

**Already issued tokens:**

- **ID Tokens** stay verifiable as long as the key that signed them is published. Step 4 keeps the old key until every ID Token it signed has expired. Clients that cache the JWKS longer than one hour, or that verify ID Tokens later than `IdTokenLifetime` after issue, can reject tokens after the old key is removed.
- **Access and Refresh Tokens** are opaque and not signed, so rotation does not affect them.

**Compromised key:** remove it from `SigningKeyPath` and `AdditionalKeyPaths` at once (skip steps 1, 2 and 4), with a new signing key. ID Tokens signed with the compromised key fail verification as soon as Clients refresh the JWKS (at most one hour); Clients that still accept them meanwhile have to be told directly. Consider revoking the Sessions of all Users (`POST /api/users/{id}/revoke-sessions`) if forged ID Tokens may have been accepted.

**Storing private keys:** keys never go into the database or the image. Mount them as a secret (Docker or Kubernetes secret, or a file readable only by the user that runs Authora, e.g. `chmod 600`). On Linux and macOS, Authora logs event 9004 if the signing key file can be read or written by other users. Back up the private key separately from the database, see [operations.md](operations.md#backup-and-recovery).

## UserInfo (US-025)

`GET` or `POST /oauth/userinfo` with `Authorization: Bearer <access token>`. The token must be active (see [oauth-core.md](oauth-core.md#token-model)) and contain `openid`. The response contains `sub` and the claims of the token's Scopes, so a refreshed token with fewer Scopes also returns fewer claims.

| Case | Response |
|---|---|
| No `Authorization: Bearer` header | `401`, `WWW-Authenticate: Bearer` |
| Unknown, expired or revoked token, or an ID Token | `401`, `WWW-Authenticate: Bearer error="invalid_token"` |
| Token without `openid` | `403`, `WWW-Authenticate: Bearer error="insufficient_scope", scope="openid"` |

Responses are sent with `Cache-Control: no-store`.

## Configuration

| Setting | Default | Description |
|---|---|---|
| `Authora:Oidc:SigningKeyPath` | – (required) | PEM file with the RSA private key that signs ID Tokens. Relative paths are resolved against the content root. Startup fails if it is missing, unreadable, encrypted, not RSA, shorter than 2048 bits or only a public key. |
| `Authora:Oidc:AdditionalKeyPaths` | `[]` | PEM files with RSA keys that are only published, for key rotation. Public keys are enough. Environment variables: `Authora__Oidc__AdditionalKeyPaths__0`, … |
| `Authora:Oidc:IdTokenLifetime` | `00:05:00` | Lifetime of ID Tokens, at most 1 hour. |
| `Authora:Oidc:KeyReloadInterval` | `00:01:00` | How often the key files are checked for changes, 10 seconds to 1 hour. Changing it needs a restart. |

## Logging

| Event Id | Message |
|---|---|
| 9001 | The `kid` of the signing key and the number of published keys, at startup. |
| 9002 | The key files changed; the new signing key and the number of published keys. |
| 9003 | Changed key files could not be loaded; the previous keys stay in use. |
| 9004 | The signing key file can be read or written by other users of the system (not checked on Windows). |
| 9005 | A change of the keys could not be recorded in the audit log; retried with the next check. |

Key material, ID Tokens and nonces are never logged.

## Known limitations

- Keys are files that the operator creates and replaces; Authora does not generate or rotate keys on a schedule itself.
- `email_verified` is always `false` until Emails can be verified.
- Only public subject identifiers, no pairwise ones.
- No RP-initiated logout, session management or front-/back-channel logout.
- No dynamic Client registration.
