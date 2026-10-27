<script setup lang="ts">
const localePath = useLocalePath()
const settings = useSettings()
const phone = computed(() => splitList(settings.value?.phones)[0] ?? '')
const email = computed(() => splitList(settings.value?.emails)[0] ?? '')
</script>

<template>
  <section class="section contact">
    <div class="section-inner">
      <h2>{{ $t('home.contact.h') }}</h2>
      <p>{{ $t('home.contact.p') }}</p>
      <p class="ways">
        <a v-if="phone" :href="`tel:${phone}`">{{ formatPhone(phone) }}</a>
        <a v-if="email" :href="`mailto:${email}`">{{ email }}</a>
      </p>
      <NuxtLink class="btn" :to="localePath('/contacts')">{{ $t('home.contact.btn') }}</NuxtLink>
    </div>
  </section>
</template>

<style scoped>
.contact { border-top: 1px solid var(--line); }
.contact p { color: var(--ink-2); font-size: 18px; max-width: 52ch; margin: 0 0 20px; }
.ways { display: flex; flex-wrap: wrap; gap: 8px 28px; font-size: 20px; margin-bottom: 28px !important; }
.ways a { color: var(--ink); text-decoration: none; }
.ways a:hover { color: var(--green); }
</style>
