<script setup lang="ts">
const props = defineProps<{ slug: string }>()
const { t } = useI18n()
const f = reactive({ name: '', phone: '', email: '', message: '', website: '' })
const cv = ref<File | null>(null)
const errors = reactive<Record<string, string>>({})

const { pending, error, done, run } = useForm<typeof f>(async (v) => {
  const body = new FormData()
  for (const [k, val] of Object.entries(v)) body.append(k, val)
  body.append('vacancySlug', props.slug)
  if (cv.value) body.append('cv', cv.value)
  await $fetch(apiUrl('/applications'), { method: 'POST', body })
})

function onFile(e: Event) {
  cv.value = (e.target as HTMLInputElement).files?.[0] ?? null
  const problem = cvProblem(cv.value)
  errors.cv = problem ? t(`vacancies.cv${problem === 'tooBig' ? 'TooBig' : 'Type'}`) : ''
}

function submit() {
  errors.name = f.name.trim().length < 2 ? t('form.required') : ''
  errors.phone = f.phone.replace(/\D/g, '').length < 7 ? t('form.phoneInvalid') : ''
  if (errors.name || errors.phone || errors.cv) return
  run(f)
}
</script>

<template>
  <section class="apply">
    <h2>{{ $t('vacancies.apply') }}</h2>
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
        <textarea v-model="f.message" name="message" rows="4" maxlength="4000" />
      </label>
      <label class="field">
        <span>{{ $t('vacancies.cv') }}</span>
        <input type="file" name="cv" accept=".pdf,.doc,.docx,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document" @change="onFile">
        <em v-if="errors.cv">{{ errors.cv }}</em>
      </label>
      <label class="hp" aria-hidden="true"><span>Website</span><input v-model="f.website" name="website" tabindex="-1" autocomplete="off"></label>
      <p v-if="error" class="form-error">{{ error }}</p>
      <button class="btn" type="submit" :disabled="pending">{{ pending ? $t('form.sending') : $t('form.send') }}</button>
    </form>
  </section>
</template>

<style scoped>
.apply { margin-top: 56px; padding-top: 40px; border-top: 1px solid var(--line); }
.apply h2 { font-size: 26px; margin-bottom: 24px; }
input[type="file"] { padding: 10px; }
</style>
