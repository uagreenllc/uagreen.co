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
            <img v-if="p.cover" :src="p.cover" :alt="p.title" width="800" height="600" loading="lazy">
            <time :datetime="p.publishedAt">{{ formatDate(p.publishedAt) }}</time>
            <span class="title">{{ p.title }}</span>
          </NuxtLink>
        </li>
      </ul>
      <p class="all"><NuxtLink class="link" :to="localePath('/blog')">{{ $t('home.posts.all') }}</NuxtLink></p>
    </div>
  </section>
</template>

<style scoped>
.posts { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 32px; }
.posts a { display: block; text-decoration: none; }
.posts img { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 10px; background: var(--surface); margin-bottom: 12px; }
time { display: block; font-size: 13px; color: var(--ink-2); margin-bottom: 4px; }
.title { display: block; font-weight: 500; font-size: 18px; line-height: 1.3; text-wrap: balance; }
.all { margin: 40px 0 0; }
@media (max-width: 900px) { .posts { grid-template-columns: 1fr; gap: 24px; } }
</style>
