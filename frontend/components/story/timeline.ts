import { BOT, CAM, R, TOP, TOPY, bladePath, coilOfSlot, strutPaths } from './scene'

type GsapLib = typeof import('gsap').gsap

/** DOM handles the timeline drives; collected by K5Story.vue after mount (document order). */
export interface SceneRefs {
  section: HTMLElement
  svg: SVGSVGElement
  stator: SVGGElement
  lams: Element[]
  coils: SVGPathElement[]
  strips: SVGPathElement[] // copper down the far wall of the bore, data-slot = slot index
  coilsBottom: SVGPathElement[] // end turns under the stack, same order as coils
  band: SVGGElement
  top: SVGGElement
  shaft: SVGGElement
  magnets: Element[]
  housing: SVGGElement
  rawGroup: SVGGElement
  mould: SVGGElement // bench + both shells; fades when assembly starts, the blades do not
  moldLayers: Element[] // glass-fabric layers laid into the mould, thinnest first
  moldLid: SVGElement // upper half-shell, closes onto the mould
  moldBlade: SVGElement // finished blade in the mould; it stands up to become rawOuter[0]
  rawOuter: SVGPathElement[] // three finished blades standing beside the mould
  assembly: SVGGElement
  asmShaft: SVGElement
  rotorBack: SVGGElement // blades on the far side, behind the shaft
  rotorFront: SVGGElement
  /** [back, front]: each holds struts + blades for all 3 blades; a blade is shown in the set matching its depth */
  rotorSets: { top: SVGPathElement[]; bot: SVGPathElement[]; blade: SVGPathElement[] }[]
  gen: SVGGElement
  ghost: SVGElement
  rebar: SVGPathElement
  concrete: SVGElement
  anchor: SVGElement
  sections: Element[] // complete sections (tube, ladder, flange); lie on the trailer, tilt up about their lower end, then fly to the mast
  trailer: SVGElement
  cable: SVGPathElement
  crane: SVGGElement
  boom: SVGPathElement // telescopic extension (thin)
  boomBase: SVGPathElement // base stage (thick)
  cyl: SVGPathElement // hydraulic cylinder from the chassis to the boom
  hub: SVGElement
  hookLine: SVGPathElement
  hook: SVGElement
  controller: SVGElement
  ballast: SVGElement
  battery: SVGElement
  window: SVGElement
  glow: SVGElement // warm halo behind the window once the house has power
}

export interface Story {
  tl: gsap.core.Timeline
  /** Scroll to `s1 + u` timeline units (QA helper). */
  seek: (u: number) => void
  destroy: () => void
}

export interface StoryOptions {
  reduce: boolean
  onStep: (idx: number, railFill: number) => void
}

const STEP_LABELS = ['s1', 's2', 's3', 's4', 's5'] as const

/**
 * The pinned, scrubbed timeline (positions and durations are the prototype's).
 * Dashes are padded by 20 so round caps leave no dots at the ends while a stroke is hidden.
 */
type ScrollTriggerLib = typeof import('gsap/ScrollTrigger').ScrollTrigger

