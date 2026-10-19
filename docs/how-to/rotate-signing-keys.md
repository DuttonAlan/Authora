# Rotate signing keys

Authora signs ID Tokens with an RSA key from a PEM file. This guide shows how to replace that key without a restart and without breaking ID Tokens already issued, and what to do if a key is compromised.

Access and Refresh Tokens are opaque and not signed, so rotating keys never affects them.

## How Authora picks up key changes

Every instance checks the key files and their paths every `Authora:Oidc:KeyReloadInterval` (default 1 minute) and uses changed keys from then on. If a changed file cannot be loaded, e.g. because it is half written, the instance keeps its previous keys, logs event 9003 and reports the health check `signing-keys` as `Degraded` until the files are fixed.

Replace files by writing a new file and renaming it over the old one (Kubernetes does this for mounted secrets), so no instance reads a half-written file. Paths changed in `appsettings.json` are picked up too; environment variables are only read at startup.

## Routine rotation

1. **Publish the new key.** Create it and add it to `Authora:Oidc:AdditionalKeyPaths`:

   ```sh
   openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out signing-key-2026-11.pem
   ```

   ```json
   "Oidc": {
     "SigningKeyPath": "/run/secrets/signing-key-2026-05.pem",
     "AdditionalKeyPaths": [ "/run/secrets/signing-key-2026-11.pem" ]
   }
   ```

   The new key now appears in the JWKS, but does not sign anything yet.

2. **Wait** at least `KeyReloadInterval` plus one hour. The JWKS may be cached for an hour, so this gives every instance and every Client time to learn the new key.

3. **Switch.** Make the new key the `SigningKeyPath` and move the old one to `AdditionalKeyPaths`. A file with only the public key is enough for the old one:

   ```sh
   openssl pkey -in signing-key-2026-05.pem -pubout -out signing-key-2026-05.pub.pem
   ```

4. **Wait** at least `KeyReloadInterval` plus `IdTokenLifetime` (default 5 minutes), so every ID Token the old key signed has expired.

5. **Remove** the old key from `AdditionalKeyPaths`.

Each change is recorded in the audit log as `system.signing-keys-changed` and logged as event 9002.

Clients that cache the JWKS for longer than an hour, or verify ID Tokens later than `IdTokenLifetime` after they were issued, may reject tokens after step 5.

## Compromised key

1. Create a new key and make it the `SigningKeyPath`.
2. Remove the compromised key from both `SigningKeyPath` and `AdditionalKeyPaths` at once; skip the waiting periods.
3. ID Tokens signed with the compromised key fail verification as soon as Clients refresh the JWKS, within at most one hour. Tell the operators of Clients that may still accept them in the meantime.
4. If forged ID Tokens may have been accepted, sign all Users out: **Users** → User → **Sign out everywhere**, or `POST /api/users/{id}/revoke-sessions` for each User.

## Lost key

Create a new key and configure it as `SigningKeyPath`. ID Tokens signed with the old key can no longer be verified once Clients refresh the JWKS. That only affects sign-ins of the last few minutes; no data is lost.

## Related

- [OpenID Connect reference: signing keys](../reference/openid-connect.md#signing-keys-and-jwks)
- [Configuration: `Authora:Oidc`](../reference/configuration.md#openid-connect)
