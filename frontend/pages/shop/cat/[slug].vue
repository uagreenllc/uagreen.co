<script setup lang="ts">
const route = useRoute()
const slug = String(route.params.slug)
const { categories, products, current } = await useCatalog(slug)
const c = current!
usePageHead({ title: c.seoTitle || c.name, description: c.seoDesc || `${c.name} — UA GREEN`, image: c.image })
</script>

<template>
  <div class="page section-inner">
    <h1>{{ c.name }}</h1>
    <CategoryNav :categories="categories" :active="slug" />
    <div v-if="c.descriptionHtml" class="prose" v-html="c.descriptionHtml" />
    <ProductList v-if="products.length" :products="products" />
    <p v-else class="empty">{{ $t('shop.empty') }}</p>
  </div>
</template>

<style scoped>
.empty { color: var(--ink-2); }
.prose { margin-bottom: 2rem; }
</style>
