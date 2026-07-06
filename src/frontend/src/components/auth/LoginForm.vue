<template>
  <form
    class="space-y-4"
    @submit.prevent="handleSubmit"
  >
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
    <BaseButton
      type="submit"
      :loading="isLoading"
      class="w-full"
    >
      登入
    </BaseButton>
  </form>
</template>

<script setup lang="ts">
  import { reactive } from 'vue'
  import BaseInput from '@/components/common/BaseInput.vue'
  import BaseButton from '@/components/common/BaseButton.vue'
  import { useAuth } from '@/composables/useAuth'

  const { login, isLoading } = useAuth()

  const form = reactive({ email: '', password: '' })
  const errors = reactive<{ email?: string; password?: string }>({})

  // result.errors 對應後端 ValidationProblemDetails.errors：key 為欄位名稱（大小寫與
  // C# DTO 屬性一致，例如 "Email"、"Password"），value 為該欄位的錯誤訊息陣列，
  // 表單只顯示第一則訊息。
  async function handleSubmit() {
    errors.email = undefined
    errors.password = undefined

    const result = await login(form)

    if (!result.success && result.errors) {
      errors.email = result.errors.Email?.[0]
      errors.password = result.errors.Password?.[0]
    }
  }
</script>
