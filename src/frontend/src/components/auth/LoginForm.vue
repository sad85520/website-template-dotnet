<template>
  <form class="space-y-4" @submit.prevent="handleSubmit">
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
      placeholder="••••••••"
      :error="errors.password"
      required
    />
    <BaseButton type="submit" :loading="isLoading" class="w-full">登入</BaseButton>
  </form>
</template>

<script setup lang="ts">
  import { reactive } from 'vue'
  import BaseInput from '@/components/common/BaseInput.vue'
  import BaseButton from '@/components/common/BaseButton.vue'
  import { useAuth } from '@/composables/useAuth'
  import type { FieldError } from '@/types'

  const { login, isLoading } = useAuth()

  const form = reactive({ email: '', password: '' })
  const errors = reactive<{ email?: string; password?: string }>({})

  async function handleSubmit() {
    errors.email = undefined
    errors.password = undefined

    const result = await login(form)

    if (!result.success && result.errors) {
      result.errors.forEach((e: FieldError) => {
        if (e.field === 'email') errors.email = e.message
        if (e.field === 'password') errors.password = e.message
      })
    }
  }
</script>
