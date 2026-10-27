<script setup lang="ts">
import type { ProductCard } from '~/types/api'

const props = defineProps<{ products: ProductCard[] }>()
const localePath = useLocalePath()
const money = useMoney()

const lead = computed(() => props.products.find(p => p.slug === 'k5v1') ?? props.products[0])
const rest = computed(() => props.products.filter(p => p !== lead.value).slice(0, 3))
</script>

<template>
  <section v-if="lead" class="section">
    <div class="section-inner">
      <h2>{{ $t('home.products.h') }}</h2>

      <article class="lead">
        <NuxtLink :to="localePath(`/shop/${lead.slug}`)" class="photo">
          <img v-if="lead.image" :src="lead.image" :alt="lead.name" width="900" height="600" loading="lazy">
        </NuxtLink>
        <div class="text">
          <h3>{{ lead.name }}</h3>
          <p v-if="lead.shortDesc" class="desc">{{ lead.shortDesc }}</p>
          <p class="price"><b>{{ money(lead.price) }}</b><s v-if="lead.oldPrice">{{ money(lead.oldPrice) }}</s></p>
          <NuxtLink class="link" :to="localePath(`/shop/${lead.slug}`)">{{ $t('home.products.more') }}</NuxtLink>
        </div>
      </article>

      <ul v-if="rest.length" class="rest">
        <li v-for="p in rest" :key="p.slug">
          <NuxtLink :to="localePath(`/shop/${p.slug}`)">
            <img v-if="p.image" :src="p.image" :alt="p.name" width="600" height="600" loading="lazy">
            <span class="name">{{ p.name }}</span>
            <span class="small-price">{{ money(p.price) }}</span>
          </NuxtLink>
        </li>
      </ul>

      <p class="all"><NuxtLink class="link" :to="localePath('/shop')">{{ $t('home.products.all') }}</NuxtLink></p>
    </div>
  </section>
</template>

<style scoped>
.lead { display: grid; grid-template-columns: 3fr 2fr; gap: 40px; align-items: center; margin-bottom: 56px; }
.photo img { width: 100%; aspect-ratio: 3 / 2; object-fit: cover; border-radius: 10px; background: var(--surface); }
.text h3 { font-size: clamp(24px, 2.4vw, 32px); font-weight: 600; margin: 0 0 12px; }
.desc { color: var(--ink-2); font-size: 17px; max-width: 48ch; margin: 0 0 18px; }
.price { margin: 0 0 20px; font-size: 22px; }
.price b { font-weight: 600; }
.price s { margin-left: 12px; color: var(--ink-2); font-size: 17px; }
.rest { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 32px; }
.rest a { display: block; text-decoration: none; }
.rest img { width: 100%; aspect-ratio: 1; object-fit: cover; border-radius: 10px; background: var(--surface); margin-bottom: 12px; }
.name { display: block; font-weight: 500; }
.small-price { display: block; color: var(--ink-2); margin-top: 2px; }
.all { margin: 40px 0 0; }
@media (max-width: 900px) {
  .lead { grid-template-columns: 1fr; gap: 20px; margin-bottom: 40px; }
  .rest { grid-template-columns: 1fr 1fr; gap: 16px; }
}
</style>
