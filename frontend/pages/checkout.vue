<script setup lang="ts">
import type { CreateOrder, DeliveryMethod, Order, PaymentMethod } from '~/types/api'
import { useCart } from '~/stores/cart'

const cart = useCart()
const localePath = useLocalePath()
const money = useMoney()
const { t, locale } = useI18n()
usePageHead({ title: t('checkout.title') })

const deliveries: DeliveryMethod[] = ['NovaPoshta', 'Ukrposhta', 'Pickup']
const payments: PaymentMethod[] = ['LiqPay', 'Invoice', 'Credit']
const f = reactive({
  name: '', phone: '', email: '', delivery: 'NovaPoshta' as DeliveryMethod, city: '', warehouse: '',
  payment: 'LiqPay' as PaymentMethod, comment: '', website: '',
})
const errors = reactive<Record<string, string>>({})
const needsAddress = computed(() => f.delivery !== 'Pickup')
const liqPay = ref<Order['liqPay']>(null)

const { pending, error, run } = useForm<typeof f>(async (v) => {
  const body: CreateOrder = {
    customer: { name: v.name, phone: v.phone, email: v.email || null },
    delivery: { method: v.delivery, city: needsAddress.value ? v.city : null, warehouse: needsAddress.value ? v.warehouse : null },
    paymentMethod: v.payment,
    comment: v.comment || null,
    items: cart.items.map(l => ({ productId: l.productId, qty: l.qty, optionValueIds: l.optionValueIds })),
    lang: locale.value,
    website: v.website,
  }
  const order = await $fetch<Order>(apiUrl('/orders'), { method: 'POST', body })
  cart.clear()
  if (order.liqPay) liqPay.value = order.liqPay
  else await navigateTo(localePath(`/order/${order.id}`))
})

function submit() {
  errors.name = f.name.trim().length < 2 ? t('form.required') : ''
  errors.phone = f.phone.replace(/\D/g, '').length < 7 ? t('form.phoneInvalid') : ''
  errors.city = needsAddress.value && !f.city.trim() ? t('form.required') : ''
  errors.warehouse = needsAddress.value && !f.warehouse.trim() ? t('form.required') : ''
  if (Object.values(errors).some(Boolean)) return
  run(f)
}
</script>

<template>
  <div class="page section-inner checkout">
    <h1>{{ $t('checkout.title') }}</h1>

    <LiqPayForm v-if="liqPay" v-bind="liqPay" />

    <template v-else-if="cart.ready">
      <p v-if="!cart.items.length" class="empty">
        {{ $t('cart.empty') }}<br>
        <NuxtLink class="link" :to="localePath('/shop')">{{ $t('cart.toShop') }}</NuxtLink>
      </p>

      <div v-else class="cols">
        <form novalidate @submit.prevent="submit">
          <h2>{{ $t('checkout.customer') }}</h2>
          <label class="field">
            <span>{{ $t('form.name') }}</span>
            <input v-model="f.name" name="name" autocomplete="name" maxlength="120">
            <em v-if="errors.name">{{ errors.name }}</em>
          </label>
          <label class="field">
            <span>{{ $t('form.phone') }}</span>
            <input v-model="f.phone" name="phone" type="tel" autocomplete="tel" maxlength="30">
            <em v-if="errors.phone">{{ errors.phone }}</em>
          </label>
          <label class="field">
            <span>{{ $t('form.email') }}</span>
            <input v-model="f.email" name="email" type="email" autocomplete="email" maxlength="200">
          </label>

          <h2>{{ $t('checkout.delivery') }}</h2>
          <div class="choices">
            <label v-for="d in deliveries" :key="d"><input v-model="f.delivery" type="radio" name="delivery" :value="d"> {{ $t(`checkout.delivery_${d}`) }}</label>
          </div>
          <template v-if="needsAddress">
            <label class="field">
              <span>{{ $t('checkout.city') }}</span>
              <input v-model="f.city" name="city" autocomplete="address-level2" maxlength="120">
              <em v-if="errors.city">{{ errors.city }}</em>
            </label>
            <label class="field">
              <span>{{ $t('checkout.warehouse') }}</span>
              <input v-model="f.warehouse" name="warehouse" maxlength="200">
              <em v-if="errors.warehouse">{{ errors.warehouse }}</em>
            </label>
          </template>
          <p class="hint">{{ $t('checkout.deliveryNote') }}</p>

          <h2>{{ $t('checkout.payment') }}</h2>
          <div class="choices">
            <label v-for="pm in payments" :key="pm">
              <input v-model="f.payment" type="radio" name="payment" :value="pm">
              <span>{{ $t(`checkout.payment_${pm}`) }}<small>{{ $t(`checkout.payment_${pm}_hint`) }}</small></span>
            </label>
          </div>

          <label class="field">
            <span>{{ $t('checkout.comment') }}</span>
            <textarea v-model="f.comment" name="comment" rows="3" maxlength="2000" />
          </label>
          <label class="hp" aria-hidden="true"><span>Website</span><input v-model="f.website" name="website" tabindex="-1" autocomplete="off"></label>

          <p v-if="error" class="form-error">{{ error }}</p>
          <button class="btn" type="submit" :disabled="pending">{{ pending ? $t('form.sending') : $t('checkout.place') }}</button>
        </form>

        <aside class="summary">
          <h2>{{ $t('checkout.summary') }}</h2>
          <ul>
            <li v-for="l in cart.items" :key="l.key">
              <span class="n">{{ l.name }}<small v-if="l.optionsText">{{ l.optionsText }}</small></span>
              <span class="q">× {{ l.qty }}</span>
              <span class="p">{{ money(l.unitPrice * l.qty) }}</span>
            </li>
          </ul>
          <p class="sum"><span>{{ $t('cart.subtotal') }}</span><strong>{{ money(cart.subtotal) }}</strong></p>
          <NuxtLink class="link" :to="localePath('/cart')">{{ $t('checkout.editCart') }}</NuxtLink>
        </aside>
      </div>
    </template>
  </div>
</template>

<style scoped>
.empty { color: var(--ink-2); font-size: 18px; line-height: 1.8; }
.cols { display: grid; grid-template-columns: minmax(0, 1fr) 360px; gap: 64px; align-items: start; }
h2 { font-size: 22px; margin: 32px 0 16px; }
form h2:first-child { margin-top: 0; }
.choices { display: grid; gap: 10px; margin: 0 0 18px; }
.choices label { display: flex; gap: 10px; align-items: baseline; cursor: pointer; }
.choices input { accent-color: var(--green); }
.choices small { display: block; color: var(--ink-2); font-size: 14px; }
.hint { color: var(--ink-2); font-size: 14px; margin: -6px 0 18px; }
.summary { position: sticky; top: 96px; padding: 24px; border: 1px solid var(--line); border-radius: 12px; }
.summary h2 { margin-top: 0; }
.summary ul { list-style: none; margin: 0; padding: 0; }
.summary li { display: grid; grid-template-columns: 1fr auto auto; gap: 12px; padding: 10px 0; border-top: 1px solid var(--line); font-size: 15px; }
.summary .n small { display: block; color: var(--ink-2); font-size: 13px; }
.summary .q { color: var(--ink-2); }
.summary .p { font-weight: 500; }
.sum { display: flex; justify-content: space-between; align-items: baseline; margin: 16px 0; border-top: 1px solid var(--line); padding-top: 16px; }
.sum strong { font-size: 22px; }
@media (max-width: 900px) { .cols { grid-template-columns: 1fr; gap: 32px; } .summary { position: static; order: -1; } }
</style>
