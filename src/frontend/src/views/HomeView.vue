<template>
  <div class="text-center py-16">
    <h1 class="text-4xl font-bold text-gray-900 mb-4">Website Template</h1>
    <p class="text-lg text-gray-600 mb-8">Vue 3 + .NET 9 現代前後端分離架構起手式</p>

    <div class="flex justify-center gap-4">
      <RouterLink v-if="!isAuthenticated" to="/register" class="btn-primary">
        開始使用
      </RouterLink>
      <RouterLink v-else to="/dashboard" class="btn-primary">進入 Dashboard</RouterLink>
      <a
        href="/api/swagger"
        target="_blank"
        rel="noopener noreferrer"
        class="btn-secondary"
      >
        API 文件
      </a>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { RouterLink } from 'vue-router'
  import { useAuthStore } from '@/stores'
  import { storeToRefs } from 'pinia'

  const authStore = useAuthStore()
  const { isAuthenticated } = storeToRefs(authStore)
</script>
