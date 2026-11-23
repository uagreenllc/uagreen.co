<script setup lang="ts">
import type { ContentBlock } from '~/types/api'

// Call-to-action for the "Оберіг" portal. Text: back-office block `oberig.<variant>`, else i18n.
// Switched off in the back office with setting `oberig.<variant>` = "0".
defineProps<{ variant: 'home' | 'product' | 'order'; block?: ContentBlock | null }>()
const oberig = useOberig()
</script>

<template>
  <section v-if="oberig.on(variant)" :class="variant === 'home' ? 'section wide' : 'card'">
    <div :class="{ 'section-inner': variant === 'home' }" class="inner">
      <div>
        <p class="eyebrow">{{ $t('oberig.eyebrow') }}</p>
        <h2>{{ block?.title || $t(`oberig.${variant}.h`) }}</h2>
      </div>
      <div>
        <div v-if="block?.bodyHtml" class="prose" v-html="block.bodyHtml" />
        <p v-else class="text">{{ $t(`oberig.${variant}.p`) }}</p>
        <p class="actions">
          <a v-if="variant === 'home'" class="btn" :href="oberig.calculatorUrl" target="_blank" rel="noopener">{{ $t('oberig.calc') }}</a>
          <a :class="variant === 'home' ? 'btn ghost' : 'btn'" :href="oberig.url" target="_blank" rel="noopener">{{ $t('oberig.portal') }}</a>
        </p>
        <p v-if="variant === 'home'" class="note">{{ $t('oberig.loginNote') }}</p>
      </div>
    </div>
  </section>
</template>

<style scoped>
.eyebrow { color: var(--green); font-size: 13px; font-weight: 600; letter-spacing: 0.08em; text-transform: uppercase; margin: 0 0 12px; }
.text { color: var(--ink-2); font-size: 17px; max-width: 68ch; margin: 0 0 1em; }
.actions { display: flex; flex-wrap: wrap; gap: 12px; margin: 24px 0 0; }
.btn.ghost { background: transparent; color: var(--green); box-shadow: inset 0 0 0 1.5px var(--green); }
.btn.ghost:hover { background: var(--green); color: #fff; }
.note { color: var(--ink-2); font-size: 14px; margin: 14px 0 0; }

/* Home: heading left, copy and buttons right */
.wide .inner { display: grid; grid-template-columns: minmax(0, 5fr) minmax(0, 7fr); gap: 56px; }

/* Product and order pages: one compact panel */
.card { background: var(--surface); border-radius: 10px; padding: 28px 32px; margin: 48px 0; max-width: 860px; }
.card h2 { font-size: 24px; margin-bottom: 10px; }
.card .text, .card .prose :deep(p) { font-size: 16px; }
.card .prose :deep(p:last-child) { margin-bottom: 0; }
.card .actions { margin-top: 18px; }

@media (max-width: 900px) {
  .wide .inner { grid-template-columns: 1fr; gap: 8px; }
  .card { padding: 24px 20px; }
}
</style>
