<script setup lang="ts">
const { t } = useI18n()
const block = await useContentPage('page.contacts', t('pages.contactsTitle'))
const settings = useSettings()
const phones = computed(() => splitList(settings.value?.phones))
const emails = computed(() => splitList(settings.value?.emails))
</script>

<template>
  <ContentPage :block="block" :title="$t('pages.contactsTitle')">
    <div class="ways">
      <dl>
        <template v-if="settings?.address">
          <dt>{{ $t('footer.address') }}</dt>
          <dd>{{ settings.address }}</dd>
        </template>
        <template v-if="phones.length">
          <dt>{{ $t('footer.phones') }}</dt>
          <dd><a v-for="p in phones" :key="p" :href="`tel:${p}`">{{ formatPhone(p) }}</a></dd>
        </template>
        <template v-if="emails.length">
          <dt>{{ $t('footer.emails') }}</dt>
          <dd><a v-for="e in emails" :key="e" :href="`mailto:${e}`">{{ e }}</a></dd>
        </template>
      </dl>
      <div>
        <h2>{{ $t('pages.write') }}</h2>
        <ContactForm />
      </div>
    </div>
  </ContentPage>
</template>

<style scoped>
.ways { display: grid; grid-template-columns: 1fr 1.3fr; gap: 48px; margin: 0 0 64px; }
dl { margin: 0; font-size: 18px; }
dt { font-size: 13px; color: var(--ink-2); margin: 0 0 4px; }
dd { margin: 0 0 24px; display: grid; gap: 4px; }
dd a { color: var(--ink); text-decoration: none; }
dd a:hover { color: var(--green); }
h2 { font-size: 24px; margin-bottom: 20px; }
@media (max-width: 900px) { .ways { grid-template-columns: 1fr; gap: 24px; } }
</style>
