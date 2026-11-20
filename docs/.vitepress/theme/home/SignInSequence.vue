<script setup lang="ts">
// The homepage's signature: the Authorization Code flow as a two-lane sequence diagram,
// with the HTTP request of each step. A dot travels the arrows once on load; at rest every step is visible.
import { withBase } from 'vitepress'

type Step =
  | { kind: 'to-authora' | 'to-app'; text: string; code: string }
  | { kind: 'at-authora'; text: string; code: string; note: string }

const steps: Step[] = [
  { kind: 'to-authora', text: 'Your app sends the user to Authora', code: 'GET /oauth/authorize' },
  {
    kind: 'at-authora',
    text: 'The user signs in at Authora',
    code: '/account/login',
    note: 'The password is typed here, and only here.',
  },
  { kind: 'to-app', text: 'Authora sends back a one-time code', code: '302 ?code=…&iss=…' },
  { kind: 'to-authora', text: 'Your app exchanges the code', code: 'POST /oauth/token' },
  { kind: 'to-app', text: 'Authora returns the tokens', code: 'access_token · id_token · refresh_token' },
]
</script>

<template>
  <figure class="sequence">
    <figcaption>
      <span class="title">Authorization Code flow with PKCE</span>
      <span class="subtitle">How your app signs a user in</span>
    </figcaption>

    <div class="lanes" aria-hidden="true">
      <span class="lane">Your app</span>
      <span class="lane lane-authora">Authora</span>
    </div>

    <ol class="steps" role="list">
      <li
        v-for="(step, index) in steps"
        :key="step.code"
        :class="['step', step.kind]"
        :style="{ '--step': index }"
      >
        <template v-if="step.kind === 'at-authora'">
          <div class="local">
            <svg class="lock" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="5" y="11" width="14" height="9" rx="2" /><path d="M8 11V8a4 4 0 0 1 8 0v3" /></svg>
            <div class="local-body">
              <p class="text">{{ step.text }}</p>
              <code class="code">{{ step.code }}</code>
              <p class="note">{{ step.note }}</p>
            </div>
          </div>
        </template>
        <template v-else>
          <p class="text">{{ step.text }}</p>
          <span class="arrow" aria-hidden="true" />
          <code class="code">{{ step.code }}</code>
        </template>
      </li>
    </ol>

    <p class="more">
      <a :href="withBase('/how-to/connect-an-application')">Connect an application</a>
    </p>
  </figure>
</template>

<style scoped>
.sequence {
  /* Horizontal distance from the card's content edge to each lane's center line. */
  --lane-inset: 40px;
  --arrow-color: var(--vp-c-brand-1);

  position: relative;
  margin: 0;
  padding: 24px 20px;
  border: 1px solid var(--vp-c-divider);
  border-radius: 16px;
  background-color: var(--vp-c-bg-soft);
}

figcaption {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin-bottom: 20px;
}

.title {
  color: var(--vp-c-text-1);
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
}

.subtitle {
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 20px;
}

/* The two participants. Their center lines run down the card behind the steps. */
.lanes {
  display: flex;
  justify-content: space-between;
}

.lane {
  width: calc(var(--lane-inset) * 2);
  padding: 4px 0;
  border: 1px solid var(--vp-c-divider);
  border-radius: 999px;
  background-color: var(--vp-c-bg);
  color: var(--vp-c-text-1);
  font-size: 13px;
  font-weight: 600;
  line-height: 18px;
  text-align: center;
}

.lane-authora {
  border-color: var(--vp-c-brand-1);
  color: var(--vp-c-brand-1);
}

.steps {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin: 0;
  padding: 12px 0 4px;
  list-style: none;
}

.steps::before,
.steps::after {
  content: '';
  position: absolute;
  top: 0;
  bottom: 0;
  width: 0;
  border-left: 1px dashed var(--vp-c-divider);
}

.steps::before {
  left: var(--lane-inset);
}

.steps::after {
  right: var(--lane-inset);
}

.step {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 8px calc(var(--lane-inset) + 12px);
  text-align: center;
}

.text {
  margin: 0;
  color: var(--vp-c-text-1);
  font-size: 14px;
  font-weight: 500;
  line-height: 20px;
  text-wrap: balance;
}

.code {
  color: var(--vp-c-text-2);
  font-family: var(--vp-font-family-mono);
  font-size: 13px;
  line-height: 18px;
  overflow-wrap: anywhere;
}

/* The arrow spans from lane to lane; its head points at the receiving participant. */
.arrow {
  position: relative;
  align-self: stretch;
  height: 10px;
  margin: 0 calc(-12px - 0.5px);
}

.arrow::before {
  content: '';
  position: absolute;
  top: 4px;
  right: 0;
  left: 0;
  height: 2px;
  border-radius: 1px;
  background-color: var(--arrow-color);
}

.arrow::after {
  content: '';
  position: absolute;
  top: 0;
  border-top: 5px solid transparent;
  border-bottom: 5px solid transparent;
}

.to-authora .arrow::after {
  right: -1px;
  border-left: 8px solid var(--arrow-color);
}

.to-app .arrow::after {
  left: -1px;
  border-right: 8px solid var(--arrow-color);
}

/* The traveling dot: decoration only, it never carries information. */
@media (prefers-reduced-motion: no-preference) {
  .step:not(.at-authora) .arrow {
    background: radial-gradient(circle, var(--vp-c-brand-1) 0 3.5px, transparent 4.5px) no-repeat;
    background-size: 10px 10px;
    animation: travel 0.7s ease-in-out both;
    animation-delay: calc(0.4s + var(--step) * 0.6s);
  }

  .to-authora .arrow {
    --from: 0%;
    --to: 100%;
  }

  .to-app .arrow {
    --from: 100%;
    --to: 0%;
  }

  @keyframes travel {
    0% {
      background-position: var(--from) 0;
      background-size: 0 0;
    }
    10% {
      background-size: 10px 10px;
    }
    90% {
      background-size: 10px 10px;
    }
    100% {
      background-position: var(--to) 0;
      background-size: 0 0;
    }
  }
}

/* Signing in happens inside Authora: a box on the Authora lane, not an arrow. */
.at-authora {
  align-items: flex-end;
  padding: 8px 0;
}

.local {
  display: flex;
  gap: 10px;
  max-width: calc(100% - var(--lane-inset) - 12px);
  padding: 12px 14px;
  border: 1px solid var(--authora-c-safe);
  border-radius: 12px;
  background-color: var(--authora-c-safe-soft);
  text-align: left;
}

.lock {
  flex: none;
  width: 18px;
  height: 18px;
  margin-top: 1px;
  color: var(--authora-c-safe);
}

.local-body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.note {
  margin: 4px 0 0;
  color: var(--authora-c-safe-text);
  font-size: 13px;
  font-weight: 500;
  line-height: 18px;
}

.more {
  margin: 16px 0 0;
  font-size: 14px;
  line-height: 20px;
}

.more a {
  color: var(--vp-c-brand-1);
  font-weight: 500;
  text-decoration: underline;
  text-underline-offset: 2px;
}

@media (min-width: 640px) {
  .sequence {
    --lane-inset: 48px;
    padding: 28px;
  }
}
</style>
