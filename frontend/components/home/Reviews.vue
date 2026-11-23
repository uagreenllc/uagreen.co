<script setup lang="ts">
import type { Review } from '~/types/api'

defineProps<{ reviews: Review[] }>()
</script>

<template>
  <section v-if="reviews.length" class="section">
    <div class="section-inner">
      <h2>{{ $t('home.reviews.h') }}</h2>
      <ul class="reviews">
        <li v-for="(r, i) in reviews" :key="i">
          <figure>
            <img v-if="r.photo" :src="r.photo" :alt="r.author" width="640" height="480" loading="lazy">
            <blockquote>{{ r.text }}</blockquote>
            <figcaption>{{ r.author }}</figcaption>
          </figure>
        </li>
      </ul>
    </div>
  </section>
</template>

<style scoped>
.reviews { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 340px), 1fr)); gap: 32px; }
figure { margin: 0; }
img { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 10px; background: var(--surface); margin-bottom: 16px; }
blockquote { margin: 0 0 12px; font-size: 17px; line-height: 1.55; color: var(--ink); }
figcaption { color: var(--ink-2); font-weight: 500; }
</style>
