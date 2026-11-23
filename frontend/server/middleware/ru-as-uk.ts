// Russian-language browsers get the Ukrainian site: "ru-RU,ru;q=0.9,en;q=0.8" is read as
// "uk-RU,uk;q=0.9,en;q=0.8" by the i18n first-visit detection (detectBrowserLanguage in nuxt.config).
export default defineEventHandler((event) => {
  const h = event.node.req.headers['accept-language']
  if (h) event.node.req.headers['accept-language'] = h.split(',').map(t => t.trim().replace(/^ru\b/i, 'uk')).join(',')
})
