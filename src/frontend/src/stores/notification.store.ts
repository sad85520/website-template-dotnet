import { defineStore } from 'pinia'
import { ref } from 'vue'

export type NotificationType = 'success' | 'error' | 'warning' | 'info'

export interface Notification {
  id: string
  type: NotificationType
  message: string
  duration?: number
}

export const useNotificationStore = defineStore('notification', () => {
  const notifications = ref<Notification[]>([])

  function notify(type: NotificationType, message: string, duration = 3000) {
    const id = crypto.randomUUID()
    notifications.value.push({ id, type, message, duration })

    if (duration > 0) {
      setTimeout(() => dismiss(id), duration)
    }

    return id
  }

  function dismiss(id: string) {
    notifications.value = notifications.value.filter((n) => n.id !== id)
  }

  const success = (message: string, duration?: number) => notify('success', message, duration)
  const error = (message: string, duration?: number) => notify('error', message, duration)
  const warning = (message: string, duration?: number) => notify('warning', message, duration)
  const info = (message: string, duration?: number) => notify('info', message, duration)

  return { notifications, notify, dismiss, success, error, warning, info }
})
