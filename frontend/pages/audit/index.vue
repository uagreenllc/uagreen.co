<script setup lang="ts">
import type { Product } from '~/types/api'

const localePath = useLocalePath()
const money = useMoney()
const { t } = useI18n()
const { data } = await useApiFetch<Product>('audit-product', '/catalog/products/audit-vitrovih-resursiv')
if (!data.value) throw createError({ statusCode: 404 })
const p = data.value
usePageHead({ title: t('audit.title'), description: p.shortDesc ?? undefined })
</script>

<template>
  <article class="page section-inner audit">
    <h1>{{ $t('audit.title') }}</h1>
    <p v-if="p.shortDesc" class="lead">{{ p.shortDesc }}</p>
    <p class="price">
      <strong>{{ money(p.price) }}</strong>
      <s v-if="p.oldPrice">{{ money(p.oldPrice) }}</s>
    </p>

    <!-- The choice sits above the long description: people who got here from the story already want to act. -->
    <section class="choices" :aria-label="$t('audit.choose')">
      <div>
        <h2>{{ $t('audit.consultTitle') }}</h2>
        <p>{{ $t('audit.consultText') }}</p>
        <NuxtLink class="btn btn-outline" :to="localePath('/audit/consultation')">{{ $t('audit.consultTitle') }}</NuxtLink>
      </div>
      <div>
        <h2>{{ $t('audit.bookTitle') }}</h2>
        <p>{{ $t('audit.bookText') }}</p>
        <NuxtLink class="btn" :to="localePath('/audit/book')">{{ $t('audit.bookTitle') }}</NuxtLink>
      </div>
    </section>

    <div v-if="p.descriptionHtml" class="prose" v-html="p.descriptionHtml" />
  </article>
</template>

<style scoped>
.lead { font-size: 20px; color: var(--ink-2); max-width: 56ch; margin: -12px 0 16px; }
.price { display: flex; align-items: baseline; gap: 12px; margin: 0 0 40px; }
.price strong { font-size: 30px; font-weight: 600; }
.price s { color: var(--metal); font-size: 18px; }
.choices { display: grid; grid-template-columns: 1fr 1fr; border-top: 1px solid var(--line); border-bottom: 1px solid var(--line); margin: 0 0 56px; }
.choices > div { padding: 32px 40px 36px 0; display: flex; flex-direction: column; align-items: flex-start; }
.choices > div + div { padding-left: 40px; padding-right: 0; border-left: 1px solid var(--line); }
.choices h2 { font-size: 24px; margin: 0 0 10px; }
.choices p { color: var(--ink-2); margin: 0 0 24px; max-width: 40ch; flex: 1; }
.btn-outline { background: transparent; color: var(--green); box-shadow: inset 0 0 0 1.5px var(--green); }
.btn-outline:hover { background: var(--green); color: #fff; }
@media (max-width: 900px) {
  .choices { grid-template-columns: 1fr; }
  .choices > div, .choices > div + div { padding: 28px 0; border-left: 0; }
  .choices > div + div { border-top: 1px solid var(--line); }
}
</style>
