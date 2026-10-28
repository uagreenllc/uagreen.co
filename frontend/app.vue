<script setup lang="ts">
import { useCart } from '~/stores/cart'

const settings = useSettings()
const { data } = await useApiFetch<Record<string, string>>('settings', '/settings/public')
if (data.value) settings.value = data.value
useSiteHead()
useAnalytics()
// After hydration, so the server HTML and the first client render agree (the cart lives in localStorage).
onMounted(() => useCart().load())
</script>

<template>
  <NuxtLayout>
    <NuxtPage />
  </NuxtLayout>
</template>
