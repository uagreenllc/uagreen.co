<script setup lang="ts">
const route = useRoute()
const slug = String(route.params.slug)
const { categories, products, current } = await useCatalog(slug)
usePageHead({ title: current!.name, description: `${current!.name} — UA GREEN`, image: current!.image })
</script>

<template>
  <div class="page section-inner">
    <h1>{{ current!.name }}</h1>
    <CategoryNav :categories="categories" :active="slug" />
    <ProductList v-if="products.length" :products="products" />
    <p v-else class="empty">{{ $t('shop.empty') }}</p>
  </div>
</template>

<style scoped>
.empty { color: var(--ink-2); }
</style>
