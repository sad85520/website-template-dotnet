<template>
  <header class="bg-white shadow-sm border-b border-gray-200">
    <div class="container mx-auto px-4 h-16 flex items-center justify-between">
      <RouterLink
        to="/"
        class="text-xl font-semibold text-gray-900"
      >
        Website Template
      </RouterLink>

      <nav class="flex items-center gap-4">
        <template v-if="isAuthenticated">
          <RouterLink
            to="/dashboard"
            class="text-sm text-gray-600 hover:text-gray-900"
          >
            Dashboard
          </RouterLink>
          <button
            class="btn-secondary text-sm"
            @click="logout"
          >
            登出
          </button>
        </template>
        <template v-else>
          <RouterLink
            to="/login"
            class="text-sm text-gray-600 hover:text-gray-900"
          >
            登入
          </RouterLink>
          <RouterLink
            to="/register"
            class="btn-primary text-sm"
          >
            註冊
          </RouterLink>
        </template>
      </nav>
    </div>
  </header>
</template>

<script setup lang="ts">
  import { RouterLink } from 'vue-router'
  import { useAuth } from '@/composables/useAuth'

  const { isAuthenticated, logout } = useAuth()
</script>
