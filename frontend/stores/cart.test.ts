import { describe, it, expect } from 'vitest'
import { cartTotals, lineKey, mergeLine, parseCart, setLineQty, type CartLine } from './cart'

const k5 = { productId: 'p-k5', slug: 'k5v1', name: 'Вітрогенератор 5кВт', image: null, unitPrice: 295000, optionValueIds: ['m12', 'z1'], optionsText: 'Щогла: 12м. щогла, Зона встановлення: Зона 1' }
const blade = { productId: 'p-blade', slug: 'lopat-gelokoyidna', name: 'Лопать', image: null, unitPrice: 6000, optionValueIds: [], optionsText: '' }

describe('lineKey', () => {
  it('is the product id plus the sorted option values', () => {
    expect(lineKey('p-k5', ['z1', 'm12'])).toBe('p-k5|m12,z1')
    expect(lineKey('p-blade', [])).toBe('p-blade|')
  })
})

describe('mergeLine', () => {
  it('appends a new key and adds qty to an existing one', () => {
    let items: CartLine[] = []
    items = mergeLine(items, k5, 1)
    items = mergeLine(items, blade, 1)
    items = mergeLine(items, k5, 1)
    expect(items.map(l => [l.key, l.qty])).toEqual([['p-k5|m12,z1', 2], ['p-blade|', 1]])
  })
  it('keeps different option sets as different lines', () => {
    const items = mergeLine(mergeLine([], k5, 1), { ...k5, optionValueIds: ['m0', 'z0'], unitPrice: 295000 }, 1)
    expect(items).toHaveLength(2)
  })
})

describe('cartTotals', () => {
  it('sums qty and unitPrice·qty', () => {
    const items = mergeLine(mergeLine([], k5, 2), blade, 1)
    expect(cartTotals(items)).toEqual({ count: 3, subtotal: 596000 })
  })
})

describe('parseCart', () => {
  it('reads back what the store wrote', () => {
    const items = mergeLine([], k5, 1)
    expect(parseCart(JSON.stringify(items))).toEqual(items)
  })
  it('treats anything that is not a list of lines as an empty cart', () => {
    expect(parseCart(null)).toEqual([])
    expect(parseCart('null')).toEqual([])
    expect(parseCart('{}')).toEqual([])
    expect(parseCart('garbage')).toEqual([])
    expect(parseCart('[{"key":"x"},{"nope":1}]')).toEqual([])
  })
})

describe('setLineQty', () => {
  it('changes the qty and removes the line at zero', () => {
    const items = mergeLine(mergeLine([], k5, 1), blade, 1)
    expect(setLineQty(items, 'p-blade|', 3).find(l => l.key === 'p-blade|')?.qty).toBe(3)
    expect(setLineQty(items, 'p-blade|', 0).map(l => l.key)).toEqual(['p-k5|m12,z1'])
  })
})
