<script setup lang="ts">
import type { ContentBlock } from '~/types/api'

// A back-office text block (projects, testimonials, …): title + trusted HTML from our own API.
defineProps<{ block: ContentBlock | null }>()
</script>

<template>
  <section v-if="block" class="section">
    <div class="section-inner">
      <h2 v-if="block.title">{{ block.title }}</h2>
      <div class="prose" v-html="block.bodyHtml" />
    </div>
  </section>
</template>

<style scoped>
.prose :deep(img) { max-width: 720px; }
</style>
