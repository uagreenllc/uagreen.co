<script setup lang="ts">
import {
  LAMS, TOPY, HOVER, DR, HR, CAM,
  lamPath, bandPath, bandClipPath, magnets, slotStrips, coilPath, flangeWindows, housing, rawBlade, bladePath, strutPaths, ladderPath, ring,
} from './scene'
import { buildTimeline, type SceneRefs, type Story } from './timeline'

const { t, tm, rt } = useI18n()
const localePath = useLocalePath()
const route = useRoute()

type Fact = { v: string; l: string }
const steps = computed(() => [1, 2, 3, 4, 5].map(i => ({
  h: t(`story.s${i}.h`),
  p: t(`story.s${i}.p`),
  facts: (tm(`story.s${i}.facts`) as Fact[]).map(f => ({ v: rt(f.v), l: rt(f.l) })),
})))
const rail = computed(() => (tm('story.rail') as string[]).map(s => rt(s)))

const active = ref(0)
const railFill = ref(0)
const isStatic = ref(false)
const sectionEl = ref<HTMLElement>()
const svgEl = ref<SVGSVGElement>()

// Geometry is pure and deterministic: identical on the server and in the browser.
const lam = lamPath()
const band = bandPath()
const clip = bandClipPath()
const mags = magnets()
const strips = slotStrips()
const coils = Array.from({ length: 36 }, (_, i) => coilPath(i))
const windows = flangeWindows()
const hs = housing()
const rawX = [360, 410, 460] // finished blades standing beside the mould
const layerFill = ['#e9f0e6', '#dde8d9', '#d1e0cc', '#c6d8c0', '#bbd0b5'] // glass fabric, 48 → 400 g/m²
const blades0 = ([0, 1, 2] as const).map(i => bladePath(i, 0))
const struts0 = strutPaths(0)
const ladder = ladderPath()
const lamShift = (i: number) => `translate(0 ${-4 * i - 40})`

let story: Story | undefined
onMounted(async () => {
  const reduce = route.query.motion === 'off' || window.matchMedia('(prefers-reduced-motion: reduce)').matches
  isStatic.value = reduce
  const [{ gsap }, { ScrollTrigger }] = await Promise.all([import('gsap'), import('gsap/ScrollTrigger')])
  const svg = svgEl.value!
  const all = <T extends Element = SVGElement>(sel: string) => Array.from(svg.querySelectorAll<T>(sel))
  const one = <T extends Element = SVGElement>(sel: string) => svg.querySelector<T>(sel)!
  const refs: SceneRefs = {
    section: sectionEl.value!,
    svg,
    stator: one<SVGGElement>('#k5-stator'),
    lams: all('[data-k=lam]'),
    coils: all<SVGPathElement>('[data-k=coil]'),
    strips: all<SVGPathElement>('[data-k=strip]'),
    coilsBottom: all<SVGPathElement>('[data-k=coil-bottom]'),
    band: one<SVGGElement>('#k5-band'),
    top: one<SVGGElement>('#k5-top'),
    shaft: one<SVGGElement>('#k5-shaft'),
    magnets: all('[data-k=mag]'),
    housing: one<SVGGElement>('#k5-housing'),
    rawGroup: one<SVGGElement>('#k5-raw'),
    moldLayers: all('[data-k=mold-layer]'),
    moldLid: one('#k5-mold-lid'),
    moldBlade: one('#k5-mold-blade'),
    rawOuter: all<SVGPathElement>('[data-k=raw-outer]'),
    assembly: one<SVGGElement>('#k5-asm'),
    asmShaft: one('#k5-asm-shaft'),
    rotor: one<SVGGElement>('#k5-rotor'),
    strutTop: all<SVGPathElement>('[data-k=strut-top]'),
    strutBot: all<SVGPathElement>('[data-k=strut-bot]'),
    bladeOut: all<SVGPathElement>('[data-k=blade-out]'),
    bladeFace: all<SVGPathElement>('[data-k=blade-face]'),
    gen: one<SVGGElement>('#k5-gen'),
    ghost: one('#k5-ghost'),
    rebar: one<SVGPathElement>('#k5-rebar'),
    concrete: one('#k5-concrete'),
    anchor: one('#k5-anchor'),
    sections: all('[data-k=sect]'),
    flanges: all('[data-k=flange]'),
    ladder: one<SVGPathElement>('#k5-ladder'),
    cable: one<SVGPathElement>('#k5-cable'),
    controller: one('#k5-controller'),
    ballast: one('#k5-ballast'),
    battery: one('#k5-battery'),
    window: one('#k5-window'),
  }
  story = buildTimeline(refs, gsap, ScrollTrigger, {
    reduce,
    onStep: (idx, fill) => { active.value = idx; railFill.value = fill },
  })
  if (reduce) active.value = 5
  if (import.meta.dev) (window as unknown as { __k5: unknown }).__k5 = { tl: story.tl, seek: (u: number) => story!.seek(u) }
})
onBeforeUnmount(() => story?.destroy())
</script>

