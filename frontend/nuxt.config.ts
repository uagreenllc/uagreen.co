// Env contract (see README): NUXT_API_BASE = API origin for SSR, NUXT_PUBLIC_API_BASE = origin
// for the browser ("" = same origin, nginx proxies /api/), NUXT_PUBLIC_SITE_URL, NITRO_PORT.
const siteUrl = process.env.NUXT_PUBLIC_SITE_URL || 'http://localhost:3000'

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  modules: ['@nuxtjs/i18n', '@nuxtjs/sitemap', '@pinia/nuxt', '@nuxtjs/tailwindcss'],
  css: ['@fontsource-variable/commissioner', '~/assets/css/main.css'],
  // Components are named by file (K5Story, SiteHeader, FeaturedProducts), not by folder + file.
  components: [{ path: '~/components', pathPrefix: false }],
  runtimeConfig: {
    apiBase: 'http://localhost:5000',
    public: { apiBase: '', siteUrl },
  },
  i18n: {
    locales: [
      { code: 'uk', language: 'uk-UA', file: 'uk.json', name: 'Українська' },
      { code: 'en', language: 'en-US', file: 'en.json', name: 'English' },
    ],
    defaultLocale: 'uk',
    strategy: 'prefix_except_default',
    detectBrowserLanguage: false,
    baseUrl: siteUrl,
  },
  site: { url: siteUrl },
  sitemap: {
    sources: ['/_sitemap/urls'],
    exclude: ['/cart', '/checkout', '/order/**', '/en/cart', '/en/checkout', '/en/order/**'],
  },
  // Dev only: the browser talks to the API and its static media through the Nuxt dev server.
  // Nitro honours `proxy` here (verified), Nuxt's typed subset of route rules just omits it.
  $development: {
    routeRules: {
      // @ts-expect-error proxy is a Nitro route rule
      '/api/**': { proxy: 'http://localhost:5000/api/**' },
      // @ts-expect-error proxy is a Nitro route rule
      '/media/**': { proxy: 'http://localhost:5000/media/**' },
    },
  },
  // HMR websocket on its own port: otherwise it grabs :3000 on the other IP family and plain
  // HTTP there answers "426 Upgrade Required" (localhost → ::1 vs 127.0.0.1 on Windows).
  vite: { server: { hmr: { port: 24678 } } },
  app: {
    head: {
      htmlAttrs: { lang: 'uk' },
      meta: [{ name: 'viewport', content: 'width=device-width, initial-scale=1' }],
    },
  },
})
