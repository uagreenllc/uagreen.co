<script setup lang="ts">
const { t } = useI18n()
const block = await useContentPage('page.offer', t('pages.offerTitle'))
</script>

<template>
  <ContentPage :block="block" :title="$t('pages.offerTitle')" />
</template>
