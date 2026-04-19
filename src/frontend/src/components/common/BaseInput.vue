<template>
  <div>
    <label
      v-if="label"
      :for="inputId"
      class="label"
    >
      {{ label }}
      <span
        v-if="required"
        aria-hidden="true"
        class="text-red-600"
      >*</span>
    </label>
    <input
      :id="inputId"
      :type="type"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :required="required"
      :aria-invalid="error ? 'true' : undefined"
      :aria-describedby="error ? errorId : undefined"
      :class="['input', error ? 'border-red-500 focus:border-red-500 focus:ring-red-500' : '']"
      v-bind="$attrs"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    >
    <p
      v-if="error"
      :id="errorId"
      class="mt-1 text-xs text-red-600"
      role="alert"
    >
      {{ error }}
    </p>
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
    // required 明示宣告於 Props 而非僅靠 $attrs 傳遞，讓 TS 型別提示完整，
    // 同時作為 aria-required / aria-invalid 邏輯的單一來源。
    required?: boolean
    error?: string
  }

  withDefaults(defineProps<Props>(), {
    type: 'text',
    label: '',
    placeholder: '',
    disabled: false,
    required: false,
    error: '',
  })

  defineEmits<{ 'update:modelValue': [value: string] }>()

  // 使用 Vue 3.5+ 內建 useId() 產生 SSR-safe、穩定且唯一的 ID，
  // 確保 label[for] 與 input[id] 正確配對（無障礙需求），
  // 且不會因 Math.random() 在 SSR/CSR 產生不同值而造成 hydration mismatch。
  const inputId = useId()
  // aria-describedby 指向錯誤訊息 id，螢幕閱讀器才能在 focus input 時唸出錯誤內容。
  const errorId = `${inputId}-error`
</script>
