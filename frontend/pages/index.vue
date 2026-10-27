<script setup lang="ts">
import type { ContentBlock, Page, PostCard, ProductCard } from '~/types/api'

const { data: products } = await useApiFetch<ProductCard[]>('products-featured', '/catalog/products', { featured: true })
const { data: blocks } = await useApiFetch<ContentBlock[]>('home-blocks', '/content', { keys: 'home.projects,home.testimonials' })
const { data: posts } = await useApiFetch<Page<PostCard>>('posts-latest', '/blog', { pageSize: 3 })

const list = computed(() => products.value ?? [])
const lead = computed(() => list.value.find(p => p.slug === 'k5v1') ?? list.value[0])
const block = (key: string) => blocks.value?.find(b => b.key === key) ?? null

useSiteHead({ image: lead.value?.image })

const settings = useSettings()
const config = useRuntimeConfig()
const org = {
  '@context': 'https://schema.org',
  '@type': 'Organization',
  name: 'UA GREEN',
  url: config.public.siteUrl,
  telephone: splitList(settings.value?.phones)[0],
  email: splitList(settings.value?.emails)[0],
  address: { '@type': 'PostalAddress', streetAddress: settings.value?.address, addressCountry: 'UA' },
  sameAs: ['instagram', 'tiktok', 'linkedin', 'youtube'].map(n => settings.value?.[`socials.${n}`]).filter(Boolean),
}
useHead({ script: [{ type: 'application/ld+json', innerHTML: JSON.stringify(org).replace(/</g, '\\u003c') }] })
</script>

<template>
  <div>
    <K5Story />
    <FeaturedProducts :products="list" />
    <WhyVertical :image="lead?.image ?? null" />
    <ContentBlock :block="block('home.projects')" />
    <ContentBlock :block="block('home.testimonials')" />
    <LatestPosts :posts="posts?.items ?? []" />
    <ContactCta />
  </div>
</template>
