type Query = Record<string, string | number | boolean>

/** `${origin}/api${path}` — origin differs between SSR (NUXT_API_BASE) and the browser (NUXT_PUBLIC_API_BASE). */
export function apiUrl(path: string): string {
  const c = useRuntimeConfig()
  const base = import.meta.server ? c.apiBase : c.public.apiBase
  return `${base}/api${path}`
}

/**
 * GET from the public API in the current locale. Never throws into render:
 * on any failure `data` is null and the section that needed it renders nothing.
 */
export function useApiFetch<T>(key: string, path: string, query: Query = {}) {
  const { locale } = useI18n()
  return useFetch<T | null>(() => apiUrl(path), {
    key: `${key}:${locale.value}`,
    query: { lang: locale.value, ...query },
    timeout: 8000,
    default: () => null,
    onResponseError({ response }) {
      console.warn(`[api] ${path} → ${response.status}`)
    },
  })
}
