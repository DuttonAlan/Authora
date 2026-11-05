<script setup lang="ts">
// States the project's status next to the call to action, before anyone installs Authora.
import { withBase } from 'vitepress'
</script>

<template>
  <p class="hero-status">
    <strong>Early project.</strong> No independent security audit yet.
    <a :href="withBase('/explanation/security-model')">Read the security model</a>
  </p>
</template>

<style scoped>
.hero-status {
  margin: 24px 0 0;
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 22px;
}

.hero-status strong {
  color: var(--vp-c-text-1);
  font-weight: 600;
}

.hero-status a {
  color: var(--vp-c-brand-1);
  font-weight: 500;
  text-decoration: underline;
  text-underline-offset: 2px;
  white-space: nowrap;
}
</style>
