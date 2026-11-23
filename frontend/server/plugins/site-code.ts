/**
 * Сторонній код from the admin (settings code_*), pasted as is into every SSR page.
 * app.vue hands the settings over on event.context; SPA navigation keeps what the first load ran.
 */
export default defineNitroPlugin((nitro) => {
  nitro.hooks.hook('render:html', (html, { event }) => {
    const s = event.context.siteSettings as Record<string, string> | undefined
    if (s?.code_head) html.head.push(s.code_head)
    if (s?.code_css) html.head.push(`<style>${s.code_css}</style>`)
    if (s?.code_body_start) html.bodyPrepend.unshift(s.code_body_start)
    if (s?.code_body_end) html.bodyAppend.push(s.code_body_end)
  })
})
