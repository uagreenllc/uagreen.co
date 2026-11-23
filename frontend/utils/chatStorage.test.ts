import { describe, it, expect, vi, afterEach } from 'vitest'
import { chatStorage, parseStoredChat } from './chatStorage'

describe('parseStoredChat', () => {
  it('reads a valid record', () => expect(parseStoredChat('{"conversationId":"c1","token":"t"}')).toEqual({ conversationId: 'c1', token: 't' }))
  it('reads the extended record', () =>
    expect(parseStoredChat('{"conversationId":"c1","token":"t","seenAt":"2026-11-23T10:00:00Z","contactSaved":true}'))
      .toStrictEqual({ conversationId: 'c1', token: 't', seenAt: '2026-11-23T10:00:00Z', contactSaved: true }))
  it('reads an old record without the new fields', () =>
    expect(parseStoredChat('{"conversationId":"c1","token":"t"}')).toStrictEqual({ conversationId: 'c1', token: 't' }))
  it('drops malformed optional fields but keeps the conversation', () =>
    expect(parseStoredChat('{"conversationId":"c1","token":"t","seenAt":5,"contactSaved":"yes"}')).toStrictEqual({ conversationId: 'c1', token: 't' }))
  it('rejects garbage', () => {
    for (const raw of [null, '', 'nope', '[]', '{"conversationId":1,"token":"t"}', '{"conversationId":"c1"}'])
      expect(parseStoredChat(raw)).toBeNull()
  })
})
describe('chatStorage.save', () => {
  afterEach(() => vi.unstubAllGlobals())
  it('writes only the known fields', () => {
    const data: Record<string, string> = {}
    vi.stubGlobal('localStorage', { setItem: (k: string, v: string) => { data[k] = v } })
    chatStorage.save({ conversationId: 'c', token: 't', seenAt: 'x', contactSaved: true, name: 'Ivan' } as never)
    expect(JSON.parse(data.uag_chat!)).toStrictEqual({ conversationId: 'c', token: 't', seenAt: 'x', contactSaved: true })
  })
})
describe('chatStorage when localStorage throws', () => {
  afterEach(() => vi.unstubAllGlobals())
  it('reads nothing and writes without throwing', () => {
    vi.stubGlobal('localStorage', {
      getItem() { throw new Error('denied') },
      setItem() { throw new Error('denied') },
      removeItem() { throw new Error('denied') },
    })
    expect(chatStorage.load()).toBeNull()
    expect(() => chatStorage.save({ conversationId: 'c', token: 't' })).not.toThrow()
    expect(chatStorage.dismissedAt()).toBeNull()
  })
})
