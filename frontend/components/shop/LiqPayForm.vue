<script setup lang="ts">
// LiqPay Checkout: a plain form POST of data + signature. Submitted on mount; the button is the fallback.
defineProps<{ checkoutUrl: string; data: string; signature: string }>()
const form = ref<HTMLFormElement>()
onMounted(() => form.value?.submit())
</script>

<template>
  <form ref="form" method="post" :action="checkoutUrl" accept-charset="utf-8" class="liqpay">
    <input type="hidden" name="data" :value="data">
    <input type="hidden" name="signature" :value="signature">
    <p>{{ $t('checkout.redirecting') }}</p>
    <button class="btn" type="submit">{{ $t('checkout.pay') }}</button>
  </form>
</template>

<style scoped>
.liqpay p { color: var(--ink-2); margin: 0 0 20px; }
</style>
