<script setup lang="ts">
import type { ContentBlock } from '~/types/api'

// Title + trusted HTML from our own back office; the slot goes between them (forms, contacts).
defineProps<{ block: ContentBlock; title: string }>()
</script>

<template>
  <article class="page section-inner">
    <h1>{{ block.title || title }}</h1>
    <slot />
    <div class="prose" v-html="block.bodyHtml" />
  </article>
</template>
