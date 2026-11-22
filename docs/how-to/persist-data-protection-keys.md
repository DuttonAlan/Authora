# Persist Data Protection keys

Authora uses ASP.NET Core Data Protection to encrypt the antiforgery tokens of its forms, the cookies of the admin interface, which hold the administrator's Access and Refresh Token, and the secrets of the Users' authenticator apps. This guide shows how to store its keys durably and encrypted.

You need this as soon as Users set up an authenticator app, and whenever you run more than one instance or the container has no persistent home directory. Without it, the keys live in memory or in the container, so every restart signs all administrators out and makes every authenticator app unusable (Users then need a recovery code or an administrator's reset), and with several instances a cookie set by one instance cannot be read by another.

## 1. Create a certificate

The certificate encrypts the keys at rest. Create a self-signed one; it is only used for encryption, so nobody needs to trust it:

```sh
openssl req -x509 -newkey rsa:3072 -nodes -days 3650 -subj "/CN=Authora Data Protection" \
  -keyout dp.key -out dp.crt
openssl pkcs12 -export -inkey dp.key -in dp.crt -out data-protection.pfx
rm dp.key
```

`openssl pkcs12` asks for a password for the `.pfx` file.

## 2. Provide a shared directory

Give every instance the same directory for the keys, e.g. a volume or a network file system. The container image creates `/var/lib/authora/keys`, owned by its user, for this purpose. Restrict the directory to the user that runs Authora.

## 3. Configure Authora

```sh
Authora__DataProtection__KeysPath=/var/lib/authora/keys
Authora__DataProtection__CertificatePath=/run/secrets/authora-data-protection.pfx
Authora__DataProtection__CertificatePassword=…
```

Mount the `.pfx` file as a secret and pass the password like your other secrets. Every instance needs the same certificate.

Authora does not start if the certificate cannot be loaded with its private key, if the password is wrong, or if `CertificatePath` is set without `KeysPath`.

## Things to know

- **Existing keys stay unencrypted.** The certificate only encrypts keys created after it was set; older keys stay in the directory in plain text, because Data Protection keeps every key to decrypt what it once encrypted. Do not delete them to start over: that also makes the authenticator apps of Users who set them up with those keys unusable. Set the certificate before Users set up authenticator apps.
- **Replacing the certificate** makes the keys encrypted with the old one unreadable: Authora creates new keys, administrators sign in again, and every authenticator app set up before stops working (Users sign in with a recovery code and set it up again, or an administrator resets them). Authora cannot read keys with a previous certificate, so keep the certificate for as long as you keep the keys; a long validity, as in step 1, avoids a forced replacement.
- **Without a certificate**, the keys are stored in plain text, and whoever can read them can decrypt a stolen admin cookie.
- **Backups:** back up the keys and the certificate, but not together with the database backup: together they decrypt the secrets of the authenticator apps. Losing them signs administrators out and makes every authenticator app unusable.

## Keep the keys

Data Protection keeps retired keys to decrypt what they encrypted, and the secrets of authenticator apps are kept for as long as the User keeps the app. Never delete files from the key directory, and back the directory up together with the certificate and its password, see [Back up and restore](back-up-and-restore.md). If the keys are lost anyway, Authora logs event 16009 for every User who tries a code of their app; they sign in with a recovery code and set the app up again, or an administrator resets their multi-factor authentication.
