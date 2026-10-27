import { CAM, TOPY, bladePath, strutPaths } from './scene'

type GsapLib = typeof import('gsap').gsap

/** DOM handles the timeline drives; collected by K5Story.vue after mount (document order). */
export interface SceneRefs {
  section: HTMLElement
  svg: SVGSVGElement
  stator: SVGGElement
  lams: Element[]
  coils: SVGPathElement[]
  band: SVGGElement
  top: SVGGElement
  shaft: SVGGElement
  magnets: Element[]
  housing: SVGGElement
  rawGroup: SVGGElement
  rawLayers: SVGPathElement[] // 3 blades × 4 layers, innermost first
  rawOuter: SVGPathElement[]
  assembly: SVGGElement
  asmShaft: SVGElement
  rotor: SVGGElement
  strutTop: SVGPathElement[]
  strutBot: SVGPathElement[]
  bladeOut: SVGPathElement[]
  bladeFace: SVGPathElement[]
  gen: SVGGElement
  ghost: SVGElement
  rebar: SVGPathElement
  concrete: SVGElement
  anchor: SVGElement
  sections: Element[]
  flanges: Element[]
  ladder: SVGPathElement
  cable: SVGPathElement
  controller: SVGElement
  ballast: SVGElement
  battery: SVGElement
  window: SVGElement
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
  const state = { phase: 0, bladeDraw: 0, strutDraw: 0 }
  let spin = 0
  let spinOn = false

  const drawBlades = () => {
    const ph = state.phase + spin
    const struts = strutPaths(ph)
    for (let i = 0; i < 3; i++) {
      const d = bladePath(i as 0 | 1 | 2, ph)
      const out = r.bladeOut[i]!
      const face = r.bladeFace[i]!
      out.setAttribute('d', d)
      face.setAttribute('d', d)
      const L = face.getTotalLength()
      const off = (L + 20) * (1 - state.bladeDraw)
      for (const p of [out, face]) {
        p.style.strokeDasharray = `${L} ${L + 40}`
        p.style.strokeDashoffset = `${off}`
      }
      const s = struts[i]!
      r.strutTop[i]!.setAttribute('d', s.top)
      r.strutBot[i]!.setAttribute('d', s.bot)
      for (const [p, len] of [[r.strutTop[i]!, s.topLen], [r.strutBot[i]!, s.botLen]] as const) {
        p.style.strokeDasharray = `${len} ${len + 40}`
        p.style.strokeDashoffset = `${(len + 20) * (1 - state.strutDraw)}`
      }
    }
  }
  const prep = (e: SVGPathElement) => {
    const L = e.getTotalLength()
    e.style.strokeDasharray = `${L} ${L + 40}`
    e.style.strokeDashoffset = `${L + 20}`
  }
  const draw = (e: Element | Element[], dur: number, pos: gsap.Position, tl: gsap.core.Timeline) =>
    tl.to(e, { strokeDashoffset: 0, duration: dur }, pos)

  // Client-side initial state. SSR shipped these hidden (opacity 0) so nothing flashes before the dashes exist.
  ;[...r.coils, ...r.rawLayers, ...r.rawOuter, r.rebar, r.ladder, r.cable].forEach(prep)
  drawBlades()
  g.set([...r.coils, ...r.rawLayers, ...r.rawOuter, r.rebar, r.ladder, r.cable, r.rotor], { opacity: 1 })
  g.set([...r.sections, r.asmShaft], { scaleY: 0, transformOrigin: '50% 100%', opacity: 1 })

  const tl = g.timeline({
    defaults: { ease: 'none' },
    paused: opts.reduce,
    scrollTrigger: opts.reduce ? undefined : { trigger: r.section, start: 'top top', end: '+=7000', pin: true, scrub: 0.8 },
    onUpdate: () => {
      const t = tl.time()
      let idx = 0
      STEP_LABELS.forEach((l, i) => { if (t >= tl.labels[l]! - 0.04) idx = i + 1 })
      const fill = Math.min(1, Math.max(0, (t - tl.labels.s1!) / (tl.labels.end! - tl.labels.s1!)))
      spinOn = t >= tl.labels.s5! + 0.78
      opts.onStep(idx, fill)
    },
  })

