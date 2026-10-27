/** Public settings (phones, emails, address, socials.*, analytics ids) loaded once in app.vue. */
export const useSettings = () => useState<Record<string, string> | null>('settings', () => null)
