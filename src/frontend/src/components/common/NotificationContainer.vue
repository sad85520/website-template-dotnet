<template>
  <div
    class="fixed top-4 right-4 z-50 flex flex-col gap-2"
    role="region"
    aria-label="通知訊息區"
    aria-live="polite"
  >
    <TransitionGroup name="notification">
      <div
        v-for="notification in notificationStore.notifications"
        :key="notification.id"
        :role="notification.type === 'error' ? 'alert' : 'status'"
        :class="['flex items-center gap-3 rounded-lg px-4 py-3 shadow-lg text-sm font-medium min-w-64', typeClasses[notification.type]]"
      >
        <span class="flex-1">{{ notification.message }}</span>
        <button
          type="button"
          class="text-current opacity-70 hover:opacity-100"
          aria-label="關閉通知"
          @click="notificationStore.dismiss(notification.id)"
        >
          <span aria-hidden="true">✕</span>
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<script setup lang="ts">
  import { useNotificationStore } from '@/stores'

  const notificationStore = useNotificationStore()

  const typeClasses = {
    success: 'bg-green-600 text-white',
    error: 'bg-red-600 text-white',
    warning: 'bg-yellow-500 text-white',
    info: 'bg-blue-600 text-white',
  }
</script>

<style scoped>
  .notification-enter-active,
  .notification-leave-active {
    transition: all 0.3s ease;
  }

  .notification-enter-from,
  .notification-leave-to {
    opacity: 0;
    transform: translateX(100%);
  }
</style>
