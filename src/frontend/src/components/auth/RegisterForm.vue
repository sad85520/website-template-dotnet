<template>
  <form class="space-y-4" @submit.prevent="handleSubmit">
    <BaseInput
      v-model="form.displayName"
      label="顯示名稱"
      placeholder="你的名字"
      :error="errors.displayName"
      required
    />
    <BaseInput
      v-model="form.email"
      type="email"
      label="電子郵件"
      placeholder="name@example.com"
      :error="errors.email"
      required
    />
    <BaseInput
      v-model="form.password"
      type="password"
      label="密碼"
      placeholder="至少 8 個字元"
      :error="errors.password"
      required
    />
    <BaseButton type="submit" :loading="isLoading" class="w-full">建立帳號</BaseButton>
  </form>
</template>

<script setup lang="ts">
  import { reactive } from 'vue'
  import BaseInput from '@/components/common/BaseInput.vue'
  import BaseButton from '@/components/common/BaseButton.vue'
  import { useAuth } from '@/composables/useAuth'
  import type { FieldError } from '@/types'

  const { register, isLoading } = useAuth()

  const form = reactive({ email: '', password: '', displayName: '' })
  const errors = reactive<{ email?: string; password?: string; displayName?: string }>({})

  async function handleSubmit() {
    errors.email = undefined
    errors.password = undefined
    errors.displayName = undefined

    const result = await register(form)

    if (!result.success && result.errors) {
      result.errors.forEach((e: FieldError) => {
        if (e.field === 'email') errors.email = e.message
        if (e.field === 'password') errors.password = e.message
        if (e.field === 'displayName') errors.displayName = e.message
      })
    }
  }
</script>
