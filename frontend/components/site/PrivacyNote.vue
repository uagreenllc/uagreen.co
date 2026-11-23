<script setup lang="ts">
const localePath = useLocalePath()
</script>

<template>
  <i18n-t keypath="form.privacyNote" tag="p" class="privacy-note" scope="global">
    <template #link><NuxtLink :to="localePath('/polityka-konfidentsiynosti')">{{ $t('form.privacyLink') }}</NuxtLink></template>
  </i18n-t>
</template>

<style scoped>
.privacy-note { font-size: 13px; color: var(--ink-2); margin: 0 0 16px; }
.privacy-note a { color: inherit; }
</style>
