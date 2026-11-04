---
layout: home
title: Authora
titleTemplate: Self-hosted OAuth 2.1 and OpenID Connect

hero:
  name: Authora
  text: Identity. Access. Control.
  tagline: A lightweight, self-hosted identity and access management server for small projects and applications. Your applications let users sign in through Authora instead of each building their own authentication.
  image:
    src: /logo.svg
    alt: Authora
  actions:
    - theme: brand
      text: Get started
      link: /tutorials/getting-started
    - theme: alt
      text: Documentation
      link: /overview
    - theme: alt
      text: View on GitHub
      link: https://github.com/DuttonAlan/Authora

features:
  - icon: 🔐
    title: OAuth 2.1 and OpenID Connect
    details: Authorization Code flow with mandatory PKCE, rotating Refresh Tokens with reuse detection, ID Tokens, UserInfo, discovery, revocation and introspection.
    link: /reference/standards
    linkText: Supported standards
  - icon: 👤
    title: Users, Groups and Roles
    details: Manage who may administer Authora, with fine-grained Permissions.
    link: /explanation/access-model
    linkText: The access model
  - icon: 🔑
    title: Applications
    details: Register public and confidential Clients with exact Redirect URIs, allowed Scopes and expiring Secrets.
    link: /how-to/connect-an-application
    linkText: Connect an application
  - icon: 🖥️
    title: Admin interface
    details: A browser interface at /admin with a backend for frontend, so tokens never reach the browser.
    link: /explanation/admin-interface
    linkText: How it works
  - icon: 📜
    title: Tamper-evident audit log
    details: Sign-ins and every administrative change, sealed into a hash chain you can verify.
    link: /explanation/audit-log-integrity
    linkText: Audit log integrity
  - icon: 🛡️
    title: Secure by default
    details: Opaque Access Tokens that are revoked instantly, only hashes stored, strict headers and cookies, and refusal to start with an insecure configuration.
    link: /explanation/security-model
    linkText: Security model
---

<div class="vp-doc home-content">

## Run it in minutes

One container and one PostgreSQL database. With Docker and OpenSSL:

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

Open <https://localhost:8443/admin/>, accept the self-signed certificate, and sign in with the administrator from `.env`. The [Getting started tutorial](tutorials/getting-started.md) walks through these steps.

For production, use the published images `ghcr.io/duttonalan/authora` and `ghcr.io/duttonalan/authora-migrator`, see [Deploy to production](how-to/deploy-to-production.md).

::: warning Project status: early
Authora is young and has **not had an independent security audit**. Try it, build on it, report what you find, but weigh that before you protect production data with it. See the [security model](explanation/security-model.md) and [what is not supported yet](reference/standards.md#not-supported).
:::

</div>
