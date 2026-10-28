<script setup lang="ts">
import type { Category } from '~/types/api'

defineProps<{ categories: Category[]; active?: string }>()
const localePath = useLocalePath()
</script>

<template>
  <nav class="cats" aria-label="Categories">
    <NuxtLink :to="localePath('/shop')" :class="{ on: !active }">{{ $t('shop.all') }}</NuxtLink>
    <NuxtLink v-for="c in categories" :key="c.slug" :to="localePath(`/shop/cat/${c.slug}`)" :class="{ on: active === c.slug }">
      {{ c.name }} <small>{{ c.productCount }}</small>
    </NuxtLink>
  </nav>
</template>

<style scoped>
.cats { display: flex; flex-wrap: wrap; gap: 8px 24px; margin: 0 0 40px; border-bottom: 1px solid var(--line); padding-bottom: 16px; }
.cats a { color: var(--ink-2); text-decoration: none; font-size: 16px; padding: 4px 0; border-bottom: 2px solid transparent; }
.cats a:hover { color: var(--ink); }
.cats a.on { color: var(--green); border-color: var(--green); }
small { color: var(--metal); font-size: 13px; margin-left: 2px; }
</style>
