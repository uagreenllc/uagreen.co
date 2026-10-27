/**
 * Pure geometry for the K5 story scene (ported from docs/prototypes/k5-scroll.html).
 * Every float that lands in a path string goes through toFixed(1), so the SVG rendered
 * on the server is byte-identical to the client's and hydration never mismatches.
 */

// Stator / generator detail (isometric: RX horizontal, RY vertical radius of the ellipse)
export const RX = 140
export const RY = 70
export const N = 72 // slots and magnets
export const LAMS = 20
export const TOPY = -4 * (LAMS - 1) // y of the top lamination
export const DR = 0.66 // rotor drum radius (× RX)
export const H = -TOPY // drum height = stack height
export const ROW = H / 3 // one magnet row
export const HOVER = TOPY - 130 // drum y while it hovers above the stack
export const HR = 1.14 // housing radius (× RX)
export const HBOT = 56 // housing bottom below the stack

// Rotor on the mast
export const R = 125
export const TOP = -320
export const BOT = -80

// Camera (viewBox) per stage
export const CAM = {
  title: '0 0 1400 1556',
  gen: '165 990 370 411',
  blades: '60 1040 400 444',
  asm: '100 1000 500 556',
  mast: '300 300 1100 1222',
  install: '250 160 1150 1278',
  end: '450 60 900 1000',
} as const

const f = (n: number) => n.toFixed(1)
const pitch = (2 * Math.PI) / N

/** Point on the isometric ellipse of radius r (× RX/RY) at angle a. */
export const pt = (r: number, a: number): string => `${f(RX * r * Math.cos(a))},${f(RY * r * Math.sin(a))}`

/** Closed ellipse path at radius r0, optionally with a second subpath at r1 (for evenodd rings). */
export function ring(r0: number, r1?: number): string {
  const loop = (r: number) => {
    let d = ''
    for (let i = 0; i <= 120; i++) d += (i ? 'L' : 'M') + pt(r, (i / 120) * 2 * Math.PI)
    return d + 'Z'
  }
  return loop(r0) + (r1 ? loop(r1) : '')
}

/** One stator lamination: rim with 4 mounting lugs + 72 slots open to the bore (fill-rule evenodd). */
export function lamPath(): string {
  const lugs = [45, 135, 225, 315].map(x => (x * Math.PI) / 180)
  const outerR = (a: number) => {
    for (const L of lugs) {
      const da = Math.abs(Math.atan2(Math.sin(a - L), Math.cos(a - L)))
      if (da < 0.06) return da < 0.015 ? 1.03 : 1.06
    }
    return 1
  }
  let d = ''
  for (let i = 0; i <= 360; i++) {
    const a = (i / 360) * 2 * Math.PI
    d += (i ? 'L' : 'M') + pt(outerR(a), a)
  }
  d += 'Z'
  // slot: narrower opening at the bore (tooth shoes), parallel walls, rounded bottom
  const rin = 0.72
  const rslot = 0.9
  const half = 0.22 * pitch
  const open = 0.15 * pitch
  const rb = 0.88 * half
  const rhob = rslot - rb
  for (let k = 0; k < N; k++) {
    const c = k * pitch
    d += (k ? 'L' : 'M') + pt(rin, c - open) + 'L' + pt(rin + 0.035, c - open) + 'L' + pt(rin + 0.045, c - half) + 'L' + pt(rhob, c - half)
    for (let t = 1; t < 4; t++) {
      const ph = (Math.PI * t) / 4
      d += 'L' + pt(rhob + rb * Math.sin(ph), c - half * Math.cos(ph))
    }
    d += 'L' + pt(rhob, c + half) + 'L' + pt(rin + 0.045, c + half) + 'L' + pt(rin + 0.035, c + open) + 'L' + pt(rin, c + open)
  }
  return d + 'Z'
}

/** Front half of a cylinder of radius r (× RX) from y=top to y=bottom (local coordinates). */
function cylinderSide(r: number, top: number, bottom: number): string {
  let d = ''
  for (let i = 0; i <= 40; i++) {
    const a = (i / 40) * Math.PI
    d += (i ? 'L' : 'M') + `${f(RX * r * Math.cos(a))},${f(RY * r * Math.sin(a) + top)}`
  }
  d += `L${f(-RX * r)},${f(bottom)}`
  for (let i = 40; i >= 0; i--) {
    const a = (i / 40) * Math.PI
    d += `L${f(RX * r * Math.cos(a))},${f(RY * r * Math.sin(a) + bottom)}`
  }
  return d + 'Z'
}

/** Rotor drum side (front half), top face centre at local (0,0). */
export const bandPath = (): string => cylinderSide(DR, 0, H)

/** Region above the stack top plane; its edge is the front arc of the bore ellipse. The band is clipped by it. */
export function bandClipPath(): string {
  let d = `M-400,-1200H400V${TOPY}H${f(RX * DR)}`
  for (let i = 0; i <= 40; i++) {
    const a = (i / 40) * Math.PI
    d += `L${f(RX * DR * Math.cos(a))},${f(TOPY + RY * DR * Math.sin(a))}`
  }
  return d + 'H-400Z'
}

