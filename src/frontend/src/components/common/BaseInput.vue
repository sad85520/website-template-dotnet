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

  const props = withDefaults(defineProps<Props>(), {
    type: 'text',
    disabled: false,
  })

  defineEmits<{ 'update:modelValue': [value: string] }>()

  const inputId = computed(() => `input-${Math.random().toString(36).slice(2, 9)}`)
</script>
