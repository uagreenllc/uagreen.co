<script setup lang="ts">
import type { ContentBlock, Order } from '~/types/api'

const route = useRoute()
const localePath = useLocalePath()
const money = useMoney()
const { t } = useI18n()
const id = String(route.params.id)
if (!/^[0-9a-f-]{36}$/i.test(id)) throw createError({ statusCode: 404 })

const [{ data, refresh }, { data: oberigBlock }] = await Promise.all([
  useApiFetch<Order>(`order:${id}`, `/orders/${id}`),
  useApiFetch<ContentBlock[]>('oberig-order', '/content', { keys: 'oberig.order' }),
])
if (!data.value) throw createError({ statusCode: 404 })
// Last good copy: a poll that times out sets `data` to null and must not blank the page.
const order = ref<Order>(data.value)
watch(data, (v) => { if (v) order.value = v })

usePageHead({ title: t('order.title', { n: order.value.number }) })

// The API re-reads monobank while the order waits; poll for up to 16 minutes (pay in parts gives the client 15).
onMounted(() => {
  let left = 192
  const timer = setInterval(async () => {
    if (order.value.status !== 'AwaitingPayment' || --left < 0) return clearInterval(timer)
    await refresh()
  }, 5000)
  onBeforeUnmount(() => clearInterval(timer))
})

// Manual payment: the company requisites from Admin → Платіжні системи, until the order is paid.
const settings = useSettings()
const bank = computed(() => {
  const s = settings.value ?? {}
  const o = order.value
  if (o.paymentMethod !== 'Invoice' || !s.pay_iban || ['Paid', 'Shipped', 'Cancelled'].includes(o.status)) return null
  const purpose = s.pay_purpose ? s.pay_purpose.replaceAll('{n}', String(o.number)) : t('order.bank.purposeDefault', { n: o.number })
  return ([
    ['recipient', s.pay_recipient, s.pay_recipient],
    ['edrpou', s.pay_edrpou, s.pay_edrpou],
    ['iban', s.pay_iban.replace(/(.{4})(?=.)/g, '$1 '), s.pay_iban],
    ['bank', s.pay_bank, s.pay_bank],
    ['amount', money(o.total), String(o.total)],
    ['purpose', purpose, purpose],
  ] as const).filter(([, shown]) => shown).map(([k, shown, copy]) => ({ k, shown, copy: copy! }))
})

const copied = ref('')
async function copy(k: string, text: string, e: Event) {
  try {
    await navigator.clipboard.writeText(text)
    copied.value = k
    setTimeout(() => { if (copied.value === k) copied.value = '' }, 2000)
  }
  catch { // no clipboard (http, blocked): select the value so Ctrl+C works
    const el = (e.target as HTMLElement).previousElementSibling
    if (el) getSelection()?.selectAllChildren(el)
  }
}

const failReasons = ['CLIENT_NOT_FOUND', 'EXCEEDED_SUM_LIMIT', 'EXISTS_OTHER_OPEN_ORDER', 'NOT_ENOUGH_MONEY_FOR_INIT_DEBIT',
  'REJECTED_BY_CLIENT', 'CLIENT_PUSH_TIMEOUT']
function partsNote(o: Order) {
  if (o.status === 'AwaitingPayment') return t('order.partsWaiting')
  if (o.status === 'Paid') return t('order.partsApproved')
  if (o.status === 'Shipped' || o.status === 'Cancelled') return ''
  const sub = o.paymentStatus?.startsWith('FAIL/') ? o.paymentStatus.slice(5) : null
  if (!sub) return ''
  const reason = sub === 'CREATE_ERROR' ? '' : t(`order.partsFail.${failReasons.includes(sub) ? sub : 'other'}`) + ' '
  return reason + t('order.partsFallback')
}

// «Оплатити»: the current monobank page, or a fresh one if it expired / failed / was never made.
const canPay = computed(() => order.value.paymentMethod === 'Card' && order.value.status === 'AwaitingPayment')
const paying = ref(false)
const payError = ref('')
async function pay() {
  paying.value = true
  payError.value = ''
  try {
    const r = await $fetch<{ payUrl: string }>(apiUrl(`/orders/${id}/pay`), { method: 'POST' })
    window.location.href = r.payUrl
  }
  catch {
    payError.value = t('order.payError')
    paying.value = false
    await refresh()
  }
}

