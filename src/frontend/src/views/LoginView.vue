<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 px-4">
    <div class="w-full max-w-md bg-white rounded-xl shadow-sm border border-gray-200 p-8">
      <h2 class="text-2xl font-bold text-gray-900 mb-6">
        登入
      </h2>
      <LoginForm />
      <p class="mt-4 text-sm text-center text-gray-600">
        還沒有帳號？
        <RouterLink
          to="/register"
          class="text-blue-600 hover:underline"
        >
          立即註冊
        </RouterLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { RouterLink } from 'vue-router'
  import LoginForm from '@/components/auth/LoginForm.vue'
</script>