export function buildTimeline(r: SceneRefs, g: GsapLib, ScrollTrigger: ScrollTriggerLib, opts: StoryOptions): Story {
  g.registerPlugin(ScrollTrigger)
  const state = { phase: 15, bladeDraw: 0, strutDraw: 0 } // phase 15°: three distinct x positions, none hidden behind the shaft
  let spin = 0
  let spinOn = false

  const drawBlades = () => {
    const ph = state.phase + spin
    const struts = strutPaths(ph)
    for (let i = 0; i < 3; i++) {
      // a blade on the far half of the circle passes behind the shaft and hub: show it in the back set only
      const front = Math.sin(((ph + i * 120) * Math.PI) / 180) >= 0
      const d = bladePath(i as 0 | 1 | 2, ph)
      const s = struts[i]!
      r.rotorSets.forEach((set, k) => {
        const vis = (k === 1) === front ? 'visible' : 'hidden'
        const top = set.top[i]!
        const bot = set.bot[i]!
        const blade = set.blade[i]!
        blade.setAttribute('d', d)
        blade.style.opacity = `${state.bladeDraw}` // the blanks travel from the workshop, then these take over in place
        top.setAttribute('d', s.top)
        bot.setAttribute('d', s.bot)
        for (const [p, len] of [[top, s.topLen], [bot, s.botLen]] as const) {
          p.style.strokeDasharray = `${len} ${len + 40}`
          p.style.strokeDashoffset = `${(len + 20) * (1 - state.strutDraw)}`
        }
        for (const el of [top, bot, blade]) el.style.visibility = vis
      })
    }
  }
  // Crane: boom from the turntable to its tip, hook line down from the tip. While a load hangs
  // (crane.target = 1..3 mast sections, 4 the turbine) the tip follows it and the hook sits on its
  // lifting point, so the load visibly hangs from the crane.
  // A section pivots about its lower end (svgOrigin), so its lifting point (upper end) is 300 away
  // in the direction of its current rotation; the turbine hangs from a fixed point on its hub.
  const hangs = [
    null,
    { el: r.sections[0]!, ax: 900, ay: 1100, pivots: true },
    { el: r.sections[1]!, ax: 900, ay: 800, pivots: true },
    { el: r.sections[2]!, ax: 900, ay: 500, pivots: true },
    { el: r.assembly, ax: 0, ay: -218, pivots: false },
  ]
  const lift = (h: NonNullable<(typeof hangs)[number]>) => {
    const x = +g.getProperty(h.el, 'x')
    const y = +g.getProperty(h.el, 'y')
    if (!h.pivots) return { x: x + h.ax, y: y + h.ay }
    const th = (+g.getProperty(h.el, 'rotation') * Math.PI) / 180
    return { x: x + h.ax + 300 * Math.sin(th), y: y + h.ay + 300 - 300 * Math.cos(th) }
  }
  const crane = { tipX: 840, tipY: 1336, hookY: 1336, target: 0 }
  const drawCrane = () => {
    let { tipX, tipY, hookY } = crane
    const h = hangs[crane.target]
    if (h) {
      const p = lift(h)
      tipX = p.x
      tipY = 110
      hookY = p.y
    }
    const mx = 760 + (tipX - 760) * 0.45
    const my = 1346 + (tipY - 1346) * 0.45
    r.boomBase.setAttribute('d', `M760,1346L${mx.toFixed(1)},${my.toFixed(1)}`)
    r.boom.setAttribute('d', `M${mx.toFixed(1)},${my.toFixed(1)}L${tipX.toFixed(1)},${tipY.toFixed(1)}`)
    r.cyl.setAttribute('d', `M722,1352L${(760 + (tipX - 760) * 0.3).toFixed(1)},${(1346 + (tipY - 1346) * 0.3).toFixed(1)}`)
    r.hookLine.setAttribute('d', `M${tipX.toFixed(1)},${tipY.toFixed(1)}V${hookY.toFixed(1)}`)
    r.hook.setAttribute('transform', `translate(${tipX.toFixed(1)} ${hookY.toFixed(1)})`)
  }
  const prep = (e: SVGPathElement) => {
    const L = e.getTotalLength()
    e.style.strokeDasharray = `${L} ${L + 40}`
    e.style.strokeDashoffset = `${L + 20}`
  }
  const draw = (e: Element | Element[], dur: number, pos: gsap.Position, tl: gsap.core.Timeline) =>
    tl.to(e, { strokeDashoffset: 0, duration: dur }, pos)

  // Client-side initial state. SSR shipped these hidden (opacity 0) so nothing flashes before the dashes exist.
  ;[...r.coils, ...r.coilsBottom, ...r.strips, r.rebar, r.cable].forEach(prep)
  drawBlades()
  g.set([...r.coils, ...r.coilsBottom, ...r.strips, r.rebar, r.cable, r.rotorBack, r.rotorFront], { opacity: 1 })
  g.set(r.asmShaft, { scaleY: 0, transformOrigin: '50% 100%', opacity: 1 })
  g.set([r.battery, r.ballast, r.controller], { scale: 0.6, transformOrigin: '50% 50%' })
  // sections lie stacked on the trailer, first-needed on top, lower end at x=940; each pivots about that lower end
  r.sections.forEach((s, i) => {
    const h = hangs[i + 1]!
    g.set(s, { svgOrigin: `${h.ax} ${h.ay + 300}`, rotation: 90, x: 40, y: 1317 + 22 * i - (h.ay + 300) })
  })

  const tl = g.timeline({
    defaults: { ease: 'none' },
    paused: opts.reduce,
    scrollTrigger: opts.reduce ? undefined : { trigger: r.section, start: 'top top', end: '+=10000', pin: true, scrub: 0.8 },
    onUpdate: () => {
      drawCrane()
      const t = tl.time()
      let idx = 0
      STEP_LABELS.forEach((l, i) => { if (t >= tl.labels[l]! - 0.04) idx = i + 1 })
      const fill = Math.min(1, Math.max(0, (t - tl.labels.s1!) / (tl.labels.end! - tl.labels.s1!)))
      spinOn = t >= tl.labels.s5! + 1.5
      opts.onStep(idx, fill)
    },
  })

  tl.to({}, { duration: 0.35 })
  // 1 — generator
  tl.addLabel('s1')
  tl.to(r.svg, { attr: { viewBox: CAM.gen }, duration: 0.3, ease: 'power2.inOut' }, 's1')
  r.lams.forEach((l, i) => tl.to(l, { opacity: 1, y: -4 * i, duration: 0.07 }, `s1+=${0.08 + i * 0.035}`))
  // one coil at a time: its top end turn, its bottom end turn and the copper in its slots go together
  const coilAt = (i: number) => `s1+=${0.48 + i * 0.013}`
  r.coils.forEach((c, i) => draw(c, 0.06, coilAt(i), tl))
  r.coilsBottom.forEach((c, i) => draw(c, 0.06, coilAt(i), tl))
  r.strips.forEach(s => draw(s, 0.06, coilAt(coilOfSlot(+s.dataset.slot!)), tl))
  tl.to([r.band, r.top, r.shaft], { opacity: 1, duration: 0.06 }, 's1+=1.02')
  r.magnets.forEach((m, i) => tl.to(m, { opacity: 1, duration: 0.02 }, `s1+=${1.06 + i * 0.006}`))
  tl.to([r.band, r.top, r.shaft], { y: TOPY, duration: 0.25, ease: 'power2.inOut' }, 's1+=1.3')
  tl.to(r.housing, { opacity: 1, duration: 0.12 }, 's1+=1.56')
  // The camera glides to the workshop view while the finished generator shrinks onto its spot on the
  // floor, so the eye follows one continuous motion; at the end it is swapped for the assembly's copy
  // (same shape at scale 0.19) in one frame, so nothing vanishes or pops.
  tl.to(r.svg, { attr: { viewBox: CAM.blades }, duration: 0.45, ease: 'power2.inOut' }, 's1+=1.62')
  tl.to(r.stator, { x: 470, y: 1374.2, scale: 0.19, duration: 0.35, ease: 'power2.inOut' }, 's1+=1.7')
  tl.set(r.stator, { opacity: 0 }, 's1+=2.05')
  tl.set(r.gen, { opacity: 1 }, 's1+=2.05')
  // 2 — blades
  tl.addLabel('s2', 's1+=2.08')
  tl.to(r.rawGroup, { opacity: 1, duration: 0.1 }, 's2') // the mould shows only in its own stage
  // fabric goes into the mould layer by layer, the lid closes, opens, the blade stands up; two more follow
  r.moldLayers.forEach((l, i) => tl.to(l, { opacity: 1, duration: 0.06 }, `s2+=${0.12 + i * 0.07}`))
  tl.to(r.moldLid, { scaleY: -1, transformOrigin: '50% 100%', duration: 0.16, ease: 'power2.inOut' }, 's2+=0.56') // flips shut on its hinge
  tl.to(r.moldLid, { scaleY: 1, duration: 0.14, ease: 'power2.inOut' }, 's2+=0.76')
  tl.to(r.moldLayers, { opacity: 0, duration: 0.06 }, 's2+=0.76')
  tl.to(r.moldBlade, { opacity: 1, duration: 0.06 }, 's2+=0.78')
  tl.to(r.moldBlade, { x: 202, y: -60, rotation: 90, transformOrigin: '50% 50%', duration: 0.22, ease: 'power2.inOut' }, 's2+=0.9')
  tl.to(r.rawOuter[0]!, { opacity: 1, duration: 0.04 }, 's2+=1.1')
  tl.to(r.moldBlade, { opacity: 0, duration: 0.04 }, 's2+=1.1')
  tl.to([r.rawOuter[1]!, r.rawOuter[2]!], { opacity: 1, duration: 0.08, stagger: 0.05 }, 's2+=1.12')
  // 3 — assembly
  tl.addLabel('s3', 's2+=1.3')
  tl.to(r.svg, { attr: { viewBox: CAM.asm }, duration: 0.3, ease: 'power2.inOut' }, 's3')
  tl.to(r.asmShaft, { scaleY: 1, duration: 0.2 }, 's3+=0.05')
  tl.to(r.hub, { opacity: 1, duration: 0.05 }, 's3+=0.23')
  tl.to(state, { strutDraw: 1, duration: 0.2, onUpdate: drawBlades }, 's3+=0.25')
  tl.to(r.mould, { opacity: 0, duration: 0.2 }, 's3+=0.3')
  // the three finished blades travel from the workshop onto the strut tips, then become the rotor's blades in place
  const asmX = +g.getProperty(r.assembly, 'x')
  const asmY = +g.getProperty(r.assembly, 'y')
  r.rawOuter.forEach((b, i) => {
    const box = b.getBBox()
    const dx = asmX + R * Math.cos(((state.phase + i * 120) * Math.PI) / 180) - (box.x + box.width / 2)
    const dy = asmY + (TOP + BOT) / 2 - (box.y + box.height / 2)
    tl.to(b, { x: dx, y: dy, duration: 0.3, ease: 'power2.inOut' }, `s3+=${0.42 + i * 0.12}`)
  })
  tl.set(state, { bladeDraw: 1, onUpdate: drawBlades }, 's3+=0.98')
  tl.set(r.rawOuter, { opacity: 0 }, 's3+=0.98')
  tl.to(state, { phase: 135, duration: 0.3, ease: 'power1.inOut', onUpdate: drawBlades }, 's3+=1.02')
  // 4 — foundation and mast
  tl.addLabel('s4', 's3+=1.4')
  tl.to(r.svg, { attr: { viewBox: CAM.mast }, duration: 0.35, ease: 'power2.inOut' }, 's4')
  tl.to(r.ghost, { opacity: 0, duration: 0.2 }, 's4')
  draw(r.rebar, 0.2, 's4+=0.12', tl)
  tl.to(r.concrete, { opacity: 1, duration: 0.15 }, 's4+=0.32')
  tl.to(r.anchor, { opacity: 1, duration: 0.08 }, 's4+=0.47')
  // the crane arrives with the three sections staged beside the house; it stacks them one by one
  tl.to(r.crane, { opacity: 1, duration: 0.1 }, 's4+=0.1')
  tl.to([r.trailer, ...r.sections], { opacity: 1, duration: 0.1 }, 's4+=0.15')
  let t4 = 0.5
  r.sections.forEach((s, i) => {
    const h = hangs[i + 1]!
    const p = lift(h) // upper end while lying on the trailer
    tl.to(crane, { tipX: p.x, tipY: 110, hookY: 110, duration: 0.1, ease: 'power2.inOut' }, `s4+=${t4}`)
    tl.to(crane, { hookY: p.y, duration: 0.08 }, `s4+=${t4 + 0.1}`)
    tl.set(crane, { target: i + 1 }, `s4+=${t4 + 0.19}`)
    tl.to(s, { rotation: 0, duration: 0.16, ease: 'power1.inOut' }, `s4+=${t4 + 0.2}`) // tilts up, lower end resting on the trailer
    tl.to(s, { y: -60, duration: 0.14, ease: 'power2.inOut' }, `s4+=${t4 + 0.36}`) // lifts clear, hanging upright
    tl.to(s, { x: 0, duration: 0.1, ease: 'power2.inOut' }, `s4+=${t4 + 0.5}`)
    tl.to(s, { y: 0, duration: 0.06, ease: 'power2.out' }, `s4+=${t4 + 0.6}`)
    tl.set(crane, { target: 0, tipX: 900, tipY: 110, hookY: h.ay }, `s4+=${t4 + 0.67}`)
    tl.to(crane, { hookY: 110, duration: 0.06 }, `s4+=${t4 + 0.68}`)
    t4 += 0.78
  })
  // 5 — installation
  tl.addLabel('s5', `s4+=${t4 + 0.1}`)
  tl.to(r.svg, { attr: { viewBox: CAM.install }, duration: 0.35, ease: 'power2.inOut' }, 's5')
  // the boom swings over the turbine, the hook comes down onto the hub…
  tl.to(crane, { tipX: 470, tipY: 110, hookY: 110, duration: 0.2, ease: 'power2.inOut' }, 's5+=0.05')
  tl.to(crane, { hookY: 1182, duration: 0.14, ease: 'power1.inOut' }, 's5+=0.25')
  tl.set(crane, { target: 4 }, 's5+=0.4')
  // …lifts, slews to the mast, sets the turbine down…
  tl.to(r.assembly, { y: 470, duration: 0.3, ease: 'power2.inOut' }, 's5+=0.42')
  tl.to(r.assembly, { x: 900, duration: 0.3, ease: 'power2.inOut' }, 's5+=0.72')
  tl.to(r.assembly, { y: 500, duration: 0.14, ease: 'power2.out' }, 's5+=1.02')
  // …unhooks, lowers the boom and drives off
  tl.set(crane, { target: 0, tipX: 900, tipY: 110, hookY: 282 }, 's5+=1.17')
  tl.to(crane, { hookY: 110, duration: 0.12, ease: 'power1.inOut' }, 's5+=1.18')
  tl.to(crane, { tipX: 840, tipY: 1336, hookY: 1336, duration: 0.18, ease: 'power2.inOut' }, 's5+=1.3') // rope stays reeled in as the boom lowers
  tl.to(r.crane, { x: -500, opacity: 0, duration: 0.25, ease: 'power2.in' }, 's5+=1.46')
  // wiring: the camera moves to the house, the cable is laid from the mast into it, the battery,
  // ballast and controller go in one after another, and the window lights up
  tl.to(r.svg, { attr: { viewBox: CAM.house }, duration: 0.3, ease: 'power2.inOut' }, 's5+=1.7')
  draw(r.cable, 0.45, 's5+=1.78', tl)
  ;[r.battery, r.ballast, r.controller].forEach((e, i) =>
    tl.to(e, { opacity: 1, scale: 1, duration: 0.1, ease: 'power2.out' }, `s5+=${2.25 + i * 0.12}`),
  )
  tl.to(r.window, { attr: { fill: '#f0cc5a' }, duration: 0.1 }, 's5+=2.65')
  tl.to(r.glow, { opacity: 0.35, duration: 0.15 }, 's5+=2.65')
  tl.addLabel('end', 's5+=2.9')
  tl.to(r.svg, { attr: { viewBox: CAM.end }, duration: 0.4, ease: 'power2.inOut' }, 'end')
  tl.to({}, { duration: 0.05 }, 'end+=0.4')

  drawCrane()
  const tick = (_t: number, dt: number) => {
    if (spinOn) {
      spin += dt * 0.09
      drawBlades()
    }
  }
  if (opts.reduce) {
    tl.progress(1) // finished scene, no spinning: continuous motion is what the user opted out of
  } else {
    g.ticker.add(tick)
    // The plugin loads after the window load event, and the web font lands later still:
    // measure the pin again once layout is final, or the scrub range can come out empty.
    requestAnimationFrame(() => ScrollTrigger.refresh())
    document.fonts?.ready.then(() => ScrollTrigger.refresh())
  }

  return {
    tl,
    seek(u) {
      const st = tl.scrollTrigger
      if (st) st.scroll(st.start + ((tl.labels.s1! + u) / tl.duration()) * (st.end - st.start))
      else tl.time(tl.labels.s1! + u)
    },
    destroy() {
      g.ticker.remove(tick)
      tl.scrollTrigger?.kill()
      tl.kill()
    },
  }
}
