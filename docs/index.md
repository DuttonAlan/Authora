---
layout: home
markdownStyles: false
title: Authora
titleTemplate: Self-hosted OAuth 2.1 and OpenID Connect
---

<!-- The homepage's sections are Vue components in .vitepress/theme/home/. The quickstart stays in Markdown for its highlighted, copyable code. -->

<HomeHero />

<HomeAudiences />

<section class="home-section home-quickstart" aria-labelledby="quickstart-heading">
<div class="home-container quickstart-grid">
<div class="quickstart-intro">
<h2 id="quickstart-heading" class="home-heading">Run it in minutes</h2>
<p class="home-subheading">One container and one PostgreSQL database. You need Docker and OpenSSL.</p>
</div>
<div class="quickstart-code vp-doc">

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

</div>
<div class="quickstart-next vp-doc">

Then open <https://localhost:8443/admin/>, accept the self-signed certificate, and sign in with the administrator from `.env`. The [Getting started tutorial](tutorials/getting-started.md) walks through each step.

For production, use the published images `ghcr.io/duttonalan/authora` and `ghcr.io/duttonalan/authora-migrator`, see [Deploy to production](how-to/deploy-to-production.md).

</div>
</div>
</section>

<HomeStandards />

<HomeScope />

<HomeDocs />
