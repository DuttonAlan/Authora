import DefaultTheme from 'vitepress/theme'
import type { Theme } from 'vitepress'
import Mermaid from './Mermaid.vue'
import HomeAudiences from './home/HomeAudiences.vue'
import HomeDocs from './home/HomeDocs.vue'
import HomeHero from './home/HomeHero.vue'
import HomeScope from './home/HomeScope.vue'
import HomeStandards from './home/HomeStandards.vue'
import './custom.css'
import './home/home.css'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component('Mermaid', Mermaid)
    // The homepage (index.md) is built from these sections.
    app.component('HomeHero', HomeHero)
    app.component('HomeAudiences', HomeAudiences)
    app.component('HomeStandards', HomeStandards)
    app.component('HomeScope', HomeScope)
    app.component('HomeDocs', HomeDocs)
  },
} satisfies Theme
