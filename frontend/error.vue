<script setup lang="ts">
import type { NuxtError } from '#app'
import type { ContentBlock } from '~/types/api'

const props = defineProps<{ error: NuxtError }>()
const { t } = useI18n()
const localePath = useLocalePath()
const notFound = props.error.statusCode === 404

// The old site's 404 copy ("the solar panels let us down") lives in the back office.
const { data: block } = notFound
  ? await useApiFetch<ContentBlock>('content:page.404', '/content/page.404')
  : { data: ref(null) }

const title = computed(() => (notFound ? block.value?.title || t('error.notFoundTitle') : t('error.generic')))
useSiteHead() // error.vue replaces app.vue, so the site defaults are set here too
usePageHead({ title: title.value })
</script>

<template>
  <NuxtLayout>
    <article class="page section-inner">
      <h1>{{ title }}</h1>
      <div v-if="block" class="prose" v-html="block.bodyHtml" />
      <p v-else-if="!notFound" class="prose"><code>{{ error.statusCode }}</code> {{ error.statusMessage }}</p>
      <p><a class="btn" :href="localePath('/')" @click.prevent="clearError({ redirect: localePath('/') })">{{ $t('error.home') }}</a></p>
    </article>
  </NuxtLayout>
</template>
