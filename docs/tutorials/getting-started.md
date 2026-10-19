# Getting started

In this tutorial you run Authora on your own computer with Docker Compose, sign in to the admin interface as the first administrator, and look around. It takes about ten minutes.

When you are done, you will have Authora running at `https://localhost:8443`, with a PostgreSQL database and an administrator account. The [next tutorial](first-application.md) builds on it.

> This setup is for trying Authora out. It uses a self-signed certificate and keeps its keys in a local folder. To run Authora for real users, follow [Deploy to production](../how-to/deploy-to-production.md).

## What you need

- [Docker](https://docs.docker.com/get-docker/) with Docker Compose v2 (`docker compose version` prints a version)
- [OpenSSL](https://www.openssl.org/) (preinstalled on macOS and most Linux distributions)
- Git

## 1. Get the code

```sh
git clone https://github.com/DuttonAlan/Authora.git
cd Authora
```

## 2. Create the keys

Authora needs two keys that you create yourself:

- a **signing key**, which signs the ID Tokens Authora issues, and
- a **TLS certificate**, because Authora only works over HTTPS: the sign-in page refuses to work without it.

Create both in a folder called `secrets` (Git ignores it):

```sh
mkdir -p secrets

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out secrets/signing-key.pem

openssl req -x509 -newkey rsa:2048 -nodes -days 365 \
  -subj "/CN=localhost" -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
  -keyout secrets/tls.key -out secrets/tls.crt

chmod 644 secrets/*
```

The `chmod` lets the container, which runs as a different user, read the files. That is fine on your own computer. In production, keys are readable only by Authora; see [Deploy to production](../how-to/deploy-to-production.md).

## 3. Choose the passwords

Copy the example settings:

```sh
cp .env.example .env
```

Open `.env` and fill in two passwords:

```sh
POSTGRES_PASSWORD=a-long-random-database-password
AUTHORA_ADMIN_EMAIL=admin@example.com
AUTHORA_ADMIN_PASSWORD=choose-a-password-of-12-or-more-characters
```

`AUTHORA_ADMIN_EMAIL` and `AUTHORA_ADMIN_PASSWORD` are the credentials of the first administrator, whom Authora creates on its first start. The password must be 12 to 128 characters long.

## 4. Start Authora

```sh
docker compose up --build
```

The first start takes a few minutes, because Docker builds Authora from the source. You see three services start, one after the other:

1. `db`, the PostgreSQL database.
2. `migrate`, which creates the database tables and then exits with `Done.`
3. `authora`, the server.

Authora is ready when the log shows:

```
authora-1  | Now listening on: https://[::]:8443
```

Leave this terminal open. In a second terminal, check that Authora answers:

```sh
curl -k https://localhost:8443/health
```

```json
{"status":"Healthy"}
```

The `-k` tells curl to accept the self-signed certificate.

## 5. Sign in to the admin interface

Open <https://localhost:8443/admin/> in your browser.

Your browser warns that the connection is not private, because it does not know your self-signed certificate. That is expected here: continue to the site (in Chrome: *Advanced* → *Proceed to localhost*; in Firefox: *Advanced* → *Accept the Risk and Continue*).

Authora shows its sign-in page. Sign in with the Email and password from your `.env` file.

You are now in the admin interface. Notice that you were not asked whether the admin interface may access your account: it is a *first-party* application of Authora itself, so Authora skips that question. You will see the question in the next tutorial, for an application of your own.

## 6. Look around

Click through the pages in the navigation:

- **Users** shows one User: you. Open it to see your Roles and your effective Permissions. You have the Role `admin`, which holds every Permission.
- **Roles** shows the `admin` Role. It is a System Role, which Authora created and protects.
- **Scopes** lists the Scopes Authora created on its first start: `openid`, `profile` and `email` for OpenID Connect, and `authora.admin` for the admin API.
- **Clients** lists the Client `Authora Admin UI`. It is the admin interface you are using right now: it signs in at Authora like any other application.
- **Audit log** shows what has happened so far, including your sign-in (`account.sign-in`) and the creation of the first administrator (`system.administrator-bootstrapped`).

## 7. Remove the bootstrap password

The administrator exists now, so Authora no longer needs the password in `.env`. As long as it is there, Authora logs a warning on every start (event 7007).

1. In the admin interface, open **Users**, then your User, and set a new password.
2. In `.env`, delete the value of `AUTHORA_ADMIN_PASSWORD`, so the line reads `AUTHORA_ADMIN_PASSWORD=`.
3. Restart Authora: press `Ctrl+C` in the first terminal, then run `docker compose up` again.

Sign in with your new password.

## What you have learned

- Authora runs as a server next to a PostgreSQL database. Migrations are a separate step that runs before the server starts.
- Authora needs a signing key and HTTPS.
- The first administrator comes from the configuration, and only on the first start.
- The admin interface is itself an application that signs in at Authora.

## Next steps

- Continue with [Your first application](first-application.md).
- To stop Authora, press `Ctrl+C`. `docker compose down` removes the containers but keeps the data; `docker compose down -v` also deletes the database.
