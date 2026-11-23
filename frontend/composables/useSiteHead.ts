/** Title/description/OG from settings (overridable per page) + i18n lang, dir and hreflang links. */
export function useSiteHead(opts: { title?: string; description?: string; image?: string | null } = {}) {
  const settings = useSettings()
  const { locale, t } = useI18n()
  const config = useRuntimeConfig()
  const i18nHead = useLocaleHead({ dir: true, lang: true, seo: true })

  // UK copy is editable in settings; other locales use i18n `site.*`.
  const title = computed(() =>
    opts.title ?? (locale.value === 'uk' ? settings.value?.site_title_uk ?? 'UA GREEN' : t('site.title')),
  )
  const description = computed(() =>
    opts.description ?? (locale.value === 'uk' ? settings.value?.site_description_uk ?? '' : t('site.description')),
  )
  const image = computed(() => (opts.image ? new URL(opts.image, config.public.siteUrl).toString() : undefined))

  useHead(() => ({
    htmlAttrs: i18nHead.value.htmlAttrs,
    link: i18nHead.value.link,
    meta: i18nHead.value.meta,
    titleTemplate: t => (!t || t.includes('UA GREEN') ? t ?? null : `${t} — UA GREEN`),
  }))
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
