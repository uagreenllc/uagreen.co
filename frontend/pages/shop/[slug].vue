<script setup lang="ts">
import type { Product } from '~/types/api'
import type { Selection } from '~/utils/pricing'
import { useCart } from '~/stores/cart'

const route = useRoute()
const localePath = useLocalePath()
const money = useMoney()
const slug = String(route.params.slug)

const { data } = await useApiFetch<Product>(`product:${slug}`, `/catalog/products/${slug}`)
if (!data.value) throw createError({ statusCode: 404 })
const p = data.value

// First value of every option preselected; the price follows the selection.
const options = p.options.filter(o => o.values.length)
const selected = ref<Selection>(defaultSelection(p.options))
const price = computed(() => unitPrice(p, selected.value))
const hasOptions = options.length > 0

const sections = computed(() => [
  ['shop.about', p.descriptionHtml],
  ['shop.specs', p.specsHtml],
  ['shop.faq', p.faqHtml],
].filter(([, html]) => html) as [string, string][])

const cart = useCart()
const added = ref(false)
function addToCart() {
  cart.add({
    productId: p.id,
    slug: p.slug,
    name: p.name,
    image: p.images[0]?.url ?? null,
    unitPrice: price.value,
    optionValueIds: Object.values(selected.value),
    optionsText: optionsText(p, selected.value),
  })
  added.value = true
}
watch(selected, () => { added.value = false }, { deep: true })

usePageHead({ title: p.seoTitle || p.name, description: p.seoDesc || p.shortDesc, image: p.images[0]?.url })
const config = useRuntimeConfig()
const ld = {
  '@context': 'https://schema.org',
  '@type': 'Product',
  name: p.name,
  sku: p.sku ?? undefined,
  image: p.images.map(i => new URL(i.url, config.public.siteUrl).toString()),
  description: p.shortDesc ?? undefined,
  brand: { '@type': 'Brand', name: 'UA GREEN' },
  offers: {
    '@type': 'Offer',
    price: p.price,
    priceCurrency: 'UAH',
    availability: p.inStock ? 'https://schema.org/InStock' : 'https://schema.org/PreOrder',
    url: new URL(route.path, config.public.siteUrl).toString(),
  },
}
useHead({ script: [{ type: 'application/ld+json', innerHTML: JSON.stringify(ld).replace(/</g, '\\u003c') }] })
</script>

<template>
  <article class="page section-inner product">
    <p class="crumbs">
      <NuxtLink :to="localePath('/shop')">{{ $t('shop.title') }}</NuxtLink>
      <template v-if="p.categorySlug"> / <NuxtLink :to="localePath(`/shop/cat/${p.categorySlug}`)">{{ p.categoryName }}</NuxtLink></template>
    </p>
    <div class="top">
      <ProductGallery :images="p.images" :name="p.name" />
      <div class="buy">
        <h1>{{ p.name }}</h1>
        <p v-if="p.sku" class="sku">{{ $t('shop.sku') }} {{ p.sku }}</p>
        <p v-if="p.shortDesc" class="short">{{ p.shortDesc }}</p>
        <OptionPicker v-model="selected" :options="options" />
        <p class="price">
          <strong>{{ money(price) }}</strong>
          <s v-if="p.oldPrice && !hasOptions">{{ money(p.oldPrice) }}</s>
          <small v-if="hasOptions">{{ $t('shop.withOptions') }}</small>
        </p>
        <p class="stock">{{ p.inStock ? $t('shop.inStock') : $t('shop.onOrder') }}</p>
        <p class="cta">
          <button class="btn" type="button" @click="addToCart">{{ $t('shop.addToCart') }}</button>
          <span v-if="added" class="added">{{ $t('shop.added') }} · <NuxtLink class="link" :to="localePath('/cart')">{{ $t('shop.goToCart') }}</NuxtLink></span>
        </p>
      </div>
    </div>
    <section v-for="[key, html] in sections" :key="key" class="block">
      <h2>{{ $t(key) }}</h2>
      <div class="prose" v-html="html" />
    </section>
    <section v-if="p.files.length" class="block">
      <h2>{{ $t('shop.files') }}</h2>
      <ul class="files">
        <li v-for="f in p.files" :key="f.url"><a class="link" :href="f.url" target="_blank" rel="noopener">{{ f.title }}</a></li>
      </ul>
    </section>
  </article>
</template>

<style scoped>
.crumbs { color: var(--ink-2); font-size: 14px; margin: 0 0 20px; }
.crumbs a { color: inherit; text-decoration: none; }
.crumbs a:hover { color: var(--green); }
.top { display: grid; grid-template-columns: minmax(0, 7fr) minmax(0, 5fr); gap: 48px; align-items: start; }
.product h1 { font-size: clamp(28px, 3vw, 40px); margin-bottom: 8px; }
.sku { color: var(--ink-2); font-size: 14px; margin: 0 0 16px; }
.short { color: var(--ink-2); font-size: 17px; margin: 0 0 24px; }
.price { margin: 8px 0 4px; display: flex; align-items: baseline; gap: 12px; flex-wrap: wrap; }
.price strong { font-size: 30px; font-weight: 600; }
.price s { color: var(--metal); font-size: 18px; }
.price small { color: var(--ink-2); font-size: 14px; }
.stock { color: var(--ink-2); font-size: 14px; margin: 0 0 24px; }
.cta { display: flex; flex-wrap: wrap; align-items: center; gap: 16px; margin: 0; }
.added { color: var(--green); }
.block { margin-top: 64px; max-width: 860px; }
.block h2 { font-size: 26px; }
.block :deep(table) { border-collapse: collapse; width: 100%; max-width: 680px; font-size: 16px; }
.block :deep(th), .block :deep(td) { text-align: left; padding: 10px 12px 10px 0; border-bottom: 1px solid var(--line); vertical-align: top; }
.block :deep(th) { font-weight: 500; color: var(--ink-2); width: 45%; }
.files { padding-left: 1.2em; }
/* minmax(0, …): the thumbnail strip's min-content width must not widen the column */
@media (max-width: 900px) { .top { grid-template-columns: minmax(0, 1fr); gap: 28px; } }
</style>
