// Dynamic sitemap entries from the API; @nuxtjs/sitemap adds the uk/en variants (_i18nTransform).
// Lives outside /api because the dev server proxies /api/** to the .NET API.
export default defineSitemapEventHandler(async (event) => {
  const base = `${useRuntimeConfig(event).apiBase}/api`
  const get = async <T>(path: string): Promise<T[]> => {
    try {
      return await $fetch<T[]>(`${base}${path}`, { timeout: 4000 })
    } catch (e) {
      console.warn(`[sitemap] ${path} → ${(e as Error).message}`)
      return []
    }
  }
  const [products, categories, posts, vacancies] = await Promise.all([
    get<{ slug: string }>('/catalog/products'),
    get<{ slug: string }>('/catalog/categories'),
    $fetch<{ items: { slug: string }[] }>(`${base}/blog?pageSize=500`, { timeout: 4000 }).then(p => p.items, () => []),
    get<{ slug: string }>('/vacancies'),
  ])
  return [
    ...products.map(p => `/shop/${p.slug}`),
    ...categories.map(c => `/shop/cat/${c.slug}`),
    ...posts.map(p => `/blog/${p.slug}`),
    ...vacancies.map(v => `/vacancies/${v.slug}`),
  ].map(loc => asSitemapUrl({ loc, _i18nTransform: true }))
})
