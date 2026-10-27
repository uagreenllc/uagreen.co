<script setup lang="ts">
import type { PostCard } from '~/types/api'

// One blog row: cover at a fixed width, date, title and (on the list page) the excerpt.
defineProps<{ post: PostCard; withExcerpt?: boolean }>()
const localePath = useLocalePath()
</script>

<template>
  <li class="row">
    <NuxtLink :to="localePath(`/blog/${post.slug}`)">
      <img v-if="post.cover" :src="post.cover" :alt="post.title" width="440" height="330" loading="lazy">
      <span v-else class="blank" />
      <span>
        <time :datetime="post.publishedAt">{{ formatDate(post.publishedAt) }}</time>
        <span class="title">{{ post.title }}</span>
        <span v-if="withExcerpt && post.excerpt" class="excerpt">{{ post.excerpt }}</span>
      </span>
    </NuxtLink>
  </li>
</template>

<style scoped>
.row { border-top: 1px solid var(--line); }
.row:last-child { border-bottom: 1px solid var(--line); }
.row a { display: grid; grid-template-columns: 220px 1fr; gap: 28px; align-items: center; padding: 20px 0; text-decoration: none; }
.row img, .blank { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 8px; background: var(--surface); }
time { display: block; font-size: 13px; color: var(--ink-2); margin-bottom: 6px; }
.title { display: block; font-weight: 500; font-size: clamp(18px, 1.8vw, 22px); line-height: 1.3; text-wrap: balance; max-width: 36ch; }
.excerpt { display: block; margin-top: 8px; color: var(--ink-2); font-size: 15px; max-width: 60ch; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
@media (max-width: 900px) {
  .row a { grid-template-columns: 112px 1fr; gap: 16px; padding: 14px 0; }
  .title { font-size: 16px; }
  .excerpt { display: none; }
}
</style>
