<template>
  <div class="min-h-screen flex items-center justify-center">
    <div class="text-center">
      <h1 class="text-6xl font-bold text-gray-300">
        404
      </h1>
      <p class="mt-4 text-xl text-gray-600">
        找不到頁面
      </p>
      <RouterLink
        to="/"
        class="mt-6 inline-block btn-primary"
      >
        回首頁
      </RouterLink>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { RouterLink } from 'vue-router'
</script>
