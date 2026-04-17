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
  import { useId } from 'vue'

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

  // 使用 Vue 3.5+ 內建 useId() 產生 SSR-safe、穩定且唯一的 ID，
  // 確保 label[for] 與 input[id] 正確配對（無障礙需求），
  // 且不會因 Math.random() 在 SSR/CSR 產生不同值而造成 hydration mismatch。
  const inputId = useId()
</script>
