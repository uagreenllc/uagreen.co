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

  it('blade spans TOP..BOT and stays inside the rotor radius', () => {
    const pts = bladePath(0, 0).slice(1).split('L').map(p => p.split(',').map(Number))
    expect(pts.length).toBe(15)
    expect(pts[0]![1]).toBe(-320)
    expect(pts[14]![1]).toBe(-80)
    expect(Math.max(...pts.map(p => Math.abs(p[0]!)))).toBeLessThanOrEqual(125)
  })

  it('geometry is deterministic', () => {
    expect(bladePath(1, 37.5)).toBe(bladePath(1, 37.5))
    expect(strutPaths(0)[0]!.top).toBe('M0,-320L125.0,-320')
  })

  it('36 magnets face the viewer, 36 coils fit the ring', () => {
    expect(magnets().length).toBe(36)
    expect(coilPath(35)).toMatch(/^M-?\d/)
    expect(bandClipPath()).toContain(`V${TOPY}`)
    expect(ladderPath()).toContain('M918,1400V510')
  })
})
