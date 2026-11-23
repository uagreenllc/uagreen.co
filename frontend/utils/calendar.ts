/** Intl locale for a site locale code (weekday/month names, long dates). */
export const intlLocale = (code: string) => ({ en: 'en-GB', cs: 'cs-CZ' } as Record<string, string>)[code] ?? 'uk-UA'

export interface CalendarDay { iso: string; day: number; inWindow: boolean; free: boolean }

const DAY = 86_400_000
const iso = (t: number) => new Date(t).toISOString().slice(0, 10)

/**
 * Monday-first weeks covering from..to (ISO dates, inclusive), padded at both ends with out-of-window days.
 * Pure UTC arithmetic on calendar dates: the API already returns Kyiv dates, nothing here may shift them.
 */
export function calendarWeeks(from: string, to: string, free: string[]): CalendarDay[][] {
  const start = Date.parse(from), end = Date.parse(to)
  const freeSet = new Set(free)
  const first = start - ((new Date(start).getUTCDay() + 6) % 7) * DAY // back to Monday
  const weeks: CalendarDay[][] = []
  for (let t = first; t <= end; t += 7 * DAY) {
    weeks.push(Array.from({ length: 7 }, (_, i) => {
      const d = t + i * DAY, inWindow = d >= start && d <= end
      return { iso: iso(d), day: new Date(d).getUTCDate(), inWindow, free: inWindow && freeSet.has(iso(d)) }
    }))
  }
  return weeks
}
