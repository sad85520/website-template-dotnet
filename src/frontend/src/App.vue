<template>
  <RouterView />
  <NotificationContainer />
</template>

<script setup lang="ts">
  import { RouterView } from 'vue-router'
  import NotificationContainer from '@/components/common/NotificationContainer.vue'
</script>
