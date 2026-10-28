<script setup lang="ts">
import type { PostCard } from '~/types/api'

defineProps<{ posts: PostCard[] }>()
const localePath = useLocalePath()
</script>

<template>
  <section v-if="posts.length" class="section">
    <div class="section-inner">
      <h2>{{ $t('home.posts.h') }}</h2>
      <ul class="posts">
        <PostRow v-for="p in posts" :key="p.slug" :post="p" />
      </ul>
      <p class="all"><NuxtLink class="link" :to="localePath('/blog')">{{ $t('home.posts.all') }}</NuxtLink></p>
    </div>
  </section>
</template>

<style scoped>
.posts { list-style: none; margin: 0; padding: 0; max-width: 860px; }
.all { margin: 32px 0 0; }
</style>
