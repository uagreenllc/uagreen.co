/**
 * The site's own page-view counter → POST /api/stats/hit → admin "Статистика".
 * A random visitor id lives in localStorage; 30 min without views starts a new session.
 * Off when the `stats_disabled` setting is set (toggle on the admin stats page).
 */
const SESSION_GAP = 30 * 60 * 1000

export default defineNuxtPlugin((nuxtApp) => {
  const settings = useSettings()
  const endpoint = apiUrl('/stats/hit') // resolve in plugin context, not inside the hooks
  let last = ''
  let referrer = document.referrer // only the landing page has a real one

  function track() {
    const url = location.pathname + location.search
    if (url === last || settings.value?.stats_disabled) return
    last = url
    try {
      let visitor = localStorage.getItem('uag.vid')
      if (!visitor) localStorage.setItem('uag.vid', (visitor = crypto.randomUUID()))
      const now = Date.now()
      const entry = now - Number(localStorage.getItem('uag.seen') || 0) > SESSION_GAP
      localStorage.setItem('uag.seen', String(now))
      const ref = referrer && new URL(referrer).origin !== location.origin ? referrer : ''
      referrer = ''
      // fetch+keepalive, not sendBeacon: Chrome refuses a JSON beacon to another origin (NUXT_PUBLIC_API_BASE).
      fetch(endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ url, ref, visitor, entry }),
        keepalive: true,
      }).catch(() => {})
    } catch {
      // Storage blocked or no crypto.randomUUID (plain http): just don't count.
    }
  }

  // Landing page on mount, then every client-side navigation (`last` drops the double count).
  nuxtApp.hook('app:mounted', track)
  useRouter().afterEach((_to, _from, failure) => {
    if (!failure) track()
  })
})
