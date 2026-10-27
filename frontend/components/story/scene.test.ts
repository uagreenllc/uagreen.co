import { describe, it, expect } from 'vitest'
import { lamPath, bladePath, strutPaths, magnets, coilPath, ladderPath, bandClipPath, TOPY } from './scene'

describe('k5 scene geometry', () => {
  it('lamination has 72 slots and 4 lugs', () => {
    const d = lamPath()
    expect(d.startsWith('M')).toBe(true)
    expect(d.endsWith('Z')).toBe(true)
    expect((d.match(/Z/g) ?? []).length).toBe(2) // outer rim + slotted bore
    expect((d.match(/L/g) ?? []).length).toBeGreaterThan(72 * 10)
  })

  it('blade is a straight vertical panel from TOP to BOT inside the rotor radius', () => {
    const pts = bladePath(0, 0).slice(1).split('L').map(p => p.split(',').map(Number))
    expect(pts.length).toBe(2)
    expect(pts[0]![1]).toBe(-320)
    expect(pts[1]![1]).toBe(-80)
    expect(pts[0]![0]).toBe(pts[1]![0]) // vertical
    expect(Math.abs(pts[0]![0])).toBeLessThanOrEqual(125)
  })

  it('each blade hangs on two struts that meet at the hub on the shaft', () => {
    const s = strutPaths(0)[0]!
    expect(s.top).toBe('M125.0,-260.0L0,-208.0') // from the blade's upper quarter down to the hub
    expect(s.bot).toBe('M125.0,-140.0L0,-192.0') // from the lower quarter up to the hub
    expect(s.topLen).toBeCloseTo(Math.hypot(125, 52), 5)
  })

  it('geometry is deterministic', () => {
    expect(bladePath(1, 37.5)).toBe(bladePath(1, 37.5))
    expect(strutPaths(120)[0]!.top).toBe(strutPaths(0)[1]!.top)
  })

  it('36 magnets face the viewer, 36 coils fit the ring', () => {
    expect(magnets().length).toBe(36)
    expect(coilPath(35)).toMatch(/^M-?\d/)
    expect(bandClipPath()).toContain(`V${TOPY}`)
    expect(ladderPath()).toContain('M918,1400V510')
  })
})
