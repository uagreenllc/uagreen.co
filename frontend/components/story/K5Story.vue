<script setup lang="ts">
import {
  LAMS, TOPY, HOVER, DR, HR, CAM,
  lamPath, bandPath, bandClipPath, magnets, slotStrips, coilPath, flangeWindows, housing, rawBlade, bladePath, strutPaths, ladderSegment, ring,
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
const rawX = [330, 370, 410] // finished blades standing beside the mould
const layerFill = ['#e9f0e6', '#dde8d9', '#d1e0cc', '#c6d8c0', '#bbd0b5'] // glass fabric, 48 → 400 g/m²
const blades0 = ([0, 1, 2] as const).map(i => bladePath(i, 0))
const struts0 = strutPaths(0)
const lamShift = (i: number) => `translate(0 ${-4 * i - 40})`

let story: Story | undefined
onMounted(async () => {
  const reduce = route.query.motion === 'off' || window.matchMedia('(prefers-reduced-motion: reduce)').matches
  isStatic.value = reduce
  const [{ gsap }, { ScrollTrigger }] = await Promise.all([import('gsap'), import('gsap/ScrollTrigger')])
  if (!svgEl.value || !sectionEl.value) return // navigated away while gsap was loading
  const svg = svgEl.value
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
    mould: one<SVGGElement>('#k5-mould'),
    moldLayers: all('[data-k=mold-layer]'),
    moldLid: one('#k5-mold-lid'),
    moldBlade: one('#k5-mold-blade'),
    rawOuter: all<SVGPathElement>('[data-k=raw-outer]'),
    assembly: one<SVGGElement>('#k5-asm'),
    asmShaft: one('#k5-asm-shaft'),
    rotorBack: one<SVGGElement>('#k5-rotor-back'),
    rotorFront: one<SVGGElement>('#k5-rotor-front'),
    rotorSets: (['b', 'f'] as const).map(p => ({
      top: all<SVGPathElement>(`[data-k=${p}-strut-top]`),
      bot: all<SVGPathElement>(`[data-k=${p}-strut-bot]`),
      blade: all<SVGPathElement>(`[data-k=${p}-blade]`),
    })),
    gen: one<SVGGElement>('#k5-gen'),
    ghost: one('#k5-ghost'),
    rebar: one<SVGPathElement>('#k5-rebar'),
    concrete: one('#k5-concrete'),
    anchor: one('#k5-anchor'),
    sections: all('[data-k=sect]'),
    trailer: one('#k5-trailer'),
    cable: one<SVGPathElement>('#k5-cable'),
    cable2: one<SVGPathElement>('#k5-cable2'),
    crane: one<SVGGElement>('#k5-crane'),
    boom: one<SVGPathElement>('#k5-boom'),
    boomBase: one<SVGPathElement>('#k5-boom-base'),
    cyl: one<SVGPathElement>('#k5-cyl'),
    hub: one('#k5-hub'),
    hookLine: one<SVGPathElement>('#k5-hook-line'),
    hook: one('#k5-hook'),
    controller: one('#k5-controller'),
    ballast: one('#k5-ballast'),
    battery: one('#k5-battery'),
    window: one('#k5-window'),
    glow: one('#k5-glow'),
    beam: one('[data-k=beam]'),
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
            <!-- the sections arrive lying on a trailer; the crane lifts each, swings it upright and sets it on the previous one -->
            <g id="k5-trailer" opacity="0">
              <rect x="930" y="1374" width="320" height="12" rx="2" fill="#6f7872" />
              <rect x="918" y="1378" width="14" height="4" fill="#6f7872" />
              <circle cx="985" cy="1392" r="8" fill="#3b423d" /><circle cx="1195" cy="1392" r="8" fill="#3b423d" />
            </g>
            <!-- each section comes complete: tube, its ladder segment and (on the two lower ones) the top flange -->
            <g v-for="(y, i) in [1100, 800, 500]" :key="y" data-k="sect" opacity="0">
              <rect x="889" :y="y" width="22" height="300" fill="var(--metal-2)" stroke="var(--metal)" stroke-width="2" />
              <path :d="ladderSegment(y)" fill="none" stroke="var(--metal)" stroke-width="2" />
              <rect v-if="i < 2" x="878" :y="y - 4" width="44" height="8" fill="var(--metal)" />
            </g>
          </g>
          <g id="k5-house">
            <!-- house: one 2.5px ink outline everywhere, round joins; walls, chimney with cap, gable roof with overhang,
                 attic window, door with step, window with sill -->
            <g stroke="var(--ink)" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round">
              <rect x="1280" y="1290" width="220" height="110" rx="6" fill="#fff" />
              <rect x="1440" y="1232" width="16" height="44" rx="2" fill="#8a948c" />
              <rect x="1436" y="1226" width="24" height="8" rx="2" fill="#6f7872" />
              <path d="M1268,1296L1390,1206L1512,1296Z" fill="#8a948c" />
              <path d="M1310,1265H1470M1340,1243H1440" stroke-width="1.5" stroke="#6f7872" />
              <circle cx="1390" cy="1256" r="9" fill="#fff" />
              <path d="M1390,1247V1265M1381,1256H1399" stroke-width="1.5" />
              <rect x="1366" y="1398" width="38" height="5" rx="1" fill="#c3c9c1" />
              <path d="M1372,1400V1353a13,13 0 0 1 26,0V1400Z" fill="var(--green)" />
              <rect id="k5-glow" x="1308" y="1298" width="68" height="68" rx="14" fill="#f0cc5a" stroke="none" opacity="0" />
              <path data-k="beam" d="M1322,1354H1362L1400,1404H1284Z" fill="#f0cc5a" stroke="none" opacity="0" />
              <rect id="k5-window" x="1320" y="1310" width="44" height="44" rx="4" fill="#fff" />
              <path d="M1342,1310V1354M1320,1332H1364" stroke-width="1.5" />
              <rect x="1315" y="1354" width="54" height="5" rx="1" fill="#c3c9c1" />
            </g>
            <circle cx="1393" cy="1374" r="1.8" fill="#e3e6e0" />
            <rect id="k5-controller" x="1410" y="1300" width="40" height="28" rx="3" fill="#fff" stroke="var(--green)" stroke-width="2.5" opacity="0" />
            <path id="k5-ballast" d="M1412,1346h6l5-8 6,16 6-16 6,16 5-8h6" fill="none" stroke="var(--copper)" stroke-width="2.5" stroke-linejoin="round" opacity="0" />
            <g id="k5-battery" opacity="0"><rect x="1408" y="1362" width="46" height="26" rx="3" fill="#fff" stroke="var(--ink)" stroke-width="2.5" /><path d="M1418,1375h8M1422,1371v8M1438,1375h8" stroke="var(--ink)" stroke-width="2" /></g>
          </g>
          <!-- cable in two runs: down the mast and underground to the house (wide view), then under the floor up into the
               equipment (house view). The first run ends horizontally so the second continues it without a kink. -->
          <path id="k5-cable" d="M900,500V1400C900,1480 1180,1440 1290,1440" fill="none" stroke="var(--copper)" stroke-width="3" stroke-linecap="round" opacity="0" />
          <path id="k5-cable2" d="M1290,1440H1400Q1430,1440 1430,1410V1388" fill="none" stroke="var(--copper)" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" opacity="0" />
          <!-- mobile crane between the workshop and the mast: it stacks the mast, lifts the turbine on, then drives off -->
          <g id="k5-crane" opacity="0">
            <!-- outriggers on pads, chassis, cab, counterweight, turntable, hydraulic cylinder, two-stage telescopic boom, hook block -->
            <rect x="618" y="1378" width="26" height="6" fill="#8a948c" /><rect x="836" y="1378" width="26" height="6" fill="#8a948c" />
            <rect x="612" y="1388" width="14" height="12" fill="#3b423d" /><rect x="854" y="1388" width="14" height="12" fill="#3b423d" />
            <rect x="640" y="1350" width="200" height="36" rx="4" fill="#d8b24a" />
            <rect x="642" y="1322" width="48" height="30" rx="4" fill="#3b423d" />
            <rect x="648" y="1328" width="30" height="14" rx="2" fill="#bcd3e6" />
            <rect x="786" y="1326" width="54" height="24" rx="3" fill="#3b423d" />
            <rect x="700" y="1340" width="90" height="10" rx="2" fill="#c9a43f" />
            <template v-for="cx in [672, 740, 808]" :key="cx"><circle :cx="cx" cy="1388" r="12" fill="#3b423d" /><circle :cx="cx" cy="1388" r="5" fill="#8a948c" /></template>
            <path id="k5-cyl" d="M722,1352L740,1300" stroke="#8a948c" stroke-width="6" stroke-linecap="round" fill="none" />
            <path id="k5-boom-base" d="M760,1346L796,1341" stroke="#c9a43f" stroke-width="14" stroke-linecap="round" fill="none" />
            <path id="k5-boom" d="M796,1341L840,1336" stroke="#d8b24a" stroke-width="9" stroke-linecap="round" fill="none" />
            <circle cx="760" cy="1346" r="9" fill="#8a948c" stroke="#3b423d" stroke-width="2" />
            <path id="k5-hook-line" d="M840,1336V1336" stroke="#3b423d" stroke-width="2" fill="none" />
            <g id="k5-hook" transform="translate(840 1336)">
              <rect x="-4" y="0" width="8" height="12" rx="1" fill="#3b423d" />
              <path d="M0,12v6a5,5 0 1,0 5,-5" stroke="#3b423d" stroke-width="2.5" fill="none" stroke-linecap="round" />
            </g>
          </g>

          <!-- workshop: stator detail -->
          <g id="k5-stator" transform="translate(470 1250)">
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
            <!-- the mould on its bench: two blue half-shells with bolted flanges, hinged along y=15 (local); the upper one flips shut -->
            <g id="k5-mould" transform="translate(40 1300)">
              <rect x="-10" y="64" width="276" height="8" rx="2" fill="#a08a66" />
              <rect x="10" y="72" width="8" height="28" fill="#8a7658" /><rect x="238" y="72" width="8" height="28" fill="#8a7658" />
              <!-- lower shell: the cavity is the blade's planform, 240 × 24, running the full length; flanges only along the long sides -->
              <rect x="0" y="20" width="256" height="44" rx="10" fill="#2b6cb0" stroke="#1f4f86" stroke-width="1.5" />
              <rect x="8" y="28" width="240" height="24" fill="#1f4f86" />
              <circle v-for="i in 10" :key="i" :cx="12 + (i - 1) * 25.8" cy="61" r="2.2" fill="#dbe3ea" />
              <rect v-for="(fill, i) in layerFill" :key="i" data-k="mold-layer" :x="9 + i" :y="29 + i * 0.6" :width="238 - 2 * i" :height="22 - 1.2 * i" :fill="fill" opacity="0" />
              <rect data-k="mold-layer" x="14" y="31" width="228" height="4" rx="2" fill="#fff" fill-opacity="0.4" opacity="0" />
              <rect id="k5-mold-blade" x="8" y="28" width="240" height="24" fill="#fff" stroke="var(--green)" stroke-width="2.5" opacity="0" />
              <!-- upper shell, drawn last so it lands on top when it flips shut on the hinge at y=16 -->
              <g id="k5-mold-lid">
                <rect x="0" y="-28" width="256" height="44" rx="10" fill="#2b6cb0" stroke="#1f4f86" stroke-width="1.5" />
                <rect x="8" y="-20" width="240" height="24" fill="#5d97d6" />
                <circle v-for="i in 10" :key="i" :cx="12 + (i - 1) * 25.8" cy="-25" r="2.2" fill="#dbe3ea" />
              </g>
            </g>
            <path v-for="x in rawX" :key="x" data-k="raw-outer" :d="rawBlade(x, 0)" fill="#fff" stroke="var(--green)" stroke-width="2.5" opacity="0" />
          </g>

          <!-- assembled rotor + generator -->
          <g id="k5-asm" transform="translate(470 1400)">
            <!-- z-order: far blades, generator, shaft (in front of the generator's stub), near blades, hub on top of every strut end.
                 Both rotor groups hold a full set of struts + blades; drawBlades shows each blade in the group matching its depth. -->
            <g id="k5-rotor-back" opacity="0">
              <template v-for="(s, i) in struts0" :key="i">
                <path data-k="b-strut-top" :d="s.top" fill="none" stroke="var(--metal)" stroke-width="6" stroke-linecap="round" />
                <path data-k="b-strut-bot" :d="s.bot" fill="none" stroke="var(--metal)" stroke-width="6" stroke-linecap="round" />
                <path data-k="b-blade" :d="blades0[i]" fill="#fff" stroke="var(--green)" stroke-width="2.5" opacity="0" />
              </template>
            </g>
            <!-- the finished generator: the big housing from stage 1 at scale 0.19, so the shrink lands on it exactly -->
            <g id="k5-gen" opacity="0">
              <path d="M-30,-41.6V-15A30,15 0 0 0 30,-15V-41.6Z" fill="#cfd4ce" stroke="#8a948c" stroke-width="0.8" />
              <ellipse cx="0" cy="-41.6" rx="30" ry="15" fill="#e3e6e0" stroke="#8a948c" stroke-width="0.8" />
              <rect v-for="[nx, ny] in [[-21, -52], [21, -52], [-21, -31], [21, -31]]" :key="`${nx}${ny}`" :x="nx - 1.5" :y="ny - 2.5" width="3" height="3" rx="0.6" fill="#6f7872" />
              <!-- the rotor's shaft end sticking out of the lid; the mast-side shaft grows from it -->
              <rect x="-4" y="-60" width="8" height="18.4" fill="#9aa39c" stroke="#6f7872" stroke-width="0.5" />
              <ellipse cx="0" cy="-60" rx="4" ry="2" fill="#c3c9c1" stroke="#6f7872" stroke-width="0.5" />
            </g>
            <!-- shaft from the generator's stub up to the hub where the struts meet -->
            <g id="k5-asm-shaft" opacity="0">
              <rect x="-4" y="-196" width="8" height="136" fill="#9aa39c" />
            </g>
            <g id="k5-rotor-front" opacity="0">
              <template v-for="(s, i) in struts0" :key="i">
                <path data-k="f-strut-top" :d="s.top" fill="none" stroke="var(--metal)" stroke-width="6" stroke-linecap="round" />
                <path data-k="f-strut-bot" :d="s.bot" fill="none" stroke="var(--metal)" stroke-width="6" stroke-linecap="round" />
                <path data-k="f-blade" :d="blades0[i]" fill="#fff" stroke="var(--green)" stroke-width="2.5" opacity="0" />
              </template>
            </g>
            <rect id="k5-hub" x="-10" y="-210" width="20" height="20" rx="3" fill="#3b423d" opacity="0" />
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
