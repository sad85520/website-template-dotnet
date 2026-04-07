<template>
  <div class="min-h-screen flex flex-col">
    <AppHeader />
    <main class="flex-1 container mx-auto px-4 py-8">
      <RouterView />
    </main>
    <AppFooter />
  </div>
</template>

<script setup lang="ts">
  import { RouterView } from 'vue-router'
  import AppHeader from './AppHeader.vue'
  import AppFooter from './AppFooter.vue'
</script>
