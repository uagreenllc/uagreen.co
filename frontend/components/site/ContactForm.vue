<script setup lang="ts">
const { t } = useI18n()
const f = reactive({ name: '', phone: '', email: '', message: '', website: '' })
const errors = reactive<Record<string, string>>({})

const { pending, error, done, run } = useForm<typeof f>(async (v) => {
  await $fetch(apiUrl('/contact'), { method: 'POST', body: { ...v, email: v.email || null } })
})

function submit() {
  errors.name = f.name.trim().length < 2 ? t('form.required') : ''
  errors.phone = f.phone.replace(/\D/g, '').length < 7 ? t('form.phoneInvalid') : ''
  errors.message = f.message.trim() ? '' : t('form.required')
  if (errors.name || errors.phone || errors.message) return
  run(f)
}
</script>

<template>
  <p v-if="done" class="form-ok">{{ $t('form.sent') }}</p>
  <form v-else novalidate @submit.prevent="submit">
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
      <span>{{ $t('form.message') }}</span>
      <textarea v-model="f.message" name="message" rows="5" maxlength="4000" />
      <em v-if="errors.message">{{ errors.message }}</em>
    </label>
    <!-- Honeypot: off-screen, never filled by people. -->
    <label class="hp" aria-hidden="true"><span>Website</span><input v-model="f.website" name="website" tabindex="-1" autocomplete="off"></label>
    <p v-if="error" class="form-error">{{ error }}</p>
    <button class="btn" type="submit" :disabled="pending">{{ pending ? $t('form.sending') : $t('form.send') }}</button>
  </form>
</template>
