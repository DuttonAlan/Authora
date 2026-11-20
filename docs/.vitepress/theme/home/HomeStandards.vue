<script setup lang="ts">
// The specifications Authora implements and the conformance test plans that run on every pull request.
// Keep the numbers in sync with docs/reference/standards.md.
import { withBase } from 'vitepress'

const plans = [
  { name: 'Basic OP', modules: 35 },
  { name: 'RP-Initiated Logout OP', modules: 11 },
]

const specifications = [
  { id: 'OAuth 2.1', name: 'Authorization framework' },
  { id: 'RFC 7636', name: 'PKCE' },
  { id: 'RFC 7009', name: 'Token Revocation' },
  { id: 'RFC 7662', name: 'Token Introspection' },
  { id: 'RFC 9207', name: 'Issuer Identification' },
  { id: 'RFC 8414', name: 'Server Metadata' },
  { id: 'RFC 7517', name: 'JSON Web Key' },
  { id: 'RFC 9457', name: 'Problem Details' },
  { id: 'OIDC Core 1.0', name: 'ID Tokens and UserInfo' },
  { id: 'OIDC Discovery 1.0', name: 'openid-configuration' },
  { id: 'OIDC RP-Initiated Logout 1.0', name: 'Sign-out from apps' },
]
</script>

<template>
  <section class="home-section" aria-labelledby="standards-heading">
    <div class="home-container standards-grid">
      <div class="conformance">
        <h2 id="standards-heading" class="home-heading">Tested against the specifications</h2>
        <p class="home-subheading">
          Every pull request runs the OpenID Foundation's conformance suite against the container. A result that
          changes fails the build.
        </p>

        <ul class="plans" role="list">
          <li v-for="plan in plans" :key="plan.name" class="plan">
            <svg class="check" aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.25" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9" /><path d="m8 12.5 2.5 2.5L16 9.5" /></svg>
            <div>
              <p class="plan-name">{{ plan.name }} test plan</p>
              <p class="plan-result">{{ plan.modules }} test modules, none failing</p>
            </div>
          </li>
        </ul>

        <p class="disclaimer">Passing these tests is not a certification. Authora is not certified by the OpenID Foundation.</p>
        <a class="home-link" :href="withBase('/reference/standards')">Standards and limitations</a>
      </div>

      <div class="specs-wrap">
        <h3 class="specs-heading">Implemented specifications</h3>
        <dl class="specs">
          <div v-for="spec in specifications" :key="spec.id" class="spec">
            <dt>{{ spec.id }}</dt>
            <dd>{{ spec.name }}</dd>
          </div>
        </dl>
      </div>
    </div>
  </section>
</template>

<style scoped>
.standards-grid {
  display: grid;
  gap: 48px;
}

.conformance,
.specs-wrap {
  min-width: 0;
}

.plans {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin: 32px 0 0;
  padding: 0;
  list-style: none;
}

.plan {
  display: flex;
  gap: 12px;
  align-items: flex-start;
}

.check {
  flex: none;
  width: 24px;
  height: 24px;
  color: var(--authora-c-safe);
}

.plan-name {
  margin: 0;
  color: var(--vp-c-text-1);
  font-size: 16px;
  font-weight: 600;
  line-height: 24px;
}

.plan-result {
  margin: 0;
  color: var(--vp-c-text-2);
  font-size: 15px;
  font-variant-numeric: tabular-nums;
  line-height: 22px;
}

.disclaimer {
  max-width: 34rem;
  margin: 24px 0 16px;
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 22px;
}

.specs-heading {
  margin: 0 0 16px;
  color: var(--vp-c-text-2);
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.04em;
  line-height: 20px;
  text-transform: uppercase;
}

.specs {
  display: grid;
  margin: 0;
  border-top: 1px solid var(--vp-c-divider);
}

.spec {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 16px;
  justify-content: space-between;
  padding: 10px 0;
  border-bottom: 1px solid var(--vp-c-divider);
}

dt {
  color: var(--vp-c-text-1);
  font-family: var(--vp-font-family-mono);
  font-size: 14px;
  font-weight: 500;
  line-height: 22px;
}

dd {
  margin: 0;
  color: var(--vp-c-text-2);
  font-size: 14px;
  line-height: 22px;
}

@media (min-width: 960px) {
  .standards-grid {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: 64px;
    align-items: start;
  }
}
</style>