const note = computed(() => {
  const o = order.value
  if (o.status === 'Paid' && o.auditDate) {
    const date = formatDate(o.auditDate)
    return o.auditConfirmed ? t('audit.booked', { date, address: o.auditAddress }) : t('audit.paidPending', { date })
  }
  if (o.paymentMethod === 'MonoParts') return partsNote(o)
  if (o.status === 'Paid') return t('order.thanksPaid')
  if (o.status === 'Cancelled') return ''
  if (o.paymentMethod === 'Card') return o.status === 'AwaitingPayment' ? t('order.waiting') : ''
  if (o.paymentMethod === 'Credit') return t('order.thanksCredit')
  return bank.value ? t('order.thanksTransfer') : t('order.thanksInvoice')
})
</script>

<template>
  <div class="page section-inner order">
    <h1>{{ $t('order.title', { n: order.number }) }}</h1>
    <p class="status">
      <span class="pill">{{ $t(`order.status.${order.status}`) }}</span>
      <time :datetime="order.createdAt">{{ formatDate(order.createdAt) }}</time>
    </p>
    <p v-if="note" class="note">{{ note }}</p>
    <div v-if="canPay" class="pay">
      <button class="btn" type="button" :disabled="paying" @click="pay">{{ $t('order.pay') }}</button>
      <p v-if="payError" class="form-error">{{ payError }}</p>
    </div>
    <section v-if="bank" class="bank">
      <h2>{{ $t('order.bank.title') }}</h2>
      <dl>
        <div v-for="r in bank" :key="r.k">
          <dt>{{ $t(`order.bank.${r.k}`) }}</dt>
          <dd>
            <span>{{ r.shown }}</span>
            <button type="button" class="copy" @click="copy(r.k, r.copy, $event)">
              {{ copied === r.k ? $t('order.bank.copied') : $t('order.bank.copy') }}
            </button>
          </dd>
        </div>
      </dl>
    </section>
    <ul class="items">
      <li v-for="(i, k) in order.items" :key="k">
        <span class="n">{{ i.productName }}<small v-if="i.optionsText">{{ i.optionsText }}</small></span>
        <span class="q">× {{ i.qty }}</span>
        <span class="p">{{ money(i.lineTotal) }}</span>
      </li>
    </ul>
    <p class="sum"><span>{{ $t('order.total') }}</span><strong>{{ money(order.total) }}</strong></p>
    <OberigCta v-if="order.status !== 'Cancelled'" variant="order" :block="oberigBlock?.[0]" />
    <p class="help">{{ $t('order.help') }} <NuxtLink class="link" :to="localePath('/contacts')">{{ $t('nav.contacts') }}</NuxtLink></p>
  </div>
</template>

<style scoped>
.order { max-width: 720px; }
.status { display: flex; gap: 16px; align-items: center; margin: -12px 0 20px; color: var(--ink-2); }
.pill { background: var(--surface); color: var(--ink); padding: 6px 12px; border-radius: 999px; font-size: 14px; font-weight: 500; }
.note { font-size: 18px; max-width: 48ch; margin: 0 0 32px; }
.pay { display: flex; flex-wrap: wrap; gap: 16px; align-items: center; margin: -12px 0 32px; }
.pay .form-error { margin: 0; }
.items { list-style: none; margin: 0; padding: 0; }
.items li { display: grid; grid-template-columns: 1fr auto auto; gap: 16px; padding: 12px 0; border-top: 1px solid var(--line); }
.items small { display: block; color: var(--ink-2); font-size: 13px; }
.q { color: var(--ink-2); }
.sum { display: flex; justify-content: space-between; border-top: 1px solid var(--line); padding-top: 16px; margin: 0 0 40px; font-size: 18px; }
.sum strong { font-size: 24px; }
.help { color: var(--ink-2); }
.bank { border: 1px solid var(--line); border-radius: 12px; padding: 24px; margin: 0 0 40px; }
.bank h2 { font-size: 20px; margin: 0 0 12px; }
.bank dl { margin: 0; }
.bank dl > div { display: grid; grid-template-columns: 160px 1fr; gap: 16px; padding: 10px 0; border-top: 1px solid var(--line); }
.bank dt { color: var(--ink-2); }
.bank dd { margin: 0; display: flex; gap: 12px; justify-content: space-between; align-items: baseline; font-weight: 500; overflow-wrap: anywhere; }
.copy { flex: none; background: none; border: 0; padding: 0; color: var(--green); font: inherit; font-size: 14px; cursor: pointer; }
.copy:hover { text-decoration: underline; }
@media (max-width: 600px) { .bank dl > div { grid-template-columns: 1fr; gap: 2px; } }
</style>
