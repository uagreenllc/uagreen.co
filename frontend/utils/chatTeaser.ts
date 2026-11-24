export const TEASER_DELAY_MS = 30_000
export const DISMISS_MS = 7 * 24 * 60 * 60 * 1000

/** Purchase pages (cart, checkout, order, paid audit booking), with or without a locale prefix and a trailing slash. */
export function isTeaserBlockedPath(path: string): boolean {
  const p = path.replace(/\/+$/, '').replace(/^\/(en|cs)(?=\/|$)/, '')
  return p === '/cart' || p === '/checkout' || p === '/order' || p.startsWith('/order/') || p === '/audit/book'
}

export function teaserAllowed(o: { path: string; hasConversation: boolean; dismissedAt: number | null; now: number; hasQuestion: boolean }): boolean {
  if (o.hasConversation || !o.hasQuestion || isTeaserBlockedPath(o.path)) return false
  return o.dismissedAt === null || o.now - o.dismissedAt >= DISMISS_MS
}

/** Keeps the stored question while it is still active; otherwise picks at random. */
export function pickQuestion<T extends { id: string }>(questions: T[], storedId: string | null, random: () => number = Math.random): T | null {
  if (!questions.length) return null
  const kept = storedId === null ? undefined : questions.find(q => q.id === storedId)
  return kept ?? questions[Math.floor(random() * questions.length)]
}
