<script setup lang="ts">
// Renders a ```mermaid block in the browser, in the light or dark colors of the site.
// If a diagram cannot be parsed, its source is shown instead, because the build cannot catch that.
import { nextTick, onMounted, ref, useId, watch } from 'vue'
import { useData } from 'vitepress'

const props = defineProps<{ code: string }>()
const { isDark } = useData()
const root = ref<HTMLElement>()
const svg = ref('')
const failed = ref(false)
const id = `mermaid-${useId()}`
const source = decodeURIComponent(props.code)

const fontFamily = 'Inter, ui-sans-serif, system-ui, sans-serif'
const minimumTextSize = 11

const lightColors = {
  background: '#ffffff',
  primaryColor: '#eef1fb',
  primaryBorderColor: '#3451b2',
  primaryTextColor: '#3c3c43',
  secondaryColor: '#f6f6f7',
  tertiaryColor: '#f6f6f7',
  lineColor: '#67676c',
  edgeLabelBackground: '#ffffff',
  noteBkgColor: '#e6f4f1',
  noteBorderColor: '#0f766e',
  noteTextColor: '#3c3c43',
}

const darkColors = {
  darkMode: true,
  background: '#1b1b1f',
  primaryColor: '#262b3d',
  primaryBorderColor: '#a8b1ff',
  primaryTextColor: '#dfdfd6',
  secondaryColor: '#202127',
  tertiaryColor: '#202127',
  lineColor: '#98989f',
  edgeLabelBackground: '#1b1b1f',
  noteBkgColor: '#17302d',
  noteBorderColor: '#5eead4',
  noteTextColor: '#dfdfd6',
}

// A wide diagram scrolls in its container rather than shrinking below a readable text size.
function keepTextReadable() {
  const element = root.value?.querySelector('svg')
  const label = element?.querySelector('foreignObject p, foreignObject span, text')
  if (!element || !label) {
    return
  }

  const textSize = parseFloat(getComputedStyle(label).fontSize)
  element.style.minWidth = `${Math.ceil((element.viewBox.baseVal.width * minimumTextSize) / textSize)}px`
}

async function render() {
  try {
    const { default: mermaid } = await import('mermaid')
    // Labels are measured with this font. If it cannot be loaded, the diagram uses the fallback fonts.
    await document.fonts.load('16px Inter').catch(() => {})
    mermaid.initialize({
      startOnLoad: false,
      suppressErrorRendering: true,
      theme: 'base',
      fontFamily,
      themeVariables: { fontFamily, ...(isDark.value ? darkColors : lightColors) },
    })

    const result = await mermaid.render(id, source)
    svg.value = result.svg
    failed.value = false
    await nextTick()
    keepTextReadable()
  } catch (error) {
    console.error('The diagram could not be rendered.', error)
    failed.value = true
  }
}

onMounted(render)
watch(isDark, render)
</script>

<template>
  <div v-if="failed" class="mermaid-diagram mermaid-diagram-failed">
    <p>This diagram could not be drawn. Its source:</p>
    <pre><code>{{ source }}</code></pre>
  </div>
  <div v-else ref="root" class="mermaid-diagram" tabindex="0" v-html="svg" />
</template>
