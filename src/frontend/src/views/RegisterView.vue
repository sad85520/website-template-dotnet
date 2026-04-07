<template>
  <div class="min-h-screen flex items-center justify-center bg-gray-50 px-4">
    <div class="w-full max-w-md bg-white rounded-xl shadow-sm border border-gray-200 p-8">
      <h2 class="text-2xl font-bold text-gray-900 mb-6">建立帳號</h2>
      <RegisterForm />
      <p class="mt-4 text-sm text-center text-gray-600">
        已有帳號？
        <RouterLink to="/login" class="text-blue-600 hover:underline">返回登入</RouterLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { RouterLink } from 'vue-router'
  import RegisterForm from '@/components/auth/RegisterForm.vue'
</script>
