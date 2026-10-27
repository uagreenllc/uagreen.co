<script setup lang="ts">
import type { Page, PostCard } from '~/types/api'

const route = useRoute()
const localePath = useLocalePath()
const { t } = useI18n()
const PAGE_SIZE = 12
const page = computed(() => Math.max(1, Number(route.query.page) || 1))

const { data } = await useApiFetch<Page<PostCard>>('blog', '/blog', { page, pageSize: PAGE_SIZE })
const posts = computed(() => data.value?.items ?? [])
const pages = computed(() => Math.ceil((data.value?.total ?? 0) / PAGE_SIZE))
const pageLink = (n: number) => localePath({ path: '/blog', query: n > 1 ? { page: n } : {} })

usePageHead({ title: t('blog.title') })
</script>

<template>
  <div class="page section-inner">
    <h1>{{ $t('blog.title') }}</h1>
    <ul v-if="posts.length" class="posts">
      <PostRow v-for="p in posts" :key="p.slug" :post="p" with-excerpt />
    </ul>
    <p v-else class="empty">{{ $t('blog.empty') }}</p>
    <nav v-if="pages > 1" class="pager" aria-label="Pagination">
      <NuxtLink v-if="page > 1" class="link" :to="pageLink(page - 1)">{{ $t('blog.prev') }}</NuxtLink>
      <span>{{ page }} / {{ pages }}</span>
      <NuxtLink v-if="page < pages" class="link" :to="pageLink(page + 1)">{{ $t('blog.next') }}</NuxtLink>
    </nav>
  </div>
</template>

<style scoped>
.posts { list-style: none; margin: 0; padding: 0; max-width: 860px; }
.empty { color: var(--ink-2); }
.pager { display: flex; gap: 24px; align-items: center; margin-top: 32px; color: var(--ink-2); }
</style>
