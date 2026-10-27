<script setup lang="ts">
defineProps<{ image: string | null }>()
const { tm, rt } = useI18n()
const facts = computed(() => (tm('home.why.facts') as { v: string; l: string }[]).map(f => ({ v: rt(f.v), l: rt(f.l) })))
</script>

<template>
  <section class="section why">
    <div class="section-inner grid">
      <div class="copy">
        <h2>{{ $t('home.why.h') }}</h2>
        <p>{{ $t('home.why.p1') }}</p>
        <p>{{ $t('home.why.p2') }}</p>
        <p class="facts">
          <template v-for="(f, i) in facts" :key="i"><i v-if="i" aria-hidden="true">·</i><b>{{ f.v }}</b> {{ f.l }}</template>
        </p>
      </div>
      <img v-if="image" :src="image" :alt="$t('home.why.alt')" width="900" height="900" loading="lazy">
    </div>
  </section>
</template>

<style scoped>
.why { background: var(--surface); }
.grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 56px; align-items: center; }
.copy p { color: var(--ink-2); font-size: 17px; max-width: 52ch; margin: 0 0 14px; }
.facts { padding-top: 16px; border-top: 1px solid var(--line); margin-top: 24px !important; }
.facts b { color: var(--ink); font-weight: 600; font-size: 18px; }
.facts i { font-style: normal; margin: 0 10px; color: var(--metal); }
img { width: 100%; aspect-ratio: 1; object-fit: cover; border-radius: 10px; }
@media (max-width: 900px) {
  .grid { grid-template-columns: 1fr; gap: 24px; }
  img { order: -1; aspect-ratio: 4 / 3; }
}
</style>
