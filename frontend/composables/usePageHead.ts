/** Per-page title/description/OG image on top of the site defaults set in app.vue. */
export function usePageHead(o: { title?: string | null; description?: string | null; image?: string | null }) {
  const config = useRuntimeConfig()
  const image = o.image ? new URL(o.image, config.public.siteUrl).toString() : undefined
  useSeoMeta({
    title: o.title ?? undefined,
    ogTitle: o.title ?? undefined,
    description: o.description ?? undefined,
    ogDescription: o.description ?? undefined,
    ogImage: image,
  })
}
