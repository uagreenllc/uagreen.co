import type { Product } from '~/types/api'

type Priced = Pick<Product, 'price' | 'options'>
/** optionId → chosen valueId */
export type Selection = Record<string, string>

function chosen(p: Priced, selected: Selection) {
  return p.options.flatMap((o) => {
    const v = o.values.find(x => x.id === selected[o.id])
    return v ? [{ option: o, value: v }] : []
  })
}

/** First value of every option that has values; a group with none (the admin API allows it) is skipped. */
export function defaultSelection(options: Priced['options']): Selection {
  return Object.fromEntries(options.filter(o => o.values.length).map(o => [o.id, o.values[0]!.id]))
}

/** Base price plus the deltas of the chosen values; unknown ids are ignored. The server recomputes this. */
export function unitPrice(p: Priced, selected: Selection): number {
  return chosen(p, selected).reduce((sum, c) => sum + c.value.priceDelta, p.price)
}

/** "Щогла: 12м. щогла, Зона встановлення: Зона 1" — what the cart and the order show. */
export function optionsText(p: Priced, selected: Selection, sep = ', '): string {
  return chosen(p, selected).map(c => `${c.option.name}: ${c.value.value}`).join(sep)
}
