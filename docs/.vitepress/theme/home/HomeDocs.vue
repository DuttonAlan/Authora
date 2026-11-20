<script setup lang="ts">
// The four kinds of documentation (Diátaxis), as the homepage's last step.
import { withBase } from 'vitepress'

const sections = [
  {
    title: 'Tutorials',
    text: 'Run Authora with Docker Compose and sign in to a first application.',
    href: '/tutorials/getting-started',
  },
  {
    title: 'How-to guides',
    text: 'Deploy to production, rotate keys, back up, connect apps and APIs.',
    href: '/overview#how-to-guides',
  },
  {
    title: 'Reference',
    text: 'Every endpoint, setting, Permission, audit event and log event.',
    href: '/overview#reference',
  },
  {
    title: 'Explanation',
    text: 'The architecture, tokens and sessions, and the security model.',
    href: '/overview#explanation',
  },
]
</script>

<template>
  <section class="home-section" aria-labelledby="docs-heading">
    <div class="home-container">
      <h2 id="docs-heading" class="home-heading">Read the documentation</h2>
      <ul class="doors" role="list">
        <li v-for="section in sections" :key="section.title">
          <a class="door" :href="withBase(section.href)">
            <span class="door-title">{{ section.title }}</span>
            <span class="door-text">{{ section.text }}</span>
          </a>
        </li>
      </ul>
    </div>
  </section>
</template>

<style scoped>
.doors {
  display: grid;
  gap: 16px;
  margin: 32px 0 0;
  padding: 0;
  list-style: none;
}

li {
  display: flex;
  min-width: 0;
}

.door {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 6px;
  padding: 20px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 12px;
  background-color: var(--vp-c-bg-soft);
  transition: border-color 0.2s;
}

.door:hover {
  border-color: var(--vp-c-brand-1);
}

.door:focus-visible {
  outline: 2px solid var(--vp-c-brand-1);
  outline-offset: 2px;
}

.door-title {
  color: var(--vp-c-text-1);
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
}

.door-title::after {
  content: ' →';
  color: var(--vp-c-brand-1);
}

.door-text {
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 22px;
}

@media (min-width: 640px) {
  .doors {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (min-width: 1024px) {
  .doors {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}
</style>
