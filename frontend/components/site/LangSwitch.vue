<script setup lang="ts">
// Language dropdown: <details> keeps it keyboard-accessible and the options real links (crawlable, work without JS).
// JS only closes it: outside click, Escape, and after the language changes.
const switchLocalePath = useSwitchLocalePath()
const { locale, locales, t } = useI18n()
const el = ref<HTMLDetailsElement>()
const current = computed(() => locales.value.find(l => l.code === locale.value))

const close = () => { if (el.value) el.value.open = false }
const onDoc = (e: Event) => {
  if (e instanceof KeyboardEvent ? e.key === 'Escape' : !el.value?.contains(e.target as Node)) close()
}
onMounted(() => { document.addEventListener('click', onDoc); document.addEventListener('keydown', onDoc) })
onBeforeUnmount(() => { document.removeEventListener('click', onDoc); document.removeEventListener('keydown', onDoc) })
watch(locale, close)
</script>

<template>
  <details ref="el" class="lang-switch">
    <summary :aria-label="`${t('lang.choose')}: ${current?.name}`">
      <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.6">
        <circle cx="12" cy="12" r="9" /><path d="M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3Z" />
      </svg>
      <span>{{ $t(`lang.${locale}`) }}</span>
      <svg class="chev" viewBox="0 0 12 12" width="10" height="10" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.6">
        <path d="m2.5 4.5 3.5 3.5 3.5-3.5" />
      </svg>
    </summary>
    <ul>
      <li v-for="l in locales" :key="l.code">
        <NuxtLink :to="switchLocalePath(l.code)" :hreflang="l.language" :lang="l.code" :aria-current="l.code === locale ? 'true' : undefined" @click="close">
          {{ l.name }}
          <svg v-if="l.code === locale" viewBox="0 0 16 16" width="14" height="14" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2">
            <path d="m3 8.5 3 3 7-7" />
          </svg>
        </NuxtLink>
      </li>
    </ul>
  </details>
</template>

<style scoped>
.lang-switch { position: relative; }
summary {
  list-style: none; cursor: pointer; user-select: none;
  display: inline-flex; align-items: center; gap: 6px;
  padding: 7px 10px; border: 1px solid var(--line); border-radius: 999px;
  font-size: 14px; color: var(--ink-2);
  transition: border-color 0.15s, color 0.15s;
}
summary::-webkit-details-marker { display: none; }
summary:hover, .lang-switch[open] summary { border-color: var(--metal); color: var(--ink); }
summary:focus-visible { outline: 2px solid var(--green); outline-offset: 2px; }
.chev { transition: transform 0.15s var(--ease); }
.lang-switch[open] .chev { transform: rotate(180deg); }
ul {
  position: absolute; right: 0; top: calc(100% + 8px); z-index: 1;
  min-width: 180px; margin: 0; padding: 6px; list-style: none;
  background: #fff; border: 1px solid var(--line); border-radius: 12px;
  box-shadow: 0 12px 32px rgb(31 42 34 / 0.12);
}
a {
  display: flex; align-items: center; justify-content: space-between; gap: 12px;
  padding: 9px 12px; border-radius: 8px;
  color: var(--ink) !important; text-decoration: none; font-size: 15px;
}
a:hover, a:focus-visible { background: var(--surface); outline: none; }
a[aria-current] { color: var(--green) !important; font-weight: 600; }
</style>
