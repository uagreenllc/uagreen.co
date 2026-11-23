import { describe, it, expect } from 'vitest'
import { oberigLinks } from './useOberig'

describe('oberigLinks', () => {
  it('defaults to the production portal with every placement on', () => {
    for (const s of [null, {}, { 'oberig.url': '  ' }] as (Record<string, string> | null)[]) {
      const o = oberigLinks(s)
      expect(o.url).toBe('https://portal.uagreen.co')
      expect(o.calculatorUrl).toBe('https://portal.uagreen.co/#/wind-calculator')
      expect(['home', 'product', 'order', 'footer'].every(p => o.on(p as never))).toBe(true)
    }
  })
  it('turns a placement off only for "0"', () => {
    const o = oberigLinks({ 'oberig.home': '0', 'oberig.product': '1', 'oberig.order': '', 'oberig.footer': 'false' })
    expect(o.on('home')).toBe(false)
    expect(o.on('product')).toBe(true)
    expect(o.on('order')).toBe(true)
    expect(o.on('footer')).toBe(true)
  })
  it('trims a trailing slash and derives the calculator from the portal url', () => {
    const o = oberigLinks({ 'oberig.url': 'https://staging.portal.test/ ' })
    expect(o.url).toBe('https://staging.portal.test')
    expect(o.calculatorUrl).toBe('https://staging.portal.test/#/wind-calculator')
  })
  it('uses an explicit calculator url as is', () => {
    expect(oberigLinks({ 'oberig.calculator_url': 'https://calc.test/#/x' }).calculatorUrl).toBe('https://calc.test/#/x')
  })
})
