<script setup lang="ts">
import { chatStorage } from '~/utils/chatStorage'
import { TEASER_DELAY_MS, isTeaserBlockedPath, pickQuestion, teaserAllowed } from '~/utils/chatTeaser'
import type { ChatQuestion } from '~/types/chat'

const { t } = useI18n()
const route = useRoute()
const chat = useChat()
const { enabled, questions, messages, isOpen, unread, sending, unavailable, contactSaved, hasConversation } = chat

const text = ref('')
const failed = ref(false)
const teaser = ref<ChatQuestion | null>(null)
const contact = reactive({ name: '', phone: '' })
const contactError = ref(false)
const btn = ref<HTMLButtonElement>()
const input = ref<HTMLTextAreaElement>()
const list = ref<HTMLElement>()
let teaserTimer: ReturnType<typeof setTimeout> | undefined

const hasVisitorMsg = computed(() => messages.value.some(m => m.fromVisitor))

function scrollDown() {
  nextTick(() => { if (list.value) list.value.scrollTop = list.value.scrollHeight })
}
watch(() => messages.value.length, scrollDown)

async function open() {
  teaser.value = null
  chat.setOpen(true)
  await nextTick()
  input.value?.focus()
  scrollDown()
}
function close() {
  chat.setOpen(false)
  nextTick(() => btn.value?.focus())
}

async function submit() {
  if (sending.value) return
  failed.value = false
  const v = text.value
  if (await chat.send(v)) { if (text.value === v) text.value = '' }
  else if (!unavailable.value && v.trim()) failed.value = true
}
function onEnter(e: KeyboardEvent) {
  if (e.shiftKey || e.isComposing) return
  e.preventDefault()
  submit()
}

const answerText = ref('')
async function answer() {
  const q = teaser.value
  const v = answerText.value.trim()
  if (!q || !v || sending.value) return
  answerText.value = ''
  open()
  failed.value = false
  if (!(await chat.send(v, q.id)) && !unavailable.value) {
    text.value = v
    failed.value = true
  }
}
function onTeaserEnter(e: KeyboardEvent) {
  if (e.shiftKey || e.isComposing) return
  e.preventDefault()
  answer()
}
function dismiss() {
  chatStorage.setDismissedAt(Date.now())
  teaser.value = null
}

async function saveContact() {
  contactError.value = false
  if (contact.phone.replace(/\D/g, '').length < 7) { contactError.value = true; return }
  if (!(await chat.saveContact(contact.name.trim(), contact.phone.trim()))) contactError.value = true
}

onMounted(async () => {
  await chat.init()
  if (!enabled.value) return
  teaserTimer = setTimeout(() => {
    const q = pickQuestion(questions.value, chatStorage.questionId())
    if (!q || isOpen.value || !teaserAllowed({ path: route.path, hasConversation: hasConversation.value, dismissedAt: chatStorage.dismissedAt(), now: Date.now(), hasQuestion: true })) return
    chatStorage.setQuestionId(q.id)
    chat.recordImpression(q.id)
    teaser.value = q
  }, TEASER_DELAY_MS)
})
onBeforeUnmount(() => {
  clearTimeout(teaserTimer)
  chat.stop()
})
</script>

<template>
  <div v-if="enabled" class="chat">
    <section
      v-if="isOpen"
      class="chat-panel"
      role="dialog"
      :aria-label="t('chat.open')"
      tabindex="-1"
      @keydown.esc="close"
    >
      <header class="chat-head">
        <strong>UA Green</strong>
        <button type="button" class="chat-x" :aria-label="t('chat.close')" @click="close">×</button>
      </header>

      <div ref="list" class="chat-list" aria-live="polite">
        <p class="chat-msg them"><small>UA Green</small>{{ t('chat.greeting') }}</p>
        <p v-for="m in messages" :key="m.id" class="chat-msg" :class="m.fromVisitor ? 'me' : 'them'">
          <small v-if="!m.fromVisitor">{{ m.authorName ?? 'UA Green' }}</small>{{ m.text }}
        </p>
      </div>

      <form v-if="hasVisitorMsg && !contactSaved" class="chat-contact" novalidate @submit.prevent="saveContact">
        <p>{{ t('chat.contactPrompt') }}</p>
        <input v-model="contact.name" :placeholder="t('chat.contactName')" :aria-label="t('chat.contactName')" autocomplete="name" maxlength="120">
        <input v-model="contact.phone" type="tel" :placeholder="t('chat.contactPhone')" :aria-label="t('chat.contactPhone')" autocomplete="tel" maxlength="30" :aria-invalid="contactError">
        <button class="btn" type="submit">{{ t('chat.contactSave') }}</button>
      </form>
      <p v-else-if="contactSaved" class="chat-note ok">{{ t('chat.contactSaved') }}</p>

      <div v-if="unavailable && !hasConversation" class="chat-fallback">
        <p class="chat-note">{{ t('chat.unavailable') }}</p>
        <ContactForm />
      </div>
      <template v-else>
        <p v-if="unavailable" class="chat-note">{{ t('chat.unavailable') }}</p>
        <p v-if="failed" class="chat-note err" role="alert">{{ t('chat.failed') }}</p>
        <form class="chat-form" @submit.prevent="submit">
          <label class="sr-only" for="chat-input">{{ t('chat.placeholder') }}</label>
          <textarea id="chat-input" ref="input" v-model="text" rows="2" maxlength="2000" :placeholder="t('chat.placeholder')" @keydown.enter="onEnter" />
          <button class="btn" type="submit" :disabled="sending || !text.trim()">{{ t('chat.send') }}</button>
        </form>
      </template>
    </section>

    <div v-if="teaser && !isOpen && !isTeaserBlockedPath(route.path)" class="chat-teaser">
      <button type="button" class="chat-x" :aria-label="t('chat.close')" @click="dismiss">×</button>
      <p>{{ teaser.text }}</p>
      <label class="sr-only" for="chat-teaser-input">{{ t('chat.placeholder') }}</label>
      <textarea id="chat-teaser-input" v-model="answerText" rows="2" maxlength="2000" :placeholder="t('chat.placeholder')" @keydown.enter="onTeaserEnter" />
      <button type="button" class="btn" :disabled="sending || !answerText.trim()" @click="answer">{{ t('chat.send') }}</button>
    </div>

    <button ref="btn" type="button" class="chat-fab" :aria-label="t('chat.open')" :aria-expanded="isOpen" @click="isOpen ? close() : open()">
      <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z" /></svg>
      <span v-if="unread" class="chat-badge">{{ unread }}</span>
    </button>
  </div>
