<template>
  <div>
    <h1 class="text-2xl font-bold text-gray-900 mb-4">Dashboard</h1>
    <p class="text-gray-600">歡迎回來，{{ currentUser?.displayName ?? '使用者' }}！</p>

    <div class="mt-8 grid grid-cols-1 md:grid-cols-3 gap-6">
      <div class="bg-white rounded-lg border border-gray-200 p-6 shadow-sm">
        <h3 class="text-sm font-medium text-gray-500">帳號</h3>
        <p class="mt-1 text-lg font-semibold text-gray-900">{{ currentUser?.email }}</p>
      </div>
      <div class="bg-white rounded-lg border border-gray-200 p-6 shadow-sm">
        <h3 class="text-sm font-medium text-gray-500">角色</h3>
        <p class="mt-1 text-lg font-semibold text-gray-900 capitalize">{{ currentUser?.role }}</p>
      </div>
      <div class="bg-white rounded-lg border border-gray-200 p-6 shadow-sm">
        <h3 class="text-sm font-medium text-gray-500">加入日期</h3>
        <p class="mt-1 text-lg font-semibold text-gray-900">
          {{ currentUser ? new Date(currentUser.createdAt).toLocaleDateString('zh-TW') : '-' }}
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { useAuthStore } from '@/stores'
  import { storeToRefs } from 'pinia'

  const authStore = useAuthStore()
  const { currentUser } = storeToRefs(authStore)
</script>