/** 72 magnets in 3 rows on the drum; only the 36 facing the viewer are drawn. */
export function magnets(): { x: number; y: number; w: number; h: number }[] {
  const out: { x: number; y: number; w: number; h: number }[] = []
  for (let j = 0; j < 3; j++) {
    for (let i = 0; i < 24; i++) {
      const a = ((i + 0.5) / 24) * 2 * Math.PI
      const s = Math.sin(a)
      if (s < 0.1) continue
      const w = 3 + 11 * s
      out.push({ x: +f(RX * DR * Math.cos(a) - w / 2), y: +f(RY * DR * s + j * ROW + 2), w: +f(w), h: +f(ROW - 4) })
    }
  }
  return out
}

/** One of 36 coils, each spanning 4 slots; copper stays on the ring face. */
export function coilPath(i: number): string {
  const a = i * 2 * pitch + pitch / 2
  const b = a + 4 * pitch
  return `M${pt(0.74, a)}L${pt(0.88, a)}Q${pt(0.96, a + pitch)} ${pt(0.96, (a + b) / 2)}Q${pt(0.96, b - pitch)} ${pt(0.88, b)}L${pt(0.74, b)}`
}

/** Three windows in the rotor's end flange. */
export function flangeWindows(): string[] {
  return [0, 120, 240].map((x) => {
    let d = ''
    for (let s = 0; s <= 8; s++) d += (s ? 'L' : 'M') + pt(0.45, ((x + 15 + s * 7.5) * Math.PI) / 180)
    return d
  })
}

/** Hex nut at (cx, cy) in the lid's projection: bottom hex, 6px body, top hex, stud above. */
export interface Nut { cx: number; cy: number; hexBottom: string; hexTop: string }
const hex = (cx: number, cy: number) => {
  let d = ''
  for (let i = 0; i < 6; i++) {
    const a = (i * Math.PI) / 3
    d += (i ? 'L' : 'M') + f(cx + 7 * Math.cos(a)) + ',' + f(cy + 3.5 * Math.sin(a))
  }
  return d + 'Z'
}

/** Finished generator: steel drum closing over the stack, lid with 4 hex nuts on studs. */
export function housing(): { side: string; lid: string; lidY: number; nuts: Nut[] } {
  const lidY = TOPY - 8
  const nuts = [45, 135, 225, 315].map((x) => {
    const a = (x * Math.PI) / 180
    const cx = +f(RX * 0.98 * Math.cos(a))
    const cy = +f(lidY + RY * 0.98 * Math.sin(a))
    return { cx, cy, hexBottom: hex(cx, cy), hexTop: hex(cx, cy - 6) }
  })
  return { side: cylinderSide(HR, lidY, HBOT), lid: ring(HR), lidY, nuts }
}

/** Straight blade blank standing in the workshop at x; k=0 outer outline, 1..4 inner lamination layers. */
export function rawBlade(x: number, k: 0 | 1 | 2 | 3 | 4): string {
  const cy = 1280
  const w = 13 * (1 - k * 0.18)
  const h = 240 * (1 - k * 0.07)
  return `M${x},${f(cy - h / 2)}C${f(x + w)},${f(cy - h / 2 + 70)} ${f(x + w)},${f(cy + h / 2 - 70)} ${x},${f(cy + h / 2)}C${f(x - w)},${f(cy + h / 2 - 70)} ${f(x - w)},${f(cy - h / 2 + 70)} ${x},${f(cy - h / 2)}Z`
}

/** Helical blade i (0..2) projected side-on at rotor phase phaseDeg; 15 points from TOP to BOT. */
export function bladePath(i: 0 | 1 | 2, phaseDeg: number): string {
  const S = 14
  let d = ''
  for (let k = 0; k <= S; k++) {
    const a = ((phaseDeg + i * 120 + (k / S) * 120) * Math.PI) / 180
    d += (k ? 'L' : 'M') + f(R * Math.cos(a)) + ',' + f(TOP + ((BOT - TOP) * k) / S)
  }
  return d
}

/** Top and bottom struts for each blade at the given phase, with their lengths for dash-drawing. */
export function strutPaths(phaseDeg: number): { top: string; bot: string; topLen: number; botLen: number }[] {
  return [0, 1, 2].map((i) => {
    const xt = R * Math.cos(((phaseDeg + i * 120) * Math.PI) / 180)
    const xb = R * Math.cos(((phaseDeg + i * 120 + 120) * Math.PI) / 180)
    return { top: `M0,${TOP}L${f(xt)},${TOP}`, bot: `M0,${BOT}L${f(xb)},${BOT}`, topLen: Math.abs(xt), botLen: Math.abs(xb) }
  })
}

/** Service ladder on the mast: two rails from the ground to the top flange, a rung every 28. */
export function ladderPath(): string {
  let d = 'M918,1400V510M930,1400V510'
  for (let y = 1390; y > 510; y -= 28) d += `M918,${y}H930`
  return d
}