<template>
  <section ref="sectionEl" class="story" :class="{ 'story--static': isStatic }">
    <div class="stage">
      <aside class="rail" aria-hidden="true">
        <div class="line" /><div class="fill" :style="{ transform: `scaleY(${railFill})` }" />
        <ol>
          <li v-for="(label, i) in rail" :key="label" :class="{ active: active === i + 1, done: active > i + 1 }">{{ label }}</li>
        </ol>
      </aside>

      <div class="copy">
        <div class="step" :class="{ active: active === 0 }">
          <h1>{{ $t('story.title') }}</h1>
          <p>{{ $t('story.lead') }}</p>
          <span class="hint"><svg width="14" height="18" viewBox="0 0 14 18" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><path d="M7 1v15M1 10l6 7 6-7" /></svg>{{ $t('story.hint') }}</span>
        </div>
        <div v-for="(s, i) in steps" :key="i" class="step" :class="{ active: active === i + 1 }">
          <h2>{{ s.h }}</h2>
          <p>{{ s.p }}</p>
          <div class="facts">
            <template v-for="(f, j) in s.facts" :key="j"><i v-if="j" aria-hidden="true">·</i><b>{{ f.v }}</b><span v-if="f.l"> {{ f.l }}</span></template>
          </div>
          <div v-if="i === 4" class="cta">
            <NuxtLink class="btn" :to="localePath('/contacts')">{{ $t('story.cta') }}</NuxtLink>
            <NuxtLink class="link" :to="localePath('/shop/k5v1')">{{ $t('story.shop') }}</NuxtLink>
          </div>
        </div>
      </div>

      <div class="scene">
        <svg id="k5-scene" ref="svgEl" :viewBox="CAM.title" preserveAspectRatio="xMidYMid meet" role="img" :aria-label="$t('story.svgLabel')">
          <defs>
            <path id="k5-lam" :d="lam" />
            <clipPath id="k5-above"><path :d="clip" /></clipPath>
            <clipPath id="k5-bore"><path :d="ring(0.72)" :transform="`translate(0 ${TOPY})`" /></clipPath>
          </defs>

          <!-- site -->
          <rect x="-200" y="1400" width="1800" height="300" fill="var(--soil)" />
          <path d="M-200,1400H1600" stroke="var(--green)" stroke-width="3" />
          <g id="k5-ghost" fill="none" stroke="var(--metal)" stroke-width="2" stroke-dasharray="8 8" opacity=".5">
            <path d="M900,1400V500" /><rect x="775" y="180" width="250" height="240" rx="8" />
          </g>
          <rect id="k5-concrete" x="790" y="1400" width="220" height="130" fill="var(--concrete)" opacity="0" />
          <path id="k5-rebar" d="M810,1420H990M810,1460H990M810,1500H990M830,1410V1520M870,1410V1520M910,1410V1520M950,1410V1520M990,1410V1520" fill="none" stroke="var(--metal)" stroke-width="3" opacity="0" />
          <rect id="k5-anchor" x="860" y="1392" width="80" height="10" fill="var(--metal)" opacity="0" />
          <g id="k5-mast">
            <rect v-for="y in [1100, 800, 500]" :key="y" data-k="sect" x="889" :y="y" width="22" height="300" fill="var(--metal-2)" stroke="var(--metal)" stroke-width="2" opacity="0" />
            <rect v-for="y in [1096, 796]" :key="y" data-k="flange" x="878" :y="y" width="44" height="8" fill="var(--metal)" opacity="0" />
            <path id="k5-ladder" :d="ladder" fill="none" stroke="var(--metal)" stroke-width="2" opacity="0" />
          </g>
          <g id="k5-house">
            <path d="M1140,1400V1290L1250,1215L1360,1290V1400Z" fill="#fff" stroke="var(--ink)" stroke-width="3" stroke-linejoin="round" />
            <rect id="k5-window" x="1180" y="1310" width="44" height="44" fill="#fff" stroke="var(--ink)" stroke-width="2" />
            <rect id="k5-controller" x="1270" y="1300" width="40" height="28" rx="3" fill="#fff" stroke="var(--green)" stroke-width="3" opacity="0" />
            <path id="k5-ballast" d="M1272,1346h6l5-8 6,16 6-16 6,16 5-8h6" fill="none" stroke="var(--copper)" stroke-width="3" stroke-linejoin="round" opacity="0" />
            <g id="k5-battery" opacity="0"><rect x="1268" y="1362" width="46" height="26" rx="3" fill="#fff" stroke="var(--ink)" stroke-width="2.5" /><path d="M1278,1375h8M1282,1371v8M1298,1375h8" stroke="var(--ink)" stroke-width="2" /></g>
          </g>
          <path id="k5-cable" d="M900,500V1400C900,1470 1120,1470 1160,1400V1330H1270" fill="none" stroke="var(--copper)" stroke-width="3" opacity="0" />

          <!-- workshop: stator detail -->
          <g id="k5-stator" transform="translate(350 1250)">
            <!-- end turns under the stack: drawn first, so only the fringe below the front rim shows -->
            <g transform="translate(0 14)">
              <path v-for="(d, i) in coils" :key="i" data-k="coil-bottom" :d="d" :stroke="i % 2 ? '#a85c31' : '#c0733f'" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round" fill="none" opacity="0" />
            </g>
            <use v-for="i in LAMS" :key="i" data-k="lam" href="#k5-lam" :transform="lamShift(i - 1)" fill="#dfe3dd" stroke="#7d867f" stroke-width="1" fill-rule="evenodd" opacity="0" />
            <!-- copper filling the slots down the far wall of the bore -->
            <g clip-path="url(#k5-bore)">
              <path v-for="s in strips" :key="s.k" data-k="strip" :data-slot="s.k" :d="`M${s.x},${s.y1}V${s.y2}`" stroke="#b5673a" stroke-width="3.2" fill="none" opacity="0" />
            </g>
            <path v-for="(d, i) in coils" :key="i" data-k="coil" :d="d" :stroke="i % 2 ? '#a85c31' : '#c0733f'" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round" fill="none" :transform="`translate(0 ${TOPY})`" opacity="0" />
            <g clip-path="url(#k5-above)">
              <g id="k5-band" :transform="`translate(0 ${HOVER})`" opacity="0">
                <path :d="band" fill="#b9c0b8" />
                <rect v-for="(m, i) in mags" :key="i" data-k="mag" :x="m.x" :y="m.y" :width="m.w" :height="m.h" rx="1.5" fill="#15181a" opacity="0" />
              </g>
            </g>
            <g id="k5-top" :transform="`translate(0 ${HOVER})`" opacity="0">
              <path :d="ring(DR)" fill="#b9c0b8" stroke="#6f7872" stroke-width="1.5" />
              <path v-for="(d, i) in windows" :key="i" :d="d" fill="none" stroke="#6f7872" stroke-width="7" stroke-linecap="round" />
              <path :d="ring(0.24)" fill="#8f9891" stroke="#6f7872" stroke-width="1" />
              <circle v-for="a in [45, 135, 225, 315]" :key="a" :cx="(140 * 0.15 * Math.cos(a * Math.PI / 180)).toFixed(1)" :cy="(70 * 0.15 * Math.sin(a * Math.PI / 180)).toFixed(1)" r="2.4" fill="#4a504c" />
            </g>
            <g id="k5-housing" opacity="0">
              <path :d="hs.side" fill="#cfd4ce" stroke="#8a948c" stroke-width="1.5" />
              <path :d="hs.lid" fill="#e3e6e0" stroke="#8a948c" stroke-width="1.5" :transform="`translate(0 ${hs.lidY})`" />
              <template v-for="n in hs.nuts" :key="n.cx">
                <path :d="n.hexBottom" fill="#6f7872" />
                <rect :x="n.cx - 7" :y="n.cy - 6" width="14" height="6" fill="#7f8882" />
                <path :d="n.hexTop" fill="#aab1a9" stroke="#6f7872" stroke-width="0.8" />
                <rect :x="n.cx - 2.5" :y="n.cy - 16" width="5" height="10" fill="#9aa39c" stroke="#6f7872" stroke-width="0.6" />
                <ellipse :cx="n.cx" :cy="n.cy - 16" rx="2.5" ry="1.2" fill="#c3c9c1" stroke="#6f7872" stroke-width="0.6" />
              </template>
            </g>
            <g id="k5-shaft" :transform="`translate(0 ${HOVER})`" opacity="0">
              <!-- cylinder: straight sides, the front half of its base ellipse where it stands on the hub, a cap on top -->
              <path d="M-20,-100V-2A20,10 0 0 0 20,-2V-100Z" fill="#9aa39c" stroke="#6f7872" stroke-width="1" />
              <ellipse cx="0" cy="-100" rx="20" ry="10" fill="#c3c9c1" stroke="#6f7872" stroke-width="1" />
            </g>
          </g>

          <!-- workshop: the blade mould (two blue half-shells on a bench), fabric laid up inside, finished blades standing beside it -->
          <g id="k5-raw" opacity="0">
            <rect x="60" y="1290" width="260" height="6" fill="#8a948c" />
            <rect x="70" y="1250" width="240" height="40" rx="8" fill="#2b6cb0" />
            <rect x="82" y="1258" width="216" height="24" rx="6" fill="#4a8ad0" />
            <rect v-for="(fill, i) in layerFill" :key="i" data-k="mold-layer" :x="86 + i * 2" :y="1261 + i * 1.5" :width="208 - i * 4" :height="18 - i * 3" rx="4" :fill="fill" opacity="0" />
            <rect id="k5-mold-blade" x="82" y="1258" width="216" height="24" rx="6" fill="#fff" stroke="var(--green)" stroke-width="2" opacity="0" />
            <rect id="k5-mold-lid" x="70" y="1170" width="240" height="40" rx="8" fill="#2b6cb0" opacity="0" />
            <path v-for="x in rawX" :key="x" data-k="raw-outer" :d="rawBlade(x, 0)" fill="#fff" stroke="var(--green)" stroke-width="2.5" opacity="0" />
          </g>

          <!-- assembled rotor + generator -->
          <g id="k5-asm" transform="translate(350 1400)">
            <!-- shaft from the generator up to the hub where the struts meet -->
            <g id="k5-asm-shaft" opacity="0">
              <rect x="-7" y="-196" width="14" height="140" fill="var(--metal)" />
              <rect x="-14" y="-212" width="28" height="24" rx="3" fill="#3b423d" />
            </g>
            <g id="k5-rotor" opacity="0">
              <g fill="none" stroke="var(--metal)" stroke-width="6" stroke-linecap="round">
                <template v-for="(s, i) in struts0" :key="i"><path data-k="strut-top" :d="s.top" /><path data-k="strut-bot" :d="s.bot" /></template>
              </g>
              <g fill="none" stroke-linecap="butt">
                <template v-for="(d, i) in blades0" :key="i"><path data-k="blade-out" :d="d" stroke="var(--green)" stroke-width="18" /><path data-k="blade-face" :d="d" stroke="#fff" stroke-width="12" /></template>
              </g>
            </g>
            <!-- the finished generator: a wide steel drum with its lid and four nuts, same as the big one -->
            <g id="k5-gen" opacity="0">
              <path d="M-45,-56V-6A45,10 0 0 0 45,-6V-56Z" fill="#cfd4ce" stroke="var(--metal)" stroke-width="2" />
              <ellipse cx="0" cy="-56" rx="45" ry="10" fill="#e3e6e0" stroke="var(--metal)" stroke-width="2" />
              <rect v-for="[nx, ny] in [[-31, -60], [31, -60], [-31, -53], [31, -53]]" :key="`${nx}${ny}`" :x="nx - 2.5" :y="ny - 2" width="5" height="4" rx="1" fill="#6f7872" />
            </g>
          </g>
        </svg>
      </div>
    </div>
  </section>
