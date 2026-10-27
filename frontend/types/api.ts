// Shapes of the public API (backend/src/UaGreen.Site.Api/Features/*), camelCase as serialized.
export interface ProductCard {
  slug: string
  sku: string | null
  name: string
  shortDesc: string | null
  price: number
  oldPrice: number | null
  inStock: boolean
  isFeatured: boolean
  image: string | null
  categorySlug: string | null
  categoryName: string | null
}

export interface PostCard {
  slug: string
  title: string
  excerpt: string | null
  cover: string | null
  author: string | null
  publishedAt: string
}

export interface Page<T> {
  items: T[]
  total: number
  pageNumber: number
  pageSize: number
}

export interface ContentBlock {
  key: string
  lang: string
  title: string | null
  bodyHtml: string
}
