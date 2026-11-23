<script setup lang="ts">
const { t } = useI18n()
const block = await useContentPage('page.privacy', t('pages.privacyTitle'))
</script>

<template>
  <ContentPage :block="block" :title="$t('pages.privacyTitle')" />
</template>
