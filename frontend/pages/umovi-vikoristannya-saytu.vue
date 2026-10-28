<script setup lang="ts">
const { t } = useI18n()
const block = await useContentPage('page.terms', t('pages.termsTitle'))
</script>

<template>
  <ContentPage :block="block" :title="$t('pages.termsTitle')" />
</template>
