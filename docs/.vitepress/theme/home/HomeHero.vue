<script setup lang="ts">
// The homepage's opening: what Authora is, the two ways in, the project's status, and the sign-in flow.
import { withBase } from 'vitepress'
import { VPButton } from 'vitepress/theme'
import SignInSequence from './SignInSequence.vue'
</script>

<template>
  <section class="home-hero">
    <div class="home-container hero-grid">
      <div class="intro">
        <p class="protocols">OAuth 2.1 · OpenID Connect · Self-hosted</p>
        <h1 class="headline">Sign-in for your apps, on your own server.</h1>
        <p class="lead">
          Authora is a lightweight identity server for small projects. Your applications send people to Authora to
          sign in and get back standard tokens, so none of them has to build its own authentication.
        </p>
        <p class="lead-detail">One container and one PostgreSQL database. Built on .NET, MIT licensed.</p>

        <div class="actions">
          <VPButton tag="a" size="big" theme="brand" text="Get started" :href="withBase('/tutorials/getting-started')" />
          <VPButton tag="a" size="big" theme="alt" text="View on GitHub" href="https://github.com/DuttonAlan/Authora" />
        </div>

        <p class="status">
          <strong>Early project.</strong> No independent security audit yet.
          <a :href="withBase('/explanation/security-model')">Read the security model</a>
        </p>
      </div>

      <SignInSequence class="flow" />
    </div>
  </section>
</template>

<style scoped>
.home-hero {
  padding: 48px 0 64px;
}

.hero-grid {
  display: grid;
  gap: 48px;
  align-items: center;
}

.intro {
  min-width: 0;
}

.protocols {
  margin: 0 0 16px;
  color: var(--vp-c-brand-1);
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.04em;
  line-height: 20px;
}

.headline {
  max-width: 14ch;
  margin: 0;
  color: var(--vp-c-text-1);
  font-size: clamp(2.25rem, 1.6rem + 2.8vw, 3.75rem);
  font-weight: 700;
  letter-spacing: -0.028em;
  line-height: 1.06;
  text-wrap: balance;
}

.lead {
  max-width: 34rem;
  margin: 24px 0 0;
  color: var(--vp-c-text-1);
  font-size: 18px;
  line-height: 28px;
}

.lead-detail {
  max-width: 34rem;
  margin: 8px 0 0;
  color: var(--vp-c-text-2);
  font-size: 16px;
  line-height: 24px;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 32px;
}

.status {
  margin: 20px 0 0;
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 22px;
}

.status strong {
  color: var(--vp-c-text-1);
  font-weight: 600;
}

.status a {
  color: var(--vp-c-brand-1);
  font-weight: 500;
  text-decoration: underline;
  text-underline-offset: 2px;
  white-space: nowrap;
}

.flow {
  width: 100%;
  max-width: 520px;
  min-width: 0;
}

@media (min-width: 960px) {
  .home-hero {
    padding: 80px 0 96px;
  }

  .hero-grid {
    grid-template-columns: minmax(0, 1.1fr) minmax(0, 1fr);
    gap: 64px;
  }

  .flow {
    justify-self: end;
  }
}
</style>
