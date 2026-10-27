<script setup lang="ts">
import type { ContentBlock, Vacancy } from '~/types/api'

const localePath = useLocalePath()
const { t } = useI18n()

const [{ data: intro }, { data }] = await Promise.all([
  useApiFetch<ContentBlock>('content:page.vacancies', '/content/page.vacancies'),
  useApiFetch<Vacancy[]>('vacancies', '/vacancies'),
])
const list = computed(() => data.value ?? [])

usePageHead({ title: t('vacancies.title'), description: excerpt(intro.value?.bodyHtml) })
</script>

<template>
  <div class="page section-inner">
    <h1>{{ $t('vacancies.title') }}</h1>
    <ul v-if="list.length" class="list">
      <li v-for="v in list" :key="v.slug">
        <NuxtLink :to="localePath(`/vacancies/${v.slug}`)">
          <span class="title">{{ v.title }}</span>
          <span class="meta">{{ [v.department, v.city, v.employmentType].filter(Boolean).join(' · ') }}</span>
        </NuxtLink>
      </li>
    </ul>
    <p v-else class="empty">{{ $t('vacancies.empty') }}</p>
    <section v-if="intro" class="intro">
      <h2 v-if="intro.title">{{ intro.title }}</h2>
      <div class="prose" v-html="intro.bodyHtml" />
    </section>
  </div>
</template>

<style scoped>
.list { list-style: none; margin: 0; padding: 0; max-width: 860px; }
.list li { border-top: 1px solid var(--line); }
.list li:last-child { border-bottom: 1px solid var(--line); }
.list a { display: flex; justify-content: space-between; align-items: baseline; gap: 24px; padding: 20px 0; text-decoration: none; }
.list a:hover .title { color: var(--green); }
.title { font-weight: 500; font-size: clamp(18px, 1.8vw, 22px); line-height: 1.3; }
.meta { color: var(--ink-2); font-size: 14px; text-align: right; flex-shrink: 0; }
.empty { color: var(--ink-2); }
.intro { margin-top: 72px; max-width: 860px; }
.intro h2 { font-size: 26px; }
@media (max-width: 700px) { .list a { flex-direction: column; gap: 4px; } .meta { text-align: left; } }
</style>
