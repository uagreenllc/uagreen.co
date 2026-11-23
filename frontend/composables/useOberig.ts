export type OberigPlacement = 'home' | 'product' | 'order' | 'footer'

/**
 * Links to the "Оберіг" monitoring portal, from public settings (back office):
 * `oberig.url` (empty → portal.uagreen.co), `oberig.calculator_url` (empty → portal's wind calculator),
 * `oberig.home|product|order|footer` placement switches — missing = on, only "0" turns one off.
 */
export function oberigLinks(s: Record<string, string> | null | undefined) {
  const url = (s?.['oberig.url']?.trim() || 'https://portal.uagreen.co').replace(/\/+$/, '')
  return {
    url,
    calculatorUrl: s?.['oberig.calculator_url']?.trim() || `${url}/#/wind-calculator`,
    on: (p: OberigPlacement) => s?.[`oberig.${p}`]?.trim() !== '0',
  }
}

export const useOberig = () => {
  const settings = useSettings()
  return computed(() => oberigLinks(settings.value))
}
