<script lang="ts">
type TurnstileApi = {
  render(el: HTMLElement, o: Record<string, unknown>): string
  reset(id: string): void
  remove(id: string): void
}
const api = () => (window as unknown as { turnstile?: TurnstileApi }).turnstile
let loading: Promise<void> | undefined

function loadTurnstile(): Promise<void> {
  loading ??= new Promise((resolve, reject) => {
    const s = document.createElement('script')
    s.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit'
    s.async = true
    s.onload = () => resolve()
    s.onerror = () => { loading = undefined; reject(new Error('Turnstile failed to load')) }
    document.head.append(s)
  })
  return loading
}
</script>

<script setup lang="ts">
// Cloudflare Turnstile. No site key (dev) → renders nothing and the API skips the check.
// Tokens are single-use: the form calls reset() after a failed submit.
const token = defineModel<string>({ required: true })
const siteKey = useRuntimeConfig().public.turnstileSiteKey as string
const el = ref<HTMLElement>()
let id: string | undefined

onMounted(async () => {
  if (!siteKey) return
  // Script blocked or offline: the submit fails with the captcha message, nothing else breaks.
  await loadTurnstile().catch(e => console.warn(`[captcha] ${e.message}`))
  if (!api() || !el.value) return
  id = api()!.render(el.value, {
    sitekey: siteKey,
    appearance: 'interaction-only', // invisible unless Cloudflare wants a click
    callback: (t: string) => { token.value = t },
    'expired-callback': () => { token.value = '' },
    'error-callback': () => { token.value = '' },
  })
})
onBeforeUnmount(() => { if (id) api()?.remove(id) })

defineExpose({
  reset() {
    token.value = ''
    if (id) api()?.reset(id)
  },
})
</script>

<template>
  <div ref="el" class="captcha" />
</template>
