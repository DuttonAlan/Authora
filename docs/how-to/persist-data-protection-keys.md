# Persist Data Protection keys

Authora uses ASP.NET Core Data Protection to encrypt the antiforgery tokens of its forms and the cookies of the admin interface, which hold the administrator's Access and Refresh Token. This guide shows how to store its keys durably and encrypted.

You need this when you run more than one instance, or when the container has no persistent home directory. Without it, the keys live in memory or in the container, so every restart signs all administrators out, and with several instances a cookie set by one instance cannot be read by another.

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

- **Existing keys stay unencrypted.** The certificate only encrypts keys created after it was set. Older keys stay readable until they expire after 90 days. To start over at once, delete the files in the directory; administrators then sign in again.
- **Replacing the certificate** makes the keys encrypted with the old one unreadable. Authora creates new keys, and administrators sign in again. No other data is lost.
- **Without a certificate**, the keys are stored in plain text, and whoever can read them can decrypt a stolen admin cookie.
- **Backups:** do not store the keys or the certificate together with the database backup. Losing them only signs administrators out.
