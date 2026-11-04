<script setup lang="ts">
// Renders a ```mermaid block in the browser, in the light or dark theme of the page.
import { onMounted, ref, useId, watch } from 'vue'
import { useData } from 'vitepress'

const props = defineProps<{ code: string }>()
const { isDark } = useData()
const svg = ref('')
const id = `mermaid-${useId()}`

async function render() {
  const { default: mermaid } = await import('mermaid')
  mermaid.initialize({ startOnLoad: false, theme: isDark.value ? 'dark' : 'default' })
  const result = await mermaid.render(id, decodeURIComponent(props.code))
  svg.value = result.svg
}

onMounted(render)
watch(isDark, render)
</script>

<template>
  <div class="mermaid-diagram" v-html="svg" />
</template>
