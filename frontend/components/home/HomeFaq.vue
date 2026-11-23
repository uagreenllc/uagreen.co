<script setup lang="ts">
// Plain-text answers to what buyers (and Google's AI answers) ask about a 5 kW turbine, mirrored as FAQPage.
const props = defineProps<{ price: string }>()
const { tm, rt } = useI18n()
const items = computed(() => (tm('home.faq.items') as { q: string; a: string }[]).map(i => ({ q: rt(i.q), a: rt(i.a, { price: props.price }) })))
useHead(() => ({
  script: [{
    key: 'faq-ld',
    type: 'application/ld+json',
    innerHTML: JSON.stringify({
      '@context': 'https://schema.org',
      '@type': 'FAQPage',
      mainEntity: items.value.map(i => ({ '@type': 'Question', name: i.q, acceptedAnswer: { '@type': 'Answer', text: i.a } })),
    }).replace(/</g, '\\u003c'),
  }],
}))
</script>

<template>
  <section class="section faq">
    <div class="section-inner">
      <h2>{{ $t('home.faq.h') }}</h2>
      <details v-for="(i, n) in items" :key="n">
        <summary><h3>{{ i.q }}</h3></summary>
        <p>{{ i.a }}</p>
      </details>
    </div>
  </section>
</template>

<style scoped>
.faq { border-top: 1px solid var(--line); }
details { max-width: 860px; border-bottom: 1px solid var(--line); }
summary { cursor: pointer; padding: 20px 0; list-style: none; display: flex; justify-content: space-between; gap: 24px; }
summary::-webkit-details-marker { display: none; }
summary::after { content: '+'; color: var(--green); font-size: 24px; line-height: 1; }
details[open] summary::after { content: '−'; }
h3 { font-size: 19px; font-weight: 500; margin: 0; }
p { color: var(--ink-2); font-size: 17px; max-width: 68ch; margin: 0 0 22px; }
</style>
