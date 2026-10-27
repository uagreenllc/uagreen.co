type Query = Record<string, MaybeRefOrGetter<string | number | boolean | undefined>>

/** `${origin}/api${path}` — origin differs between SSR (NUXT_API_BASE) and the browser (NUXT_PUBLIC_API_BASE). */
export function apiUrl(path: string): string {
  const c = useRuntimeConfig()
  const base = import.meta.server ? c.apiBase : c.public.apiBase
  return `${base}/api${path}`
}

/**
 * GET from the public API in the current locale. Never throws into render:
 * on any failure `data` is null and the section that needed it renders nothing.
 * Ref/getter query values are watched, so `?page=` navigation refetches.
 */
export function useApiFetch<T>(key: string, path: string, query: Query = {}) {
  const { locale } = useI18n()
  const q = computed(() => {
    const out: Record<string, string | number | boolean> = { lang: locale.value }
    for (const [k, v] of Object.entries(query)) {
      const val = toValue(v)
      if (val !== undefined) out[k] = val
    }
    return out
  })
  return useFetch<T | null>(() => apiUrl(path), {
    key: `${key}:${locale.value}`,
    query: q,
    timeout: 4000,
    default: () => null,
    onRequestError({ error }) {
      console.warn(`[api] ${path} → ${error.message}`)
    },
    onResponseError({ response }) {
      console.warn(`[api] ${path} → ${response.status}`)
    },
  })
}
