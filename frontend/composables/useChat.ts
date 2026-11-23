import type { ChatMsg, ChatQuestion } from '~/types/chat'
import { chatStorage } from '~/utils/chatStorage'

const POLL_OPEN_MS = 3000
const POLL_CLOSED_MS = 30000

const statusOf = (e: unknown): number | undefined => {
  const x = e as { statusCode?: number; status?: number; response?: { status?: number } }
  return x?.statusCode ?? x?.status ?? x?.response?.status
}

/** Client-only chat state: questions, conversation, polling. Every API failure degrades, none throws. */
export function useChat() {
  const { locale } = useI18n()
  const route = useRoute()

  const enabled = ref(false)
  const questions = ref<ChatQuestion[]>([])
  const messages = ref<ChatMsg[]>([])
  const isOpen = ref(false)
  const unread = ref(0)
  const sending = ref(false)
  const unavailable = ref(false)
  const contactSaved = ref(false)
  const conv = ref(chatStorage.load())
  const hasConversation = computed(() => conv.value !== null)

  let timer: ReturnType<typeof setTimeout> | undefined
  let stopped = false
  let polling = false

  const headers = () => ({ 'X-Chat-Token': conv.value?.token ?? '' })
  const url = (suffix: string) => apiUrl(`/chat/conversations/${conv.value?.conversationId}${suffix}`)

  function merge(incoming: ChatMsg[]) {
    const known = new Set(messages.value.map(m => m.id))
    const fresh = incoming.filter(m => !known.has(m.id))
    if (!fresh.length) return
    messages.value = [...messages.value, ...fresh]
    if (!isOpen.value) unread.value += fresh.filter(m => !m.fromVisitor).length
  }

  function reset() {
    chatStorage.clear()
    conv.value = null
    messages.value = []
    unread.value = 0
    contactSaved.value = false
    clearTimeout(timer)
  }

  function schedule() {
    clearTimeout(timer)
    if (stopped || !conv.value) return
    timer = setTimeout(() => poll(false), isOpen.value ? POLL_OPEN_MS : POLL_CLOSED_MS)
  }

  async function poll(force: boolean) {
    if (stopped || !conv.value || polling) return
    if (document.hidden && !force) return schedule()
    polling = true
    try {
      const list = await $fetch<ChatMsg[]>(url('/messages'), { headers: headers() })
      unavailable.value = false
      merge(list)
    } catch (e) {
      const s = statusOf(e)
      if (s === 404) {
        reset()
        return
      }
      if (s === 503) unavailable.value = true
    } finally {
      polling = false
    }
    schedule()
  }

  const onVisible = () => {
    if (!document.hidden && conv.value) poll(true)
  }

  async function init() {
    try {
      const r = await $fetch<{ enabled: boolean; questions: ChatQuestion[] }>(apiUrl('/chat/questions'), { query: { lang: locale.value } })
      enabled.value = !!r.enabled
      questions.value = r.questions ?? []
    } catch {
      enabled.value = false
      return
    }
    if (!enabled.value) return
    document.addEventListener('visibilitychange', onVisible)
    if (conv.value) {
      try {
        messages.value = await $fetch<ChatMsg[]>(url('/messages'), { headers: headers() })
      } catch (e) {
        if (statusOf(e) === 404) return reset()
        if (statusOf(e) === 503) unavailable.value = true
      }
      schedule()
    }
  }

  async function send(text: string, questionId?: string): Promise<boolean> {
    const body = text.trim()
    if (sending.value || !body) return false
    sending.value = true
    try {
      if (!conv.value) {
        const r = await $fetch<{ conversationId: string; token: string; messages: ChatMsg[] }>(apiUrl('/chat/conversations'), {
          method: 'POST',
          body: { text: body, lang: locale.value, pageUrl: route.fullPath, questionId, website: '' },
        })
        conv.value = { conversationId: r.conversationId, token: r.token }
        chatStorage.save(conv.value)
        merge(r.messages)
        schedule()
      } else {
        merge([await $fetch<ChatMsg>(url('/messages'), { method: 'POST', body: { text: body }, headers: headers() })])
      }
      unavailable.value = false
      return true
    } catch (e) {
      const s = statusOf(e)
      if (s === 503) unavailable.value = true
      else if (s === 404 && conv.value) reset()
      return false
    } finally {
      sending.value = false
    }
  }

  async function saveContact(name: string, phone: string): Promise<boolean> {
    if (!conv.value) return false
    try {
      await $fetch(url('/contact'), { method: 'PUT', body: { name, phone }, headers: headers() })
      contactSaved.value = true
      return true
    } catch {
      return false
    }
  }

  function recordImpression(id: string) {
    $fetch(apiUrl(`/chat/questions/${id}/impressions`), { method: 'POST' }).catch(() => {})
  }

  function setOpen(v: boolean) {
    isOpen.value = v
    if (v) unread.value = 0
    schedule()
  }

  function stop() {
    stopped = true
    clearTimeout(timer)
    document.removeEventListener('visibilitychange', onVisible)
  }

  return { enabled, questions, messages, isOpen, unread, sending, unavailable, contactSaved, hasConversation, init, send, saveContact, recordImpression, setOpen, stop }
}
