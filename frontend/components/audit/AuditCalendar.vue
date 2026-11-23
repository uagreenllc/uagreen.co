<script setup lang="ts">
import type { AuditSlots } from '~/types/api'

const props = defineProps<{ slots: AuditSlots }>()
const date = defineModel<string | null>({ required: true })
const { locale } = useI18n()

const weeks = computed(() => calendarWeeks(props.slots.from, props.slots.to, props.slots.dates))
// Intl gives weekday and month names in both locales; timeZone UTC because the dates are plain calendar dates.
const fmt = (opts: Intl.DateTimeFormatOptions) => new Intl.DateTimeFormat(intlLocale(locale.value), { timeZone: 'UTC', ...opts })
const weekdays = computed(() => (weeks.value[0] ?? []).map(d => fmt({ weekday: 'short' }).format(new Date(d.iso))))
const span = computed(() => {
  const m = fmt({ month: 'long', year: 'numeric' })
  const a = m.format(new Date(props.slots.from)), b = m.format(new Date(props.slots.to))
  const s = a === b ? a : `${a} — ${b}`
  return s.charAt(0).toUpperCase() + s.slice(1) // not CSS capitalize: it would also turn «р.» into «Р.»
})
const monthShort = (iso: string) => fmt({ month: 'short' }).format(new Date(iso))
const label = (iso: string) => fmt({ day: 'numeric', month: 'long', weekday: 'long' }).format(new Date(iso))
</script>

<template>
  <div class="cal">
    <p class="span">{{ span }}</p>
    <div class="grid" role="group" :aria-label="span">
      <span v-for="w in weekdays" :key="w" class="wd" aria-hidden="true">{{ w }}</span>
      <template v-for="(week, i) in weeks" :key="i">
        <template v-for="d in week" :key="d.iso">
          <span v-if="!d.inWindow" class="pad" aria-hidden="true" />
          <button
            v-else type="button" class="day" :disabled="!d.free" :aria-pressed="date === d.iso" :aria-label="label(d.iso)"
            @click="date = d.iso"
          >
            {{ d.day }}<small v-if="d.day === 1">{{ monthShort(d.iso) }}</small>
          </button>
        </template>
      </template>
    </div>
  </div>
</template>

<style scoped>
.span { margin: 0 0 12px; font-weight: 600; }
.grid { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 6px; max-width: 460px; }
.wd { font-size: 12px; color: var(--ink-2); text-align: center; padding-bottom: 4px; text-transform: capitalize; }
.day {
  aspect-ratio: 1; border: 1px solid var(--line); border-radius: 10px; background: transparent;
  font: inherit; font-weight: 600; color: var(--ink); cursor: pointer; display: grid; place-content: center; line-height: 1.1;
  transition: background 0.15s var(--ease), border-color 0.15s var(--ease);
}
.day small { font-size: 10px; font-weight: 500; color: inherit; opacity: 0.7; }
.day:hover:not(:disabled) { border-color: var(--green); }
.day[aria-pressed='true'] { background: var(--green); border-color: var(--green); color: #fff; }
.day:disabled { border-color: transparent; color: var(--line); font-weight: 400; cursor: default; }
.day:focus-visible { outline: 2px solid var(--green); outline-offset: 2px; }
</style>
