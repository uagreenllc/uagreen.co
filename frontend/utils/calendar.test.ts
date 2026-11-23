import { describe, expect, it } from 'vitest'
import { calendarWeeks } from './calendar'

describe('calendarWeeks', () => {
  it('starts weeks on Monday and pads', () => {
    const weeks = calendarWeeks('2026-12-08', '2026-12-31', [])
    const first = weeks[0]!, last = weeks.at(-1)!
    expect(first[0]).toMatchObject({ iso: '2026-12-07', day: 7, inWindow: false })
    expect(first[1]).toMatchObject({ iso: '2026-12-08', inWindow: true })
    expect(last[6]).toMatchObject({ iso: '2027-01-03', inWindow: false })
    expect(weeks.every(w => w.length === 7)).toBe(true)
  })

  it('marks only listed in-window days as free', () => {
    const days = calendarWeeks('2026-12-08', '2026-12-31', ['2026-12-08', '2026-12-10', '2027-01-02']).flat()
    expect(days.filter(d => d.free).map(d => d.iso)).toEqual(['2026-12-08', '2026-12-10'])
  })
})
