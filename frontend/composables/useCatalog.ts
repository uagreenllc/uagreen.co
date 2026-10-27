import type { Category, ProductCard } from '~/types/api'

/** Categories + products for /shop and /shop/cat/[slug]; an unknown category is a 404. */
export async function useCatalog(category?: string) {
  const [{ data: categories }, { data: products }] = await Promise.all([
    useApiFetch<Category[]>('categories', '/catalog/categories'),
    useApiFetch<ProductCard[]>(`products:${category ?? 'all'}`, '/catalog/products', { category }),
  ])
  const current = category ? categories.value?.find(c => c.slug === category) : undefined
  if (category && !current) throw createError({ statusCode: 404 })
  return { categories: computed(() => categories.value ?? []), products: computed(() => products.value ?? []), current }
}
