import type { ChatMsg, ChatQuestion } from '~/types/chat'
import { chatStorage, type StoredChat } from '~/utils/chatStorage'

const POLL_OPEN_MS = 3000
const POLL_CLOSED_MS = 30000
const PAGE_URL_MAX = 500 // Gospodar rejects a longer pageUrl, and with it the whole conversation

const statusOf = (e: unknown): number | undefined => {
  const x = e as { statusCode?: number; status?: number; response?: { status?: number } }
  return x?.statusCode ?? x?.status ?? x?.response?.status
}

/** Client-only chat state: questions, conversation, polling. Every API failure degrades, none throws. */
export function useChat() {
  const { locale } = useI18n()
  const route = useRoute()
  // Gospodar speaks uk and en only: every other locale (cs) gets the English questions and conversation.
  const lang = computed(() => (locale.value === 'uk' ? 'uk' : 'en'))

  const enabled = ref(false)
  const questions = ref<ChatQuestion[]>([])
  const messages = ref<ChatMsg[]>([])
  const isOpen = ref(false)
  const unread = ref(0)
  const sending = ref(false)
  const unavailable = ref(false)
  const conv = ref<StoredChat | null>(chatStorage.load())
  const hasConversation = computed(() => conv.value !== null)
  const contactSaved = computed(() => !!conv.value?.contactSaved)

  let timer: ReturnType<typeof setTimeout> | undefined
  let stopped = false
  let polling = false
  let loaded = false // the server's list for this conversation has been shown in this tab
  let sentDuringPoll: ChatMsg[] = [] // the in-flight poll's answer may predate these
  let pendingQuestion: string | undefined // the starter question, until a conversation starts with it

  const headers = () => ({ 'X-Chat-Token': conv.value?.token ?? '' })
  const url = (suffix: string) => apiUrl(`/chat/conversations/${conv.value?.conversationId}${suffix}`)

  function store(patch: Partial<StoredChat>) {
    if (!conv.value) return
    conv.value = { ...conv.value, ...patch }
    chatStorage.save(conv.value)
  }

  /** Everything shown has been seen: remember the newest `at`. */
  function markSeen() {
    const last = messages.value.at(-1)?.at
    if (last && conv.value && conv.value.seenAt !== last) store({ seenAt: last })
  }

  /** The server's list wins: deleted messages go, order follows the server. */
  function replace(list: ChatMsg[]) {
    const seenAt = conv.value?.seenAt
    const known = new Set(messages.value.map(m => m.id))
    // first load: what came after the stored mark; afterwards: ids this tab has not shown yet
    const fresh = loaded
      ? list.filter(m => !known.has(m.id))
      : seenAt ? list.filter(m => Date.parse(m.at) > Date.parse(seenAt)) : []
    if (!isOpen.value) unread.value += fresh.filter(m => !m.fromVisitor).length
    const ids = new Set(list.map(m => m.id))
    messages.value = [...list, ...sentDuringPoll.filter(m => !ids.has(m.id))]
    loaded = true
    if (!seenAt) markSeen() // new conversation, or a record from before seenAt: all seen so far
  }

  function append(m: ChatMsg) {
    if (messages.value.some(x => x.id === m.id)) return
    messages.value = [...messages.value, m]
    if (polling) sentDuringPoll.push(m)
  }

  function reset() {
    chatStorage.clear()
    conv.value = null
    messages.value = []
    unread.value = 0
    loaded = false
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
    const id = conv.value.conversationId
    polling = true
    sentDuringPoll = []
    try {
      const list = await $fetch<ChatMsg[]>(url('/messages'), { headers: headers() })
      if (conv.value?.conversationId !== id) return // reset or restarted meanwhile
      unavailable.value = false
      replace(list)
    } catch (e) {
      const s = statusOf(e)
      if (s === 404 && conv.value?.conversationId === id) return reset()
      if (s === 503) unavailable.value = true
    } finally {
      polling = false
      sentDuringPoll = []
    }
    schedule()
  }

  const onVisible = () => {
    if (!document.hidden && conv.value) poll(true)
  }

  // questions are served in one language: refetch when the visitor switches it
  watch(lang, async (l) => {
    if (!enabled.value) return
    try {
      questions.value = (await $fetch<{ questions: ChatQuestion[] }>(apiUrl('/chat/questions'), { query: { lang: l } })).questions ?? []
    } catch {
      questions.value = []
    }
  })
  watch([isOpen, messages], () => { if (isOpen.value) markSeen() })

  async function init() {
    try {
      const r = await $fetch<{ enabled: boolean; questions: ChatQuestion[] }>(apiUrl('/chat/questions'), { query: { lang: lang.value } })
      enabled.value = !!r.enabled
      questions.value = r.questions ?? []
    } catch {
      enabled.value = false
      return
    }
    if (!enabled.value) return
    document.addEventListener('visibilitychange', onVisible)
    if (conv.value) await poll(true)
  }

  async function send(text: string, o: { questionId?: string; website?: string } = {}): Promise<boolean> {
    const body = text.trim()
    if (sending.value || !body) return false
    if (o.questionId) pendingQuestion = o.questionId
    sending.value = true
    try {
      // another tab may have started a conversation meanwhile: write there, not into a second one
      if (!conv.value) conv.value = chatStorage.load()
      if (!conv.value) {
        const r = await $fetch<{ conversationId: string; token: string; messages: ChatMsg[] }>(apiUrl('/chat/conversations'), {
          method: 'POST',
          body: { text: body, lang: lang.value, pageUrl: route.fullPath.slice(0, PAGE_URL_MAX), questionId: pendingQuestion, website: o.website ?? '' },
        })
        conv.value = { conversationId: r.conversationId, token: r.token }
        chatStorage.save(conv.value)
        pendingQuestion = undefined
        replace(r.messages)
        schedule()
      } else {
        append(await $fetch<ChatMsg>(url('/messages'), { method: 'POST', body: { text: body }, headers: headers() }))
        if (!loaded) poll(true) // taken over from another tab (or the first load failed): fetch the history
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
      store({ contactSaved: true })
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
