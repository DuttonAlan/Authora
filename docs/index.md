---
layout: home
title: Authora
titleTemplate: Self-hosted OAuth 2.1 and OpenID Connect

hero:
  name: Authora
  text: Sign-in for your apps, on your own server.
  tagline: A lightweight, self-hosted identity and access management server for small projects. Your applications let users sign in through Authora instead of each building their own authentication.
  actions:
    - theme: brand
      text: Get started
      link: /tutorials/getting-started
    - theme: alt
      text: View on GitHub
      link: https://github.com/DuttonAlan/Authora

features:
  - icon: '<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="M13 4h5a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-5"/><path d="M4 12h10"/><path d="m10 8 4 4-4 4"/></svg>'
    title: OAuth 2.1 and OpenID Connect
    details: Authorization Code flow with mandatory PKCE, rotating Refresh Tokens with reuse detection, ID Tokens, UserInfo, discovery, revocation and introspection.
    link: /reference/standards
    linkText: Supported standards
  - icon: '<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><circle cx="9" cy="8" r="3.5"/><path d="M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6"/><path d="M16 4.8a3.5 3.5 0 0 1 0 6.4"/><path d="M18 14.5c1.8.9 3 2.9 3 5.5"/></svg>'
    title: Users, Groups and Roles
    details: Manage who may administer Authora, with fine-grained Permissions.
    link: /explanation/access-model
    linkText: The access model
  - icon: '<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 9h18"/><path d="M7 6.5h.01"/><path d="M10.5 6.5h.01"/></svg>'
    title: Applications
    details: Register public and confidential Clients with exact Redirect URIs, allowed Scopes and expiring Secrets.
    link: /how-to/connect-an-application
    linkText: Connect an application
  - icon: '<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h9"/><path d="M17 7h3"/><circle cx="15" cy="7" r="2"/><path d="M4 17h3"/><path d="M11 17h9"/><circle cx="9" cy="17" r="2"/></svg>'
    title: Admin interface
    details: A browser interface at /admin with a backend for frontend, so tokens never reach the browser.
    link: /explanation/admin-interface
    linkText: How it works
  - icon: '<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><rect x="2.5" y="8.5" width="9" height="7" rx="3.5"/><rect x="12.5" y="8.5" width="9" height="7" rx="3.5"/><path d="M9 12h6"/></svg>'
    title: Tamper-evident audit log
    details: Sign-ins and every administrative change, sealed into a hash chain you can verify.
    link: /explanation/audit-log-integrity
    linkText: Audit log integrity
  - icon: '<svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round"><path d="m12 3 7 3v6c0 4.5-3 7.6-7 9-4-1.4-7-4.5-7-9V6z"/><path d="m9 12 2 2 4-4"/></svg>'
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