</template>

<style scoped>
.story { height: 100vh; position: relative; overflow: hidden; }
.stage { height: 100%; display: grid; grid-template-columns: 150px minmax(300px, 36%) 1fr; gap: 32px; padding: 88px 40px 32px; }

.rail { position: relative; }
.rail .line, .rail .fill { position: absolute; left: 11px; top: 10px; bottom: 10px; width: 2px; background: var(--line); }
.rail .fill { background: var(--green); transform-origin: top; transform: scaleY(0); }
.rail ol { list-style: none; margin: 0; padding: 0; position: relative; display: flex; flex-direction: column; justify-content: space-between; height: 100%; }
.rail li { display: flex; align-items: center; gap: 12px; font-size: 12px; color: var(--ink-2); white-space: nowrap; }
.rail li::before { content: ''; width: 8px; height: 8px; border-radius: 50%; background: var(--bg); border: 2px solid var(--line); margin-left: 7px; flex: none; }
.rail li.done::before { border-color: var(--green); }
.rail li.active { color: var(--green); font-weight: 600; }
.rail li.active::before { border-color: var(--green); background: var(--green); }

/* Steps stack in one grid cell, so the row is as tall as the tallest step on every viewport. */
.copy { display: grid; align-items: center; }
.step { grid-area: 1 / 1; opacity: 0; transform: translateY(18px); transition: opacity 0.45s var(--ease), transform 0.45s var(--ease); pointer-events: none; }
.step.active { opacity: 1; transform: none; pointer-events: auto; }
.step h1 { font-size: clamp(38px, 4.4vw, 64px); line-height: 1.05; margin: 0 0 18px; letter-spacing: -0.02em; font-weight: 600; }
.step h2 { font-size: clamp(26px, 2.5vw, 36px); margin: 0 0 14px; }
.step p { margin: 0 0 18px; color: var(--ink-2); font-size: 17px; max-width: 44ch; }
.facts { padding-top: 14px; border-top: 1px solid var(--line); font-size: 15px; color: var(--ink-2); }
.facts b { color: var(--ink); font-weight: 600; font-size: 18px; }
.facts i { font-style: normal; margin: 0 10px; color: var(--line); }
.cta { margin-top: 26px; display: flex; flex-direction: column; align-items: flex-start; gap: 16px; }
.hint { display: inline-flex; align-items: center; gap: 10px; color: var(--ink-2); font-size: 14px; margin-top: 8px; }
.hint svg { animation: nudge 1.6s var(--ease) infinite; }
@keyframes nudge { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(5px); } }

.scene { position: relative; min-height: 0; }
.scene svg { width: 100%; height: 100%; display: block; }

/* Reduced motion or ?motion=off: no pin, the finished turbine, every step readable in order */
.story--static { height: auto; overflow: visible; }
.story--static .stage { height: auto; grid-template-columns: 1fr; padding-top: 88px; }
.story--static .rail { display: none; }
.story--static .copy { display: block; }
.story--static .step { opacity: 1; transform: none; pointer-events: auto; margin-bottom: 48px; transition: none; }
.story--static .scene { height: 80vh; }
.story--static .hint { display: none; }

@media (max-width: 900px) {
  .stage { grid-template-columns: 1fr; grid-template-rows: 1fr auto; gap: 8px; padding: 64px 16px 20px; }
  .rail { display: none; }
  .scene { order: 1; }
  .copy { order: 2; }
  .step h1 { font-size: clamp(32px, 9vw, 44px); }
  .step h2 { font-size: clamp(22px, 6vw, 28px); }
  .step p { font-size: 15px; }
}
@media (prefers-reduced-motion: reduce) {
  .hint svg { animation: none; }
  .step { transition: none; }
}
</style>
