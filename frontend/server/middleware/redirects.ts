import type { Redirect } from '../../types/api'
import { matchRedirect } from '../../utils/redirects'

// Old-site URLs (root vacancy slugs, /en/*-1) → 301 to the new ones. The table lives in the
// API; it is re-read at most once a minute and the last good copy survives an API outage.
let table: Redirect[] = []
let loadedAt = 0

export default defineEventHandler(async (event) => {
  const path = event.path.split('?')[0]
  if (/^\/(api|_nuxt|media|__)/.test(path) || /\.[a-z0-9]+$/i.test(path)) return
  if (Date.now() - loadedAt > 60_000) {
    loadedAt = Date.now()
    try {
      table = await $fetch<Redirect[]>(`${useRuntimeConfig(event).apiBase}/api/redirects`, { timeout: 2000 })
    } catch (e) {
      console.warn(`[redirects] ${(e as Error).message}`)
    }
  }
  const to = matchRedirect(path, table)
  if (to) return sendRedirect(event, to, 301)
})
