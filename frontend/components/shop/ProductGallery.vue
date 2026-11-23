<script setup lang="ts">
const props = defineProps<{ images: { url: string; alt: string | null }[]; name: string }>()
const current = ref(0)
const main = computed(() => props.images[current.value] ?? props.images[0])
const dlg = ref<HTMLDialogElement>()
const step = (d: number) => { current.value = (current.value + d + props.images.length) % props.images.length }
</script>

<template>
  <div class="gallery">
    <button v-if="main" type="button" class="zoom" :aria-label="`${$t('shop.zoom')}: ${main.alt || name}`" @click="dlg?.showModal()">
      <img :src="main.url" :alt="main.alt || name" width="900" height="675" class="main">
    </button>
    <span v-else class="main blank" />
    <div v-if="images.length > 1" class="thumbs" role="tablist">
      <button
        v-for="(img, i) in images" :key="img.url" type="button" role="tab"
        :aria-selected="i === current" :class="{ on: i === current }" @click="current = i"
      >
        <img :src="img.url" :alt="img.alt || `${name} ${i + 1}`" width="120" height="90" loading="lazy">
      </button>
    </div>
    <!-- native <dialog>: Esc, focus trap and backdrop for free; any click outside the arrows closes -->
    <dialog v-if="main" ref="dlg" class="lightbox" @click="dlg?.close()" @keydown.left="step(-1)" @keydown.right="step(1)">
      <img :src="main.url" :alt="main.alt || name">
      <!-- SVG, not ×/‹/› glyphs: glyphs sit on the text baseline and never centre in the circle -->
      <button type="button" class="x" :aria-label="$t('shop.close')">
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" /></svg>
      </button>
      <template v-if="images.length > 1">
        <button type="button" class="nav prev" :aria-label="$t('shop.prevPhoto')" @click.stop="step(-1)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M15 5l-7 7 7 7" /></svg>
        </button>
        <button type="button" class="nav next" :aria-label="$t('shop.nextPhoto')" @click.stop="step(1)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 5l7 7-7 7" /></svg>
        </button>
      </template>
    </dialog>
  </div>
</template>

<style scoped>
.main { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 12px; background: var(--surface); }
.zoom { display: block; width: 100%; padding: 0; border: 0; background: none; cursor: zoom-in; }
.zoom:focus-visible { outline: 2px solid var(--green); outline-offset: 2px; border-radius: 12px; }
.thumbs { display: flex; gap: 8px; margin-top: 10px; overflow-x: auto; padding-bottom: 4px; }
.thumbs button { flex: 0 0 72px; padding: 0; border: 2px solid transparent; border-radius: 8px; background: none; cursor: pointer; }
.thumbs button.on { border-color: var(--green); }
.thumbs button:focus-visible { outline: 2px solid var(--green); outline-offset: 2px; }
.thumbs img { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 6px; background: var(--surface); }

.lightbox { width: 96vw; height: 94vh; max-width: none; max-height: none; padding: 0; border: 0; background: none; overflow: visible; }
.lightbox::backdrop { background: rgb(0 0 0 / .88); }
.lightbox > img { width: 100%; height: 100%; object-fit: contain; cursor: zoom-out; }
/* opaque: a translucent circle lets the page header show through and the button reads as an oval */
.lightbox button { position: fixed; display: grid; place-items: center; width: 48px; height: 48px; padding: 0; border: 0; border-radius: 50%; background: #2b2b2b; color: #fff; cursor: pointer; }
.lightbox button:hover { background: #444; }
.lightbox svg { width: 24px; height: 24px; fill: none; stroke: currentColor; stroke-width: 2.5; stroke-linecap: round; stroke-linejoin: round; }
.lightbox button:focus-visible { outline: 2px solid #fff; outline-offset: 2px; }
.x { top: 16px; right: 16px; }
.nav { top: 50%; translate: 0 -50%; }
.prev { left: 16px; }
.next { right: 16px; }
:global(html:has(.lightbox[open])) { overflow: hidden; }
</style>
