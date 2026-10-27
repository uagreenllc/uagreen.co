<script setup lang="ts">
const { t } = useI18n()
const { categories, products } = await useCatalog()
usePageHead({ title: t('shop.title'), description: t('shop.description') })
</script>

<template>
  <div class="page section-inner">
    <h1>{{ $t('shop.title') }}</h1>
    <CategoryNav :categories="categories" />
    <ProductList v-if="products.length" :products="products" />
    <p v-else class="empty">{{ $t('shop.empty') }}</p>
  </div>
</template>

<style scoped>
.empty { color: var(--ink-2); }
</style>
