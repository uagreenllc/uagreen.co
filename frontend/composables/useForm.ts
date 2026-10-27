import type { ApiError } from '~/types/api'

/** pending / error / done around one submit; the API's `{ message }` (or a generic line) lands in `error`. */
export function useForm<T>(submit: (values: T) => Promise<void>) {
  const { t } = useI18n()
  const pending = ref(false)
  const error = ref('')
  const done = ref(false)

  async function run(values: T) {
    pending.value = true
    error.value = ''
    try {
      await submit(values)
      done.value = true
    } catch (e) {
      error.value = (e as { data?: ApiError }).data?.message || t('form.failed')
    } finally {
      pending.value = false
    }
  }
  return { pending, error, done, run }
}
