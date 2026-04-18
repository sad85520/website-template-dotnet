<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    :class="[variantClasses, 'btn']"
    v-bind="$attrs"
  >
    <span
      v-if="loading"
      class="mr-2 h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
    />
    <slot />
  </button>
</template>

<script setup lang="ts">
  import { computed } from 'vue'

  interface Props {
    type?: 'button' | 'submit' | 'reset'
    variant?: 'primary' | 'secondary' | 'danger'
    disabled?: boolean
    loading?: boolean
  }

  const props = withDefaults(defineProps<Props>(), {
    type: 'button',
    variant: 'primary',
    disabled: false,
    loading: false,
  })

  const variantClasses = computed(() => {
    const map = {
      primary: 'btn-primary',
      secondary: 'btn-secondary',
      danger: 'bg-red-600 text-white hover:bg-red-700 focus:ring-red-500',
    }
    return map[props.variant]
  })
</script>
