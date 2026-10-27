<script setup lang="ts">
const settings = useSettings()
const { data } = await useApiFetch<Record<string, string>>('settings', '/settings/public')
if (data.value) settings.value = data.value
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
