import { describe, it, expect } from 'vitest'
import { DISMISS_MS, isTeaserBlockedPath, pickQuestion, teaserAllowed } from './chatTeaser'

describe('isTeaserBlockedPath', () => {
  it('blocks cart, checkout and order pages in both languages', () => {
    for (const p of ['/cart', '/checkout', '/order', '/order/123', '/en/cart', '/en/checkout/', '/en/order/abc'])
      expect(isTeaserBlockedPath(p)).toBe(true)
  })
  it('allows everything else', () => {
    for (const p of ['/', '/en', '/shop/k5v1', '/en/shop', '/orders-info', '/blog/cart-tips'])
      expect(isTeaserBlockedPath(p)).toBe(false)
  })
})
describe('teaserAllowed', () => {
  const base = { path: '/', hasConversation: false, dismissedAt: null, now: 1_000_000_000_000, hasQuestion: true }
  it('shows on a normal page', () => expect(teaserAllowed(base)).toBe(true))
  it('hides when a conversation exists', () => expect(teaserAllowed({ ...base, hasConversation: true })).toBe(false))
  it('hides without a question', () => expect(teaserAllowed({ ...base, hasQuestion: false })).toBe(false))
  it('hides for 7 days after dismissal, then shows again', () => {
    expect(teaserAllowed({ ...base, dismissedAt: base.now - DISMISS_MS + 1 })).toBe(false)
    expect(teaserAllowed({ ...base, dismissedAt: base.now - DISMISS_MS })).toBe(true)
  })
  it('hides on blocked paths', () => expect(teaserAllowed({ ...base, path: '/checkout' })).toBe(false))
})
describe('pickQuestion', () => {
  const qs = [{ id: 'a' }, { id: 'b' }, { id: 'c' }]
  it('keeps the stored question', () => expect(pickQuestion(qs, 'b', () => 0)).toEqual({ id: 'b' }))
  it('picks randomly when nothing is stored', () => expect(pickQuestion(qs, null, () => 0.7)).toEqual({ id: 'c' }))
  it('picks again when the stored one was disabled', () => expect(pickQuestion(qs, 'zzz', () => 0)).toEqual({ id: 'a' }))
  it('returns null for no questions', () => expect(pickQuestion([], 'a')).toBeNull())
})
