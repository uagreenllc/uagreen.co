<script setup lang="ts">
const localePath = useLocalePath()
const { locale } = useI18n()
const settings = useSettings()

const year = new Date().getFullYear()
const address = computed(() => (locale.value === 'en' && settings.value?.address_en) || settings.value?.address || '')
const phones = computed(() => splitList(settings.value?.phones))
const emails = computed(() => splitList(settings.value?.emails))
const socials = computed(() =>
  (['instagram', 'tiktok', 'linkedin', 'youtube'] as const)
    .map(name => ({ name, url: settings.value?.[`socials.${name}`] }))
    .filter(s => s.url),
)
</script>

<template>
  <footer class="site-footer">
    <div class="cols">
      <div v-if="address">
        <h3>{{ $t('footer.address') }}</h3>
        <p>{{ address }}</p>
      </div>
      <div v-if="phones.length || emails.length">
        <h3>{{ $t('footer.phones') }}</h3>
        <p v-for="p in phones" :key="p"><a :href="`tel:${p}`">{{ formatPhone(p) }}</a></p>
        <h3>{{ $t('footer.emails') }}</h3>
        <p v-for="e in emails" :key="e"><a :href="`mailto:${e}`">{{ e }}</a></p>
      </div>
      <div>
        <p v-for="s in socials" :key="s.name"><a :href="s.url" rel="noopener" target="_blank">{{ s.name }}</a></p>
        <h3>{{ $t('footer.legal') }}</h3>
        <p><NuxtLink :to="localePath('/oplata')">{{ $t('footer.payment') }}</NuxtLink></p>
        <p><NuxtLink :to="localePath('/umovi-vikoristannya-saytu')">{{ $t('footer.terms') }}</NuxtLink></p>
      </div>
    </div>
    <p class="rights">{{ $t('footer.rights', { year }) }}</p>
  </footer>
</template>

<style scoped>
.site-footer { border-top: 1px solid var(--line); padding: 48px 40px 32px; color: var(--ink-2); font-size: 14px; }
.cols { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 32px; max-width: 1200px; margin: 0 auto; }
h3 { font-size: 13px; font-weight: 600; color: var(--ink); margin: 0 0 8px; }
h3 + p + h3 { margin-top: 20px; }
p { margin: 0 0 6px; }
a { text-decoration: none; }
a:hover { color: var(--green); }
.rights { max-width: 1200px; margin: 40px auto 0; }
@media (max-width: 900px) {
  .site-footer { padding: 40px 16px 24px; }
  .cols { grid-template-columns: 1fr; }
}
</style>
