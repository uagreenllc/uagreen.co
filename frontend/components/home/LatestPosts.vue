<script setup lang="ts">
import type { PostCard } from '~/types/api'

defineProps<{ posts: PostCard[] }>()
const localePath = useLocalePath()
</script>

<template>
  <section v-if="posts.length" class="section">
    <div class="section-inner">
      <h2>{{ $t('home.posts.h') }}</h2>
      <ul class="posts">
        <li v-for="p in posts" :key="p.slug">
          <NuxtLink :to="localePath(`/blog/${p.slug}`)">
            <img v-if="p.cover" :src="p.cover" :alt="p.title" width="440" height="330" loading="lazy">
            <span>
              <time :datetime="p.publishedAt">{{ formatDate(p.publishedAt) }}</time>
              <span class="title">{{ p.title }}</span>
            </span>
          </NuxtLink>
        </li>
      </ul>
      <p class="all"><NuxtLink class="link" :to="localePath('/blog')">{{ $t('home.posts.all') }}</NuxtLink></p>
    </div>
  </section>
</template>

<style scoped>
/* Rows, not a card grid: cover at a fixed width, date and title beside it. */
.posts { list-style: none; margin: 0; padding: 0; max-width: 860px; }
.posts li { border-top: 1px solid var(--line); }
.posts li:last-child { border-bottom: 1px solid var(--line); }
.posts a { display: grid; grid-template-columns: 220px 1fr; gap: 28px; align-items: center; padding: 20px 0; text-decoration: none; }
.posts img { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 8px; background: var(--surface); }
time { display: block; font-size: 13px; color: var(--ink-2); margin-bottom: 6px; }
.title { display: block; font-weight: 500; font-size: clamp(18px, 1.8vw, 22px); line-height: 1.3; text-wrap: balance; max-width: 36ch; }
.all { margin: 32px 0 0; }
@media (max-width: 900px) { .posts a { grid-template-columns: 112px 1fr; gap: 16px; padding: 14px 0; } .title { font-size: 16px; } }
</style>
