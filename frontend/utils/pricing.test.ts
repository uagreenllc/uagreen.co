import { describe, it, expect } from 'vitest'
import { optionsText, unitPrice } from './pricing'

const product = {
  price: 295000,
  options: [
    { id: 'mast', name: 'Щогла', values: [{ id: 'm0', value: 'Без мачти', priceDelta: 0 }, { id: 'm12', value: '12м. щогла', priceDelta: 76000 }] },
    { id: 'zone', name: 'Зона встановлення', values: [{ id: 'z0', value: 'Без встановлення', priceDelta: 0 }, { id: 'z1', value: 'Зона 1', priceDelta: 45000 }] },
  ],
}

describe('unitPrice', () => {
  it('is the base price with nothing selected', () => {
    expect(unitPrice(product, {})).toBe(295000)
  })
  it('adds the chosen values\' deltas', () => {
    expect(unitPrice(product, { mast: 'm12', zone: 'z1' })).toBe(416000)
  })
  it('ignores unknown option or value ids', () => {
    expect(unitPrice(product, { mast: 'nope', colour: 'red' })).toBe(295000)
  })
})

describe('optionsText', () => {
  it('names each chosen value after its option', () => {
    expect(optionsText(product, { mast: 'm12', zone: 'z1' })).toBe('Щогла: 12м. щогла, Зона встановлення: Зона 1')
  })
  it('is empty with nothing selected', () => {
    expect(optionsText(product, {})).toBe('')
  })
})
