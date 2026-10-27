<script setup lang="ts">
const localePath = useLocalePath()
const switchLocalePath = useSwitchLocalePath()
const { locale } = useI18n()
const settings = useSettings()

const links = [
  ['shop', '/shop'],
  ['about', '/about-us'],
  ['blog', '/blog'],
  ['vacancies', '/vacancies'],
  ['contacts', '/contacts'],
] as const
const other = computed(() => (locale.value === 'uk' ? 'en' : 'uk'))
const phone = computed(() => splitList(settings.value?.phones)[0] ?? '')
</script>

<template>
  <header class="site-header">
    <NuxtLink :to="localePath('/')" class="logo">UA GREEN</NuxtLink>

    <nav class="nav-desktop" aria-label="Main">
      <NuxtLink v-for="[key, to] in links" :key="key" :to="localePath(to)">{{ $t(`nav.${key}`) }}</NuxtLink>
      <CartBadge />
      <NuxtLink :to="switchLocalePath(other)" class="lang">{{ $t(`lang.${other}`) }}</NuxtLink>
      <a v-if="phone" :href="`tel:${phone}`" class="pill">{{ formatPhone(phone) }}</a>
    </nav>

    <details class="nav-mobile">
      <summary>{{ $t('nav.menu') }}</summary>
      <ul>
        <li v-for="[key, to] in links" :key="key"><NuxtLink :to="localePath(to)">{{ $t(`nav.${key}`) }}</NuxtLink></li>
        <li><CartBadge /></li>
        <li><NuxtLink :to="switchLocalePath(other)">{{ $t(`lang.${other}`) }}</NuxtLink></li>
        <li v-if="phone"><a :href="`tel:${phone}`">{{ formatPhone(phone) }}</a></li>
      </ul>
    </details>
  </header>
</template>

<style scoped>
.site-header {
  position: fixed;
  inset: 0 0 auto 0;
  z-index: var(--z-header);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 18px 40px;
  background: var(--bg);
}
.logo { font-weight: 700; letter-spacing: 0.04em; color: var(--green); text-decoration: none; font-size: 18px; }
.nav-desktop { display: flex; align-items: center; gap: 26px; }
.nav-desktop a { color: var(--ink); text-decoration: none; font-size: 15px; }
.nav-desktop a.router-link-active:not(.pill) { color: var(--green); }
.lang { color: var(--ink-2) !important; }
.pill { background: var(--green); color: #fff !important; padding: 9px 16px; border-radius: 999px; font-weight: 500; }
.pill:hover { background: var(--green-2); }

.nav-mobile { display: none; position: relative; }
.nav-mobile summary { list-style: none; cursor: pointer; font-size: 15px; padding: 8px 0; }
.nav-mobile summary::-webkit-details-marker { display: none; }
.nav-mobile ul {
  position: absolute; right: 0; top: 100%;
  margin: 8px 0 0; padding: 12px 20px; list-style: none;
  background: var(--bg); border: 1px solid var(--line); border-radius: 12px; min-width: 200px;
}
.nav-mobile li { padding: 8px 0; }
.nav-mobile a { color: var(--ink); text-decoration: none; font-size: 16px; }

@media (max-width: 900px) {
  .site-header { padding: 14px 16px; }
  .nav-desktop { display: none; }
  .nav-mobile { display: block; }
}
</style>