  tl.to({}, { duration: 0.35 })
  // 1 — generator
  tl.addLabel('s1')
  tl.to(r.svg, { attr: { viewBox: CAM.gen }, duration: 0.3, ease: 'power2.inOut' }, 's1')
  r.lams.forEach((l, i) => tl.to(l, { opacity: 1, y: -4 * i, duration: 0.07 }, `s1+=${0.08 + i * 0.035}`))
  r.coils.forEach((c, i) => draw(c, 0.06, `s1+=${0.48 + i * 0.013}`, tl))
  tl.to([r.band, r.top, r.shaft], { opacity: 1, duration: 0.06 }, 's1+=1.02')
  r.magnets.forEach((m, i) => tl.to(m, { opacity: 1, duration: 0.02 }, `s1+=${1.06 + i * 0.006}`))
  tl.to([r.band, r.top, r.shaft], { y: TOPY, duration: 0.25, ease: 'power2.inOut' }, 's1+=1.3')
  tl.to(r.housing, { opacity: 1, duration: 0.12 }, 's1+=1.56')
  tl.to(r.stator, { x: 350, y: 1370, scale: 0.2, duration: 0.18, ease: 'power2.inOut' }, 's1+=1.7')
  tl.to(r.stator, { opacity: 0, duration: 0.06 }, 's1+=1.84')
  tl.to(r.gen, { opacity: 1, duration: 0.06 }, 's1+=1.84')
  // 2 — blades
  tl.addLabel('s2', 's1+=1.92')
  tl.to(r.svg, { attr: { viewBox: CAM.blades }, duration: 0.3, ease: 'power2.inOut' }, 's2')
  for (let b = 0; b < 3; b++) {
    for (let k = 0; k < 4; k++) draw(r.rawLayers[b * 4 + k]!, 0.08, `s2+=${0.12 + b * 0.07 + k * 0.1}`, tl)
    draw(r.rawOuter[b]!, 0.1, `s2+=${0.62 + b * 0.07}`, tl)
    tl.to(r.rawOuter[b]!, { attr: { 'fill-opacity': 1 }, duration: 0.1 }, `s2+=${0.82 + b * 0.07}`)
    tl.to(r.rawLayers.slice(b * 4, b * 4 + 4), { opacity: 0, duration: 0.1 }, `s2+=${0.82 + b * 0.07}`)
  }
  // 3 — assembly
  tl.addLabel('s3', 's2+=1.2')
  tl.to(r.svg, { attr: { viewBox: CAM.asm }, duration: 0.3, ease: 'power2.inOut' }, 's3')
  tl.to(r.asmShaft, { scaleY: 1, duration: 0.2 }, 's3+=0.05')
  tl.to(state, { strutDraw: 1, duration: 0.2, onUpdate: drawBlades }, 's3+=0.25')
  tl.to(r.rawGroup, { opacity: 0, duration: 0.2 }, 's3+=0.42')
  tl.to(state, { bladeDraw: 1, duration: 0.35, onUpdate: drawBlades }, 's3+=0.42')
  tl.to(state, { phase: 120, duration: 0.3, ease: 'power1.inOut', onUpdate: drawBlades }, 's3+=0.82')
  // 4 — foundation and mast
  tl.addLabel('s4', 's3+=1.2')
  tl.to(r.svg, { attr: { viewBox: CAM.mast }, duration: 0.35, ease: 'power2.inOut' }, 's4')
  tl.to(r.ghost, { opacity: 0, duration: 0.2 }, 's4')
  draw(r.rebar, 0.2, 's4+=0.12', tl)
  tl.to(r.concrete, { opacity: 1, duration: 0.15 }, 's4+=0.32')
  tl.to(r.anchor, { opacity: 1, duration: 0.08 }, 's4+=0.47')
  r.sections.forEach((s, i) => tl.to(s, { scaleY: 1, duration: 0.15 }, `s4+=${0.55 + i * 0.17}`))
  r.flanges.forEach((f, i) => tl.to(f, { opacity: 1, duration: 0.04 }, `s4+=${0.7 + i * 0.17}`))
  draw(r.ladder, 0.3, 's4+=0.75', tl)
  // 5 — installation
  tl.addLabel('s5', 's4+=1.15')
  tl.to(r.svg, { attr: { viewBox: CAM.install }, duration: 0.35, ease: 'power2.inOut' }, 's5')
  tl.to(r.assembly, { y: 470, duration: 0.3, ease: 'power2.inOut' }, 's5+=0.05')
  tl.to(r.assembly, { x: 900, duration: 0.3, ease: 'power2.inOut' }, 's5+=0.32')
  tl.to(r.assembly, { y: 500, duration: 0.14, ease: 'power2.out' }, 's5+=0.62')
  draw(r.cable, 0.25, 's5+=0.78', tl)
  tl.to([r.controller, r.ballast, r.battery], { opacity: 1, duration: 0.08, stagger: 0.05 }, 's5+=0.98')
  tl.to(r.window, { attr: { fill: '#f0cc5a' }, duration: 0.08 }, 's5+=1.12')
  tl.addLabel('end', 's5+=1.3')
  tl.to(r.svg, { attr: { viewBox: CAM.end }, duration: 0.3, ease: 'power2.inOut' }, 'end')
  tl.to({}, { duration: 0.05 }, 'end+=0.3')

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
