<script setup lang="ts">
import type { AuditBookingRequest, AuditSlots, Order } from '~/types/api'

const localePath = useLocalePath()
const money = useMoney()
const { t, locale } = useI18n()
usePageHead({ title: t('audit.bookTitle') })

const { data: slots } = await useApiFetch<AuditSlots>('audit-slots', '/audit/slots')
const hasDates = computed(() => !!slots.value && !slots.value.unavailable && slots.value.dates.length > 0)

const f = reactive({ date: null as string | null, name: '', phone: '', email: '', address: '', comment: '', offer: false, website: '' })
const errors = reactive<Record<string, string>>({})
watch(() => f.date, () => { errors.date = '' })
const liqPay = ref<Order['liqPay']>(null)
const picked = computed(() => f.date && new Intl.DateTimeFormat(locale.value === 'en' ? 'en-GB' : 'uk-UA',
  { timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long' }).format(new Date(f.date)))

const { pending, error, run } = useForm<typeof f>(async (v) => {
  const body: AuditBookingRequest = {
    name: v.name, phone: v.phone, email: v.email || null, address: v.address, comment: v.comment || null,
    date: v.date!, offerAccepted: v.offer, lang: locale.value, website: v.website,
  }
  try {
    const order = await $fetch<Order>(apiUrl('/audit/bookings'), { method: 'POST', body })
    liqPay.value = order.liqPay
  } catch (e) {
    // Someone took the day while this visitor was typing: fresh calendar, their own words in the error.
    if ((e as { data?: { code?: string } }).data?.code === 'DATE_UNAVAILABLE') {
      f.date = null
      refreshNuxtData('audit-slots')
      throw { data: { code: 'DATE_UNAVAILABLE', message: t('audit.dateTaken') } }
    }
    throw e
  }
})

function submit() {
  errors.date = f.date ? '' : t('audit.pickDate')
  errors.name = f.name.trim().length < 2 ? t('form.required') : ''
  errors.phone = f.phone.replace(/\D/g, '').length < 7 ? t('form.phoneInvalid') : ''
  errors.address = f.address.trim().length < 5 ? t('audit.addressRequired') : ''
  errors.offer = f.offer ? '' : t('audit.offerRequired')
  if (Object.values(errors).some(Boolean)) return
  run(f)
}
</script>

<template>
  <div class="page section-inner book">
    <h1>{{ $t('audit.bookTitle') }}</h1>

    <LiqPayForm v-if="liqPay" v-bind="liqPay" />

    <div v-else-if="!hasDates" class="none">
      <p>{{ $t('audit.noDates') }}</p>
      <NuxtLink class="btn" :to="localePath('/audit/consultation')">{{ $t('audit.consultTitle') }}</NuxtLink>
    </div>

    <form v-else class="cols" novalidate @submit.prevent="submit">
      <section>
        <h2>{{ $t('audit.dateTitle') }}</h2>
        <AuditCalendar v-model="f.date" :slots="slots!" />
        <p v-if="picked" class="picked">{{ $t('audit.picked', { date: picked }) }}</p>
        <em v-if="errors.date" class="err">{{ errors.date }}</em>
      </section>

      <section>
        <h2>{{ $t('audit.dataTitle') }}</h2>
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
        <label class="field">
          <span>{{ $t('audit.address') }}</span>
          <input v-model="f.address" name="address" autocomplete="street-address" maxlength="300" :placeholder="$t('audit.addressHint')">
          <em v-if="errors.address">{{ errors.address }}</em>
        </label>
        <label class="field">
          <span>{{ $t('audit.comment') }}</span>
          <textarea v-model="f.comment" name="comment" rows="3" maxlength="1000" />
        </label>
        <label class="offer">
          <input v-model="f.offer" type="checkbox" name="offer">
          <i18n-t keypath="audit.offer" tag="span" scope="global">
            <template #link><NuxtLink :to="localePath('/publichna-oferta')" target="_blank">{{ $t('audit.offerLink') }}</NuxtLink></template>
          </i18n-t>
        </label>
        <em v-if="errors.offer" class="err">{{ errors.offer }}</em>
        <!-- Honeypot: off-screen, never filled by people. -->
        <label class="hp" aria-hidden="true"><span>Website</span><input v-model="f.website" name="website" tabindex="-1" autocomplete="off"></label>
        <PrivacyNote />
        <p v-if="error" class="form-error">{{ error }}</p>
        <button class="btn" type="submit" :disabled="pending">
          {{ pending ? $t('form.sending') : $t('audit.pay', { price: money(slots!.price) }) }}
        </button>
      </section>
    </form>
  </div>
</template>

<style scoped>
.cols { display: grid; grid-template-columns: minmax(0, 460px) minmax(0, 1fr); gap: 64px; align-items: start; }
h2 { font-size: 22px; margin: 0 0 18px; }
.picked { margin: 16px 0 0; font-weight: 600; color: var(--green); }
.err { display: block; color: var(--copper); font-style: normal; font-size: 14px; margin: 8px 0 0; }
.offer { display: flex; gap: 10px; align-items: baseline; margin: 4px 0 0; cursor: pointer; }
.offer input { accent-color: var(--green); }
.offer a { color: var(--green); }
.offer + .err { margin: 6px 0 0; }
.book :deep(.privacy-note) { margin-top: 16px; }
.none p { color: var(--ink-2); font-size: 18px; max-width: 52ch; margin: 0 0 24px; }
@media (max-width: 900px) { .cols { grid-template-columns: 1fr; gap: 40px; } }
</style>
