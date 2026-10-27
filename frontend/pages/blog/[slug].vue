<script setup lang="ts">
import type { Post } from '~/types/api'

const route = useRoute()
const localePath = useLocalePath()
const slug = String(route.params.slug)

const { data: post } = await useApiFetch<Post>(`post:${slug}`, `/blog/${slug}`)
if (!post.value) throw createError({ statusCode: 404 })
const p = post.value

usePageHead({ title: p.seoTitle || p.title, description: p.seoDesc || p.excerpt || excerpt(p.bodyHtml), image: p.cover })

const config = useRuntimeConfig()
const ld = {
  '@context': 'https://schema.org',
  '@type': 'BlogPosting',
  headline: p.title,
  datePublished: p.publishedAt,
  dateModified: p.updatedAt,
  author: p.author ? { '@type': 'Person', name: p.author } : { '@type': 'Organization', name: 'UA GREEN' },
  image: p.cover ? new URL(p.cover, config.public.siteUrl).toString() : undefined,
  mainEntityOfPage: new URL(route.path, config.public.siteUrl).toString(),
}
useHead({ script: [{ type: 'application/ld+json', innerHTML: JSON.stringify(ld).replace(/</g, '\\u003c') }] })
</script>

<template>
  <article class="page section-inner post">
    <p class="meta">
      <time :datetime="p.publishedAt">{{ formatDate(p.publishedAt) }}</time>
      <span v-if="p.author">· {{ p.author }}</span>
    </p>
    <h1>{{ p.title }}</h1>
    <img v-if="p.cover" :src="p.cover" :alt="p.title" width="1200" height="675" class="cover">
    <div class="prose" v-html="p.bodyHtml" />
    <p class="back"><NuxtLink class="link" :to="localePath('/blog')">{{ $t('blog.back') }}</NuxtLink></p>
  </article>
</template>

<style scoped>
.post { max-width: 860px; }
.meta { color: var(--ink-2); font-size: 14px; margin: 0 0 12px; display: flex; gap: 8px; }
.post h1 { max-width: 30ch; }
.cover { width: 100%; aspect-ratio: 16 / 9; object-fit: cover; border-radius: 12px; margin: 0 0 40px; background: var(--surface); }
.back { margin-top: 48px; }
</style>
