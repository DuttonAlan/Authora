import { h } from 'vue'
import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import HeroStatus from './HeroStatus.vue'
import Mermaid from './Mermaid.vue'
import SignInFlow from './SignInFlow.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  Layout() {
    return h(DefaultTheme.Layout, null, {
      'home-hero-actions-after': () => h(HeroStatus),
      'home-hero-image': () => h(SignInFlow),
    })
  },
  enhanceApp({ app }) {
    app.component('Mermaid', Mermaid)
  },
} satisfies Theme
