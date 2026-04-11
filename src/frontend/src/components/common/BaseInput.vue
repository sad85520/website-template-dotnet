<template>
  <div>
    <label v-if="label" :for="inputId" class="label">{{ label }}</label>
    <input
      :id="inputId"
      :type="type"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :class="['input', error ? 'border-red-500 focus:border-red-500 focus:ring-red-500' : '']"
      v-bind="$attrs"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    />
    <p v-if="error" class="mt-1 text-xs text-red-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
  import { computed } from 'vue'

  interface Props {
    modelValue: string
    type?: string
    label?: string
    placeholder?: string
    disabled?: boolean
    error?: string
  }

  withDefaults(defineProps<Props>(), {
    type: 'text',
    disabled: false,
  })

  defineEmits<{ 'update:modelValue': [value: string] }>()

  // 每次元件掛載時產生唯一 ID，確保 label 的 for 屬性與 input 的 id 正確對應，
  // 讓點擊 label 能聚焦對應的 input（無障礙需求）。
  // 注意：此 ID 在 SSR 環境下會造成 hydration mismatch，若需支援 SSR 應改用 useId()。
  const inputId = computed(() => `input-${Math.random().toString(36).slice(2, 9)}`)
</script>