</template>

<style scoped>
.chat { position: fixed; right: 20px; bottom: 20px; z-index: var(--z-modal); display: flex; flex-direction: column; align-items: flex-end; gap: 12px; font-size: 15px; }
.chat-fab { position: relative; width: 56px; height: 56px; border-radius: 50%; border: 0; background: var(--green); color: #fff; cursor: pointer; display: grid; place-items: center; box-shadow: 0 6px 20px rgb(31 42 34 / 0.25); }
.chat-fab:hover { background: var(--green-2); }
.chat-badge { position: absolute; top: -4px; right: -4px; min-width: 20px; height: 20px; padding: 0 5px; border-radius: 10px; background: var(--copper); color: #fff; font-size: 12px; font-weight: 600; display: grid; place-items: center; }
.chat-panel, .chat-teaser { background: #fff; border: 1px solid var(--line); border-radius: 14px; box-shadow: 0 12px 40px rgb(31 42 34 / 0.2); }
.chat-panel { width: 360px; max-width: calc(100vw - 40px); max-height: min(560px, calc(100vh - 110px)); display: flex; flex-direction: column; overflow: hidden; }
.chat-head { display: flex; justify-content: space-between; align-items: center; padding: 12px 16px; background: var(--green); color: #fff; }
.chat-x { border: 0; background: none; font-size: 24px; line-height: 1; cursor: pointer; color: inherit; padding: 0 4px; }
.chat-list { flex: 1; overflow-y: auto; padding: 12px; display: flex; flex-direction: column; gap: 8px; min-height: 120px; }
.chat-msg { margin: 0; max-width: 85%; padding: 8px 12px; border-radius: 12px; white-space: pre-wrap; overflow-wrap: anywhere; }
.chat-msg small { display: block; font-size: 12px; color: var(--ink-2); }
.chat-msg.me { align-self: flex-end; background: var(--green); color: #fff; }
.chat-msg.them { align-self: flex-start; background: var(--soil); }
.chat-contact { display: grid; gap: 8px; padding: 10px 12px; border-top: 1px solid var(--line); }
.chat-contact p { margin: 0; font-size: 14px; color: var(--ink-2); }
.chat-contact input, .chat-form textarea { font: inherit; border: 1px solid var(--line); border-radius: 8px; padding: 8px 10px; min-width: 0; }
.chat-contact input[aria-invalid='true'] { border-color: var(--copper); }
.chat-contact .btn, .chat-form .btn { padding: 10px 16px; font-size: 15px; }
.chat-form { display: flex; gap: 8px; align-items: flex-end; padding: 10px 12px; border-top: 1px solid var(--line); }
.chat-form textarea { flex: 1; resize: none; }
.chat-note { margin: 0; padding: 8px 12px; font-size: 14px; color: var(--ink-2); }
.chat-note.ok { color: var(--green); }
.chat-note.err { color: var(--copper); }
.chat-fallback { overflow-y: auto; padding: 0 12px 12px; border-top: 1px solid var(--line); }
.chat-teaser { position: relative; width: 280px; max-width: calc(100vw - 40px); padding: 14px 16px; display: grid; gap: 10px; }
.chat-teaser textarea { font: inherit; border: 1px solid var(--line); border-radius: 8px; padding: 8px 10px; min-width: 0; resize: none; }
.chat-teaser p { margin: 0; padding-right: 20px; overflow-wrap: anywhere; }
.chat-teaser .chat-x { position: absolute; top: 4px; right: 6px; color: var(--ink-2); }
.chat-teaser .btn { justify-self: start; padding: 10px 18px; font-size: 15px; }
.sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
@media (max-width: 639px) {
  .chat { right: 12px; bottom: 12px; }
  .chat-panel { position: fixed; left: 0; right: 0; bottom: 0; width: auto; max-width: none; border-radius: 14px 14px 0 0; max-height: 80vh; }
  .chat-panel ~ .chat-fab { display: none; }
}
</style>
