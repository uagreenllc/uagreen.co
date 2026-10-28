<script setup lang="ts">
import { useCart } from '~/stores/cart'

// The count comes from localStorage, so it is client-only: the server renders the bare link.
const cart = useCart()
const localePath = useLocalePath()
</script>

<template>
  <NuxtLink :to="localePath('/cart')" class="cart-link">
    {{ $t('nav.cart') }}<ClientOnly><span v-if="cart.count" class="n">{{ cart.count }}</span></ClientOnly>
  </NuxtLink>
</template>

<style scoped>
.cart-link { display: inline-flex; align-items: center; gap: 6px; }
.n { min-width: 20px; height: 20px; padding: 0 6px; border-radius: 10px; background: var(--copper); color: #fff; font-size: 12px; font-weight: 600; display: inline-grid; place-items: center; }
</style>
