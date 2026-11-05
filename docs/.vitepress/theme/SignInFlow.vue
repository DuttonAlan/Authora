<script setup lang="ts">
// The homepage's hero: what Authora does for an application, in the steps of the Authorization Code flow.
import { withBase } from 'vitepress'

const steps = [
  { text: 'Your app sends the user to Authora', code: 'GET /oauth/authorize' },
  { text: 'The user signs in at Authora', code: '/account/login' },
  { text: 'Authora returns a one-time code to your app', code: '?code=…' },
  { text: 'Your app exchanges the code for tokens', code: 'POST /oauth/token' },
]
</script>

<template>
  <figure class="signin-flow">
    <figcaption>Your app never sees the password.</figcaption>

    <ol role="list">
      <li v-for="(step, index) in steps" :key="step.code">
        <span class="number" aria-hidden="true">{{ index + 1 }}</span>
        <div>
          <p class="text">{{ step.text }}</p>
          <code class="code">{{ step.code }}</code>
        </div>
      </li>
    </ol>

    <p class="more">
      <a :href="withBase('/how-to/connect-an-application')">Connect an application</a>
    </p>
  </figure>
</template>

<style scoped>
.signin-flow {
  margin: 0;
  padding: 24px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 12px;
  background-color: var(--vp-c-bg-soft);
  text-align: left;
}

figcaption {
  margin-bottom: 20px;
  color: var(--vp-c-text-1);
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
}

ol {
  margin: 0;
  padding: 0;
  list-style: none;
}

li {
  position: relative;
  display: flex;
  gap: 12px;
  padding-bottom: 20px;
}

li:last-child {
  padding-bottom: 0;
}

/* The line between the numbers shows that the steps follow each other. */
li:not(:last-child)::before {
  content: '';
  position: absolute;
  top: 28px;
  bottom: 0;
  left: 13px;
  width: 2px;
  background-color: var(--vp-c-divider);
}

.number {
  flex: none;
  width: 28px;
  height: 28px;
  border: 2px solid var(--vp-c-brand-1);
  border-radius: 50%;
  color: var(--vp-c-brand-1);
  font-size: 13px;
  font-weight: 600;
  line-height: 24px;
  text-align: center;
}

.text {
  margin: 0;
  color: var(--vp-c-text-1);
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
}

.code {
  color: var(--vp-c-text-2);
  font-family: var(--vp-font-family-mono);
  font-size: 13px;
  line-height: 20px;
  overflow-wrap: anywhere;
}

.more {
  margin: 20px 0 0;
  font-size: 14px;
  line-height: 20px;
}

.more a {
  color: var(--vp-c-brand-1);
  font-weight: 500;
  text-decoration: underline;
  text-underline-offset: 2px;
}
</style>
