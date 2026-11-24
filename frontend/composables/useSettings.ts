/** Public settings (phones, emails, address, socials.*, analytics ids) loaded once in app.vue. */
export const useSettings = () => useState<Record<string, string> | null>('settings', () => null)

/** A translatable setting (`address`, `address_en`, `address_cs`): this locale, then EN, then the plain (UK) key. */
export function useLocalSetting(key: string) {
  const settings = useSettings()
  const { locale } = useI18n()
  return computed(() => {
    const s = settings.value
    return (locale.value !== 'uk' && (s?.[`${key}_${locale.value}`] || s?.[`${key}_en`])) || s?.[key] || ''
  })
}
