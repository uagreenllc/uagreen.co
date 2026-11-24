import { describe, it, expect } from 'vitest'
import uk from '../i18n/locales/uk.json'
import en from '../i18n/locales/en.json'
import cs from '../i18n/locales/cs.json'

// A key added to uk.json but not to the other locales shows the raw key there — or crashes the page (tm() on a list).
const keys = (o: unknown, p = ''): string[] =>
  o && typeof o === 'object' ? Object.entries(o).flatMap(([k, v]) => keys(v, `${p}${k}.`)) : [p.slice(0, -1)]

describe('locales', () => {
  for (const [name, messages] of Object.entries({ en, cs })) {
    it(`${name} has every uk key`, () => {
      const have = new Set(keys(messages))
      expect(keys(uk).filter(k => !have.has(k))).toEqual([])
    })
  }
})
