/** Title/description/OG from settings (overridable per page) + i18n lang, dir and hreflang links. */
export function useSiteHead(opts: { title?: string; description?: string; image?: string | null } = {}) {
  const settings = useSettings()
  const { locale } = useI18n()
  const config = useRuntimeConfig()
  const i18nHead = useLocaleHead({ dir: true, lang: true, seo: true })

  const title = computed(() =>
    opts.title ?? (locale.value === 'en' ? 'UA GREEN | Wind turbines and equipment' : settings.value?.site_title_uk ?? 'UA GREEN'),
  )
  const description = computed(() =>
    opts.description
    ?? (locale.value === 'en'
      ? 'Ukrainian manufacturer of vertical-axis wind turbines and the equipment around them.'
      : settings.value?.site_description_uk ?? ''),
  )
  const image = computed(() => (opts.image ? new URL(opts.image, config.public.siteUrl).toString() : undefined))

  useHead(() => ({ htmlAttrs: i18nHead.value.htmlAttrs, link: i18nHead.value.link, meta: i18nHead.value.meta }))
  useSeoMeta({
    title,
    description,
    ogTitle: title,
    ogDescription: description,
    ogImage: image,
    ogType: 'website',
    twitterCard: 'summary_large_image',
  })
}
