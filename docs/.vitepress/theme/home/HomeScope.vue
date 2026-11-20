<script setup lang="ts">
// What Authora leaves out and the project's status, stated before anyone relies on it.
// Keep the list in sync with "Not supported" in docs/reference/standards.md.
import { withBase } from 'vitepress'

const notSupported = [
  'Multi-factor authentication and passkeys',
  'Self-registration and password reset by users',
  'Sign-in through Google or other identity providers',
  'Front- and back-channel logout',
  'Dynamic Client registration',
]
</script>

<template>
  <section class="home-section" aria-labelledby="scope-heading">
    <div class="home-container scope-grid">
      <div class="scope">
        <h2 id="scope-heading" class="home-heading">Small on purpose</h2>
        <p class="home-subheading">
          Authora covers sign-in for the applications you run. It leaves out what a small project rarely needs, and
          the documentation says exactly what that is. Not supported today:
        </p>
        <ul role="list">
          <li v-for="item in notSupported" :key="item">{{ item }}</li>
        </ul>
        <a class="home-link" :href="withBase('/reference/standards#not-supported')">Everything that is not supported</a>
      </div>

      <aside class="status" aria-labelledby="status-heading">
        <svg class="status-icon" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.3 3.9 2.4 17.5A2 2 0 0 0 4.1 20.5h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z" /><path d="M12 9v4" /><path d="M12 17h.01" /></svg>
        <h3 id="status-heading">Project status: early</h3>
        <p>
          Authora is young and has <strong>not had an independent security audit</strong>. Try it, build on it and
          report what you find, but weigh that before you protect production data with it.
        </p>
        <a class="home-link" :href="withBase('/explanation/security-model')">Read the security model</a>
      </aside>
    </div>
  </section>
</template>

<style scoped>
.scope-grid {
  display: grid;
  gap: 40px;
}

.scope,
.status {
  min-width: 0;
}

ul {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 24px 0;
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

/* A dash, not a check: these are things Authora does not do. */
li::before {
  content: '';
  position: absolute;
  top: 10px;
  left: 2px;
  width: 8px;
  height: 2px;
  border-radius: 1px;
  background-color: var(--vp-c-text-3);
}

.status {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-self: start;
  padding: 24px;
  border: 1px solid var(--vp-c-warning-2);
  border-radius: 16px;
  background-color: var(--vp-c-warning-soft);
}

.status-icon {
  width: 24px;
  height: 24px;
  color: var(--vp-c-warning-1);
}

h3 {
  margin: 4px 0 0;
  color: var(--vp-c-text-1);
  font-size: 18px;
  font-weight: 600;
  line-height: 26px;
}

.status p {
  margin: 0 0 8px;
  color: var(--vp-c-text-1);
  font-size: 15px;
  line-height: 24px;
}

.status strong {
  font-weight: 600;
}

@media (min-width: 960px) {
  .scope-grid {
    grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
    gap: 64px;
  }
}
</style>
