<script setup lang="ts">
const props = defineProps<{ images: { url: string; alt: string | null }[]; name: string }>()
const current = ref(0)
const main = computed(() => props.images[current.value] ?? props.images[0])
</script>

<template>
  <div class="gallery">
    <img v-if="main" :src="main.url" :alt="main.alt || name" width="900" height="675" class="main">
    <span v-else class="main blank" />
    <div v-if="images.length > 1" class="thumbs" role="tablist">
      <button
        v-for="(img, i) in images" :key="img.url" type="button" role="tab"
        :aria-selected="i === current" :class="{ on: i === current }" @click="current = i"
      >
        <img :src="img.url" :alt="img.alt || `${name} ${i + 1}`" width="120" height="90" loading="lazy">
      </button>
    </div>
  </div>
</template>

<style scoped>
.main { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 12px; background: var(--surface); }
.thumbs { display: flex; gap: 8px; margin-top: 10px; overflow-x: auto; padding-bottom: 4px; }
.thumbs button { flex: 0 0 72px; padding: 0; border: 2px solid transparent; border-radius: 8px; background: none; cursor: pointer; }
.thumbs button.on { border-color: var(--green); }
.thumbs button:focus-visible { outline: 2px solid var(--green); outline-offset: 2px; }
.thumbs img { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 6px; background: var(--surface); }
</style>
