<script setup lang="ts">
// What Authora offers, grouped by who uses it: the developers, administrators and operators of a small project.
import { withBase } from 'vitepress'

const audiences = [
  {
    title: 'For your applications',
    summary: 'Standard OAuth 2.1 and OpenID Connect, so the client libraries you already use work with it.',
    items: [
      'Authorization Code flow, with PKCE required by default',
      'ID Tokens, UserInfo, discovery and JWKS',
      'Rotating Refresh Tokens with reuse detection',
      'Client Credentials for machine-to-machine access',
      'Sign-out started by your app',
      'Introspection and revocation for your APIs',
    ],
    link: { text: 'Connect an application', href: '/how-to/connect-an-application' },
  },
  {
    title: 'For administrators',
    summary: 'A browser interface at /admin, and a REST API for everything in it.',
    items: [
      'Users, Groups and Roles with fine-grained Permissions',
      'Public and Confidential Clients with exact Redirect URIs',
      'Client Secrets that expire',
      'A consent screen for third-party apps, none for your own',
      'Tokens stay on the server, never in the browser',
    ],
    link: { text: 'Manage users and access', href: '/how-to/manage-users-and-access' },
  },
  {
    title: 'For operators',
    summary: 'Two container images and a PostgreSQL database. Nothing else to run.',
    items: [
      'Refuses to start with an insecure production configuration',
      'Signing keys reloaded without a restart, for rotation',
      'An audit log sealed into a hash chain you can verify',
      'Health checks and documented log events',
      'Database migrations as their own image',
    ],
    link: { text: 'Deploy to production', href: '/how-to/deploy-to-production' },
  },
]
</script>

<template>
  <section class="home-section" aria-labelledby="audiences-heading">
    <div class="home-container">
      <h2 id="audiences-heading" class="home-heading">What Authora does</h2>
      <p class="home-subheading">
        One server covers the whole sign-in of a small project, from the app that asks for a token to the person
        who keeps it running.
      </p>

      <div class="audiences">
        <article v-for="audience in audiences" :key="audience.title" class="audience">
          <h3>{{ audience.title }}</h3>
          <p class="summary">{{ audience.summary }}</p>
          <ul role="list">
            <li v-for="item in audience.items" :key="item">{{ item }}</li>
          </ul>
          <a class="home-link" :href="withBase(audience.link.href)">{{ audience.link.text }}</a>
        </article>
      </div>
    </div>
  </section>
</template>

<style scoped>
.audiences {
  display: grid;
  gap: 40px;
  margin-top: 40px;
}

.audience {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 0;
  padding-top: 24px;
  border-top: 1px solid var(--vp-c-divider);
}

h3 {
  margin: 0;
  color: var(--vp-c-text-1);
  font-size: 20px;
  font-weight: 600;
  letter-spacing: -0.01em;
  line-height: 28px;
}

.summary {
  margin: 0;
  color: var(--vp-c-text-2);
  font-size: 16px;
  line-height: 24px;
}

ul {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 4px 0 8px;
  padding: 0;
  list-style: none;
}

li {
  position: relative;
  padding-left: 20px;
  color: var(--vp-c-text-1);
  font-size: 15px;
  line-height: 22px;
}

li::before {
  content: '';
  position: absolute;
  top: 9px;
  left: 2px;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: var(--vp-c-brand-1);
}

.home-link {
  margin-top: auto;
}

@media (min-width: 768px) {
  .audiences {
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 32px;
  }
}
</style>
