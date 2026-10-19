# Authora

### Identity. Access. Control.

[![Build](https://github.com/DuttonAlan/Authora/actions/workflows/build.yml/badge.svg)](https://github.com/DuttonAlan/Authora/actions/workflows/build.yml)
[![CodeQL](https://github.com/DuttonAlan/Authora/actions/workflows/codeql.yml/badge.svg)](https://github.com/DuttonAlan/Authora/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.txt)

Authora is a lightweight, self-hosted **identity and access management (IAM)** server for small projects and applications: an **OAuth 2.1** authorization server and **OpenID Connect** provider with an admin interface, built on .NET 10 and PostgreSQL.

Your applications let users sign in through Authora instead of each building their own authentication.

> **Project status: early.** Authora is young and has **not had an independent security audit**. Try it, build on it, report what you find, but weigh that before you protect production data with it. See the [security model](docs/explanation/security-model.md) and [what is not supported yet](docs/reference/standards.md#not-supported).

## Features

- 🔐 **OAuth 2.1 and OpenID Connect**: Authorization Code flow with mandatory PKCE, rotating Refresh Tokens with reuse detection, ID Tokens, UserInfo, discovery, revocation and introspection
- 👤 **Users, Groups and Roles**: manage who may administer Authora, with fine-grained Permissions
- 🔑 **Applications**: register public and confidential Clients with exact Redirect URIs, allowed Scopes and expiring Secrets
- 🖥️ **Admin interface** at `/admin`, which never exposes tokens to the browser
- 📜 **Tamper-evident audit log** of sign-ins and every administrative change
- 🛡️ **Secure by default**: opaque Access Tokens that are revoked instantly, only hashes stored, strict headers and cookies, and refusal to start with an insecure configuration
- 🏠 **Self-hosted**: one container and one PostgreSQL database

## Quick start

You need Docker and OpenSSL.

```sh
git clone https://github.com/DuttonAlan/Authora.git && cd Authora

mkdir -p secrets
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out secrets/signing-key.pem
openssl req -x509 -newkey rsa:2048 -nodes -days 365 -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" -keyout secrets/tls.key -out secrets/tls.crt
chmod 644 secrets/*

cp .env.example .env    # then set POSTGRES_PASSWORD and AUTHORA_ADMIN_PASSWORD
docker compose up --build
```

Open <https://localhost:8443/admin/>, accept the self-signed certificate, and sign in with the administrator from `.env`.

The [Getting started tutorial](docs/tutorials/getting-started.md) walks through these steps, and [Your first application](docs/tutorials/first-application.md) signs you in to an application of your own.

## Documentation

The [documentation](docs/README.md) has four parts:

- **Tutorials**: [Getting started](docs/tutorials/getting-started.md), [Your first application](docs/tutorials/first-application.md)
- **How-to guides**: [deploy to production](docs/how-to/deploy-to-production.md), [connect an application](docs/how-to/connect-an-application.md), [protect an API](docs/how-to/protect-an-api.md), [rotate signing keys](docs/how-to/rotate-signing-keys.md), [back up and restore](docs/how-to/back-up-and-restore.md), and more
- **Reference**: [configuration](docs/reference/configuration.md), [OAuth endpoints](docs/reference/oauth-endpoints.md), [OpenID Connect](docs/reference/openid-connect.md), [admin API](docs/reference/admin-api.md), [audit events](docs/reference/audit-events.md)
- **Explanation**: [architecture](docs/explanation/architecture.md), [access model](docs/explanation/access-model.md), [tokens and sessions](docs/explanation/tokens-and-sessions.md), [security model](docs/explanation/security-model.md)

## Supported standards

OAuth 2.1 (Authorization Code with PKCE, Refresh Tokens), RFC 7636 (PKCE, `S256`), RFC 7009 (revocation), RFC 7662 (introspection), RFC 9207 (`iss` in the authorization response), OpenID Connect Core 1.0 and Discovery 1.0. Details: [Standards and limitations](docs/reference/standards.md).

## Roadmap

Planned, roughly in this order; nothing is promised:

- Self-service password change and reset, and Email verification
- Sign-in with external identity providers (OpenID Connect, starting with Google)
- Multi-factor authentication
- RP-initiated logout
- Audience-restricted Access Tokens (RFC 8707)
- Client credentials grant for machine-to-machine access

Ideas and feedback are welcome in [issues](https://github.com/DuttonAlan/Authora/issues).

## Contributing

Contributions are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test and submit changes, and follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Security

Please do **not** report vulnerabilities in public issues. See [SECURITY.md](SECURITY.md) for how to report them privately.

## License

[MIT](LICENSE.txt) © Alan Dutton
