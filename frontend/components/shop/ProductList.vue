<script setup lang="ts">
import type { ProductCard } from '~/types/api'

// Rows, not a card grid: photo left, name / short description / price right.
defineProps<{ products: ProductCard[] }>()
const localePath = useLocalePath()
const money = useMoney()
</script>

<template>
  <ul class="list">
    <li v-for="p in products" :key="p.slug">
      <NuxtLink :to="localePath(`/shop/${p.slug}`)">
        <img v-if="p.image" :src="p.image" :alt="p.name" width="440" height="330" loading="lazy">
        <span v-else class="blank" />
        <span class="text">
          <span class="name">{{ p.name }}</span>
          <span v-if="p.shortDesc" class="desc">{{ p.shortDesc }}</span>
          <span class="price">
            {{ money(p.price) }}
            <s v-if="p.oldPrice">{{ money(p.oldPrice) }}</s>
          </span>
          <span class="stock">{{ p.inStock ? $t('shop.inStock') : $t('shop.onOrder') }}</span>
        </span>
      </NuxtLink>
    </li>
  </ul>
</template>

<style scoped>
.list { list-style: none; margin: 0; padding: 0; }
.list li { border-top: 1px solid var(--line); }
.list li:last-child { border-bottom: 1px solid var(--line); }
.list a { display: grid; grid-template-columns: 280px 1fr; gap: 32px; align-items: center; padding: 24px 0; text-decoration: none; }
.list img, .blank { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 8px; background: var(--surface); }
.text { display: grid; gap: 6px; align-content: center; }
.name { font-weight: 500; font-size: clamp(20px, 2vw, 26px); line-height: 1.25; text-wrap: balance; }
.list a:hover .name { color: var(--green); }
.desc { color: var(--ink-2); max-width: 56ch; }
.price { font-size: 20px; font-weight: 600; margin-top: 6px; }
.price s { color: var(--metal); font-weight: 400; font-size: 16px; margin-left: 10px; }
.stock { font-size: 13px; color: var(--ink-2); }
@media (max-width: 700px) { .list a { grid-template-columns: 120px 1fr; gap: 16px; padding: 16px 0; } .desc { display: none; } .name { font-size: 17px; } .price { font-size: 17px; } }
</style>
