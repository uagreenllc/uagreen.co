<script setup lang="ts">
const { t } = useI18n()
const block = await useContentPage('page.about', t('pages.aboutTitle'))
</script>

<template>
  <ContentPage :block="block" :title="$t('pages.aboutTitle')" />
</template>
