<script setup lang="ts">
import type { Order } from '~/types/api'

const route = useRoute()
const localePath = useLocalePath()
const money = useMoney()
const { t } = useI18n()
const id = String(route.params.id)
if (!/^[0-9a-f-]{36}$/i.test(id)) throw createError({ statusCode: 404 })

const { data, refresh } = await useApiFetch<Order>(`order:${id}`, `/orders/${id}`)
if (!data.value) throw createError({ statusCode: 404 })
// Last good copy: a poll that times out sets `data` to null and must not blank the page.
const order = ref<Order>(data.value)
watch(data, (v) => { if (v) order.value = v })

usePageHead({ title: t('order.title', { n: order.value.number }) })

// LiqPay confirms through a server callback; poll for up to two minutes while the customer waits.
onMounted(() => {
  let left = 24
  const timer = setInterval(async () => {
    if (order.value.status !== 'AwaitingPayment' || --left < 0) return clearInterval(timer)
    await refresh()
  }, 5000)
  onBeforeUnmount(() => clearInterval(timer))
})

const note = computed(() => {
  const o = order.value
  if (o.status === 'Paid') return t('order.thanksPaid')
  if (o.status === 'Cancelled') return ''
  if (o.paymentMethod === 'LiqPay') return t('order.waiting')
  if (o.paymentMethod === 'Credit') return t('order.thanksCredit')
  return t('order.thanksInvoice')
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
    <ul class="items">
      <li v-for="(i, k) in order.items" :key="k">
        <span class="n">{{ i.productName }}<small v-if="i.optionsText">{{ i.optionsText }}</small></span>
        <span class="q">× {{ i.qty }}</span>
        <span class="p">{{ money(i.lineTotal) }}</span>
      </li>
    </ul>
    <p class="sum"><span>{{ $t('order.total') }}</span><strong>{{ money(order.total) }}</strong></p>
    <p class="help">{{ $t('order.help') }} <NuxtLink class="link" :to="localePath('/contacts')">{{ $t('nav.contacts') }}</NuxtLink></p>
  </div>
</template>

<style scoped>
.order { max-width: 720px; }
.status { display: flex; gap: 16px; align-items: center; margin: -12px 0 20px; color: var(--ink-2); }
.pill { background: var(--surface); color: var(--ink); padding: 6px 12px; border-radius: 999px; font-size: 14px; font-weight: 500; }
.note { font-size: 18px; max-width: 48ch; margin: 0 0 32px; }
.items { list-style: none; margin: 0; padding: 0; }
.items li { display: grid; grid-template-columns: 1fr auto auto; gap: 16px; padding: 12px 0; border-top: 1px solid var(--line); }
.items small { display: block; color: var(--ink-2); font-size: 13px; }
.q { color: var(--ink-2); }
.sum { display: flex; justify-content: space-between; border-top: 1px solid var(--line); padding-top: 16px; margin: 0 0 40px; font-size: 18px; }
.sum strong { font-size: 24px; }
.help { color: var(--ink-2); }
</style>
