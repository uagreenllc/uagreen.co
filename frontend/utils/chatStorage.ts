export interface StoredChat {
  conversationId: string
  token: string
}

const KEY = 'uag_chat'
const QUESTION_KEY = 'uag_chat_q'
const DISMISSED_KEY = 'uag_chat_dismissed'

/** localStorage → chat record; anything that is not a valid record is no record. */
export function parseStoredChat(raw: string | null): StoredChat | null {
  if (!raw) return null
  try {
    const v: unknown = JSON.parse(raw)
    if (!v || typeof v !== 'object' || Array.isArray(v)) return null
    const { conversationId, token } = v as Record<string, unknown>
    return typeof conversationId === 'string' && typeof token === 'string' ? { conversationId, token } : null
  } catch {
    return null
  }
}

// Every access is guarded: private mode, blocked storage and SSR (no localStorage) must not throw.
const read = (key: string): string | null => {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}
const write = (key: string, value: string | null) => {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch {
    // ignored: the widget works without persistence
  }
}

export const chatStorage = {
  load: (): StoredChat | null => parseStoredChat(read(KEY)),
  save: (c: StoredChat) => write(KEY, JSON.stringify({ conversationId: c.conversationId, token: c.token })),
  clear: () => write(KEY, null),
  questionId: (): string | null => read(QUESTION_KEY),
  setQuestionId: (id: string) => write(QUESTION_KEY, id),
  dismissedAt: (): number | null => {
    const raw = read(DISMISSED_KEY)
    const n = raw ? Number(raw) : NaN
    return Number.isFinite(n) ? n : null
  },
  setDismissedAt: (ms: number) => write(DISMISSED_KEY, String(ms)),
}
