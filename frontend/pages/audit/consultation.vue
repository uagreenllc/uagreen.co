<script setup lang="ts">
const { t } = useI18n()
const localePath = useLocalePath()
usePageHead({ title: t('audit.consultTitle') })

const f = reactive({ phone: '', website: '' })
const phoneError = ref('')
const { pending, error, done, run } = useForm<typeof f>(async (v) => {
  await $fetch(apiUrl('/audit/consultation'), { method: 'POST', body: v })
})

function submit() {
  phoneError.value = f.phone.replace(/\D/g, '').length < 7 ? t('form.phoneInvalid') : ''
  if (!phoneError.value) run(f)
}
</script>

<template>
  <div class="page section-inner consult">
    <h1>{{ $t('audit.consultTitle') }}</h1>
    <template v-if="done">
      <p class="form-ok">{{ $t('audit.consultDone') }}</p>
      <NuxtLink class="link" :to="localePath('/audit')">{{ $t('audit.back') }}</NuxtLink>
    </template>
    <form v-else novalidate @submit.prevent="submit">
      <p class="lead">{{ $t('audit.consultLead') }}</p>
      <label class="field">
        <span>{{ $t('form.phone') }}</span>
        <input v-model="f.phone" name="phone" type="tel" autocomplete="tel" maxlength="30">
        <em v-if="phoneError">{{ phoneError }}</em>
      </label>
      <!-- Honeypot: off-screen, never filled by people. -->
      <label class="hp" aria-hidden="true"><span>Website</span><input v-model="f.website" name="website" tabindex="-1" autocomplete="off"></label>
      <PrivacyNote />
      <p v-if="error" class="form-error">{{ error }}</p>
      <button class="btn" type="submit" :disabled="pending">{{ pending ? $t('form.sending') : $t('audit.callMe') }}</button>
    </form>
  </div>
</template>

<style scoped>
.consult { max-width: 640px; }
.lead { color: var(--ink-2); font-size: 18px; margin: -12px 0 28px; }
</style>
