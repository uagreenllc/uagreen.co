import { describe, it, expect, vi, afterEach } from 'vitest'
import { chatStorage, parseStoredChat } from './chatStorage'

describe('parseStoredChat', () => {
  it('reads a valid record', () => expect(parseStoredChat('{"conversationId":"c1","token":"t"}')).toEqual({ conversationId: 'c1', token: 't' }))
  it('rejects garbage', () => {
    for (const raw of [null, '', 'nope', '[]', '{"conversationId":1,"token":"t"}', '{"conversationId":"c1"}'])
      expect(parseStoredChat(raw)).toBeNull()
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
