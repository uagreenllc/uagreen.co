<script setup lang="ts">
import type { ProductOption } from '~/types/api'
import type { Selection } from '~/utils/pricing'

// One <select> per option group; v-model is { optionId: valueId }.
defineProps<{ options: ProductOption[] }>()
const selected = defineModel<Selection>({ required: true })
const money = useMoney()
</script>

<template>
  <div v-if="options.length" class="options">
    <label v-for="o in options" :key="o.id" class="field">
      <span>{{ o.name }}</span>
      <select v-model="selected[o.id]">
        <option v-for="v in o.values" :key="v.id" :value="v.id">
          {{ v.value }}{{ v.priceDelta ? ` (+${money(v.priceDelta)})` : '' }}
        </option>
      </select>
    </label>
  </div>
</template>
