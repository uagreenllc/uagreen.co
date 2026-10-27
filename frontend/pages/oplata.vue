<script setup lang="ts">
const { t } = useI18n()
const block = await useContentPage('page.oplata', t('pages.paymentTitle'))
</script>

<template>
  <ContentPage :block="block" :title="$t('pages.paymentTitle')" />
</template>
