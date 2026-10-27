<script setup lang="ts">
import type { Vacancy } from '~/types/api'

const route = useRoute()
const localePath = useLocalePath()
const slug = String(route.params.slug)

const { data } = await useApiFetch<Vacancy>(`vacancy:${slug}`, `/vacancies/${slug}`)
// A closed vacancy is the common "not found" here (old job-board links): send people to the open ones.
if (!data.value) await navigateTo(localePath('/vacancies'), { redirectCode: 301 })
const v = data.value!

const facts = computed(() => [
  ['vacancies.department', v.department],
  ['vacancies.employment', v.employmentType],
  ['vacancies.city', v.city],
  ['vacancies.experience', v.experience],
  ['vacancies.language', v.language],
].filter(([, val]) => val) as [string, string][])

const sections = computed(() => [
  ['vacancies.requirements', v.requirementsHtml],
  ['vacancies.niceToHave', v.niceToHaveHtml],
  ['vacancies.conditions', v.conditionsHtml],
].filter(([, html]) => html) as [string, string][])

usePageHead({ title: v.title, description: excerpt(v.requirementsHtml) })
</script>

<template>
  <article class="page section-inner vacancy">
    <p class="back"><NuxtLink class="link" :to="localePath('/vacancies')">{{ $t('vacancies.all') }}</NuxtLink></p>
    <h1>{{ v.title }}</h1>
    <dl class="facts">
      <div v-for="[key, val] in facts" :key="key">
        <dt>{{ $t(key) }}</dt>
        <dd>{{ val }}</dd>
      </div>
    </dl>
    <section v-for="[key, html] in sections" :key="key">
      <h2>{{ $t(key) }}</h2>
      <div class="prose" v-html="html" />
    </section>
    <ApplicationForm :slug="v.slug" />
  </article>
</template>

<style scoped>
.vacancy { max-width: 860px; }
.back { margin: 0 0 16px; }
.facts { display: flex; flex-wrap: wrap; gap: 16px 40px; margin: 0 0 48px; }
dt { font-size: 13px; color: var(--ink-2); margin-bottom: 2px; }
dd { margin: 0; font-size: 16px; }
section h2 { font-size: 24px; margin: 32px 0 12px; }
</style>
