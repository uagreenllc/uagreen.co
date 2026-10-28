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

export interface Category {
  slug: string
  name: string
  image: string | null
  productCount: number
}

export interface OptionValue { id: string; value: string; priceDelta: number }
export interface ProductOption { id: string; name: string; values: OptionValue[] }

export interface Product {
  id: string
  slug: string
  sku: string | null
  name: string
  shortDesc: string | null
  price: number
  oldPrice: number | null
  inStock: boolean
  isFeatured: boolean
  categorySlug: string | null
  categoryName: string | null
  descriptionHtml: string | null
  specsHtml: string | null
  faqHtml: string | null
  images: { url: string; alt: string | null }[]
  files: { url: string; title: string }[]
  options: ProductOption[]
  seoTitle: string | null
  seoDesc: string | null
}

export interface PostCard {
  slug: string
  title: string
  excerpt: string | null
  cover: string | null
  author: string | null
  publishedAt: string
}

export interface Post extends PostCard {
  bodyHtml: string
  seoTitle: string | null
  seoDesc: string | null
  updatedAt: string
}

export interface Vacancy {
  slug: string
  title: string
  department: string
  employmentType: string
  city: string
  experience: string | null
  language: string | null
  requirementsHtml: string | null
  niceToHaveHtml: string | null
  conditionsHtml: string | null
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

export interface Redirect { from: string; to: string }

/** The API's error body ({ code, message }); ofetch exposes it as `error.data`. */
export interface ApiError { code: string; message: string }

export type DeliveryMethod = 'NovaPoshta' | 'Ukrposhta' | 'Pickup'
export type PaymentMethod = 'LiqPay' | 'Invoice' | 'Credit'

export interface CreateOrder {
  customer: { name: string; phone: string; email: string | null }
  delivery: { method: DeliveryMethod; city: string | null; warehouse: string | null }
  paymentMethod: PaymentMethod
  comment: string | null
  items: { productId: string; qty: number; optionValueIds: string[] }[]
  lang: string
  website: string
}

export interface OrderItem { productName: string; optionsText: string | null; qty: number; unitPrice: number; lineTotal: number }

export interface Order {
  id: string
  number: number
  status: string
  paymentMethod: PaymentMethod
  subtotal: number
  deliveryCost: number
  total: number
  createdAt: string
  items: OrderItem[]
  liqPay: { checkoutUrl: string; data: string; signature: string } | null
}
