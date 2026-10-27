import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'

export interface CartLine {
  key: string
  productId: string
  slug: string
  name: string
  image: string | null
  unitPrice: number
  qty: number
  optionValueIds: string[]
  optionsText: string
}
export type NewLine = Omit<CartLine, 'key' | 'qty'>

/** Same product + same option values = same line. */
export const lineKey = (productId: string, optionValueIds: string[]) =>
  `${productId}|${[...optionValueIds].sort().join(',')}`

export function mergeLine(items: CartLine[], line: NewLine, qty = 1): CartLine[] {
  const key = lineKey(line.productId, line.optionValueIds)
  const i = items.findIndex(l => l.key === key)
  return i < 0 ? [...items, { ...line, key, qty }] : items.map((l, j) => (j === i ? { ...l, qty: l.qty + qty } : l))
}

export function setLineQty(items: CartLine[], key: string, qty: number): CartLine[] {
  return qty <= 0 ? items.filter(l => l.key !== key) : items.map(l => (l.key === key ? { ...l, qty } : l))
}

export function cartTotals(items: CartLine[]) {
  return items.reduce((t, l) => ({ count: t.count + l.qty, subtotal: t.subtotal + l.unitPrice * l.qty }), { count: 0, subtotal: 0 })
}

const STORAGE = 'uagreen.cart'

export const useCart = defineStore('cart', () => {
  const items = ref<CartLine[]>([])
  // false until load() ran in the browser: SSR and the first client render agree on "nothing yet".
  const ready = ref(false)
  const count = computed(() => cartTotals(items.value).count)
  const subtotal = computed(() => cartTotals(items.value).subtotal)

  const add = (line: NewLine, qty = 1) => { items.value = mergeLine(items.value, line, qty) }
  const setQty = (key: string, qty: number) => { items.value = setLineQty(items.value, key, qty) }
  const remove = (key: string) => setQty(key, 0)
  const clear = () => { items.value = [] }

  /** Called once after mount; from then on every change is written back. */
  function load() {
    try {
      items.value = JSON.parse(localStorage.getItem(STORAGE) ?? '[]')
    } catch {
      items.value = []
    }
    ready.value = true
    watch(items, (v) => {
      try { localStorage.setItem(STORAGE, JSON.stringify(v)) } catch { /* private mode: cart lives for the session */ }
    }, { deep: true })
  }

  return { items, ready, count, subtotal, add, setQty, remove, clear, load }
})
