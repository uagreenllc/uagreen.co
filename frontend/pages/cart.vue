<script setup lang="ts">
import { useCart } from '~/stores/cart'

const cart = useCart()
const localePath = useLocalePath()
const money = useMoney()
const { t } = useI18n()
usePageHead({ title: t('cart.title') })
</script>

<template>
  <div class="page section-inner cart">
    <h1>{{ $t('cart.title') }}</h1>
    <template v-if="cart.ready">
      <p v-if="!cart.items.length" class="empty">
        {{ $t('cart.empty') }}<br>
        <NuxtLink class="link" :to="localePath('/shop')">{{ $t('cart.toShop') }}</NuxtLink>
      </p>
      <template v-else>
        <ul class="lines">
          <li v-for="l in cart.items" :key="l.key">
            <NuxtLink :to="localePath(`/shop/${l.slug}`)" class="pic">
              <img v-if="l.image" :src="l.image" :alt="l.name" width="200" height="150">
              <span v-else class="blank" />
            </NuxtLink>
            <div class="what">
              <NuxtLink :to="localePath(`/shop/${l.slug}`)" class="name">{{ l.name }}</NuxtLink>
              <span v-if="l.optionsText" class="opts">{{ l.optionsText }}</span>
              <span class="unit">{{ money(l.unitPrice) }}</span>
            </div>
            <div class="qty" :aria-label="$t('cart.qty')">
              <button type="button" :aria-label="'−'" @click="cart.setQty(l.key, l.qty - 1)">−</button>
              <input type="number" min="1" max="99" :value="l.qty" @change="cart.setQty(l.key, Math.max(1, Number(($event.target as HTMLInputElement).value) || 1))">
              <button type="button" :aria-label="'+'" @click="cart.setQty(l.key, Math.min(99, l.qty + 1))">+</button>
            </div>
            <span class="total">{{ money(l.unitPrice * l.qty) }}</span>
            <button type="button" class="remove" @click="cart.remove(l.key)">{{ $t('cart.remove') }}</button>
          </li>
        </ul>
        <div class="sum">
          <span>{{ $t('cart.subtotal') }}</span>
          <strong>{{ money(cart.subtotal) }}</strong>
        </div>
        <p class="note">{{ $t('checkout.deliveryNote') }}</p>
        <NuxtLink class="btn" :to="localePath('/checkout')">{{ $t('cart.checkout') }}</NuxtLink>
      </template>
    </template>
  </div>
</template>

<style scoped>
.cart { max-width: 960px; }
.empty { color: var(--ink-2); font-size: 18px; line-height: 1.8; }
.lines { list-style: none; margin: 0; padding: 0; }
.lines li { display: grid; grid-template-columns: 120px 1fr auto auto auto; gap: 24px; align-items: center; padding: 20px 0; border-top: 1px solid var(--line); }
.lines li:last-child { border-bottom: 1px solid var(--line); }
.pic img, .blank { width: 120px; aspect-ratio: 4 / 3; object-fit: cover; border-radius: 8px; background: var(--surface); display: block; }
.what { display: grid; gap: 4px; }
.name { font-weight: 500; font-size: 18px; text-decoration: none; }
.name:hover { color: var(--green); }
.opts { color: var(--ink-2); font-size: 14px; }
.unit { color: var(--ink-2); font-size: 14px; }
.qty { display: inline-flex; border: 1px solid var(--line); border-radius: 999px; overflow: hidden; }
.qty button { width: 36px; height: 36px; border: 0; background: #fff; cursor: pointer; font-size: 18px; color: var(--ink); }
.qty button:hover { background: var(--surface); }
.qty input { width: 44px; border: 0; text-align: center; font: inherit; -moz-appearance: textfield; }
.qty input::-webkit-outer-spin-button, .qty input::-webkit-inner-spin-button { -webkit-appearance: none; margin: 0; }
.total { font-weight: 600; font-size: 18px; min-width: 110px; text-align: right; }
.remove { border: 0; background: none; color: var(--ink-2); cursor: pointer; font: inherit; font-size: 14px; text-decoration: underline; }
.remove:hover { color: var(--copper); }
.sum { display: flex; justify-content: space-between; align-items: baseline; max-width: 420px; margin: 28px 0 0 auto; font-size: 18px; }
.sum strong { font-size: 26px; }
.note { color: var(--ink-2); font-size: 14px; text-align: right; margin: 6px 0 24px; }
.cart .btn { float: right; }
@media (max-width: 760px) {
  .lines li { grid-template-columns: 90px 1fr; gap: 12px 16px; }
  .pic img, .blank { width: 90px; }
  .qty, .total, .remove { grid-column: 2; justify-self: start; }
  .total { text-align: left; min-width: 0; }
  .sum, .note { text-align: left; margin-left: 0; }
  .cart .btn { float: none; }
}
</style>
