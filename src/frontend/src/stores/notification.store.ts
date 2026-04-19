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
  // 記錄每個通知的 setTimeout handle，讓使用者手動 dismiss 時能清除預定 timer，
  // 避免 SPA 長時運作累積未觸發的 timer；SSR 時不會註冊（notify 只在 client 觸發）。
  const timers = new Map<string, ReturnType<typeof setTimeout>>()

  function notify(type: NotificationType, message: string, duration = 3000) {
    const id = crypto.randomUUID()
    notifications.value.push({ id, type, message, duration })

    // duration <= 0 表示通知不自動消失（需使用者手動關閉），
    // 適合需要使用者確認的重要訊息。
    if (duration > 0) {
      const handle = setTimeout(() => dismiss(id), duration)
      timers.set(id, handle)
    }

    return id
  }

  function dismiss(id: string) {
    const handle = timers.get(id)
    if (handle !== undefined) {
      clearTimeout(handle)
      timers.delete(id)
    }
    notifications.value = notifications.value.filter((n) => n.id !== id)
  }

  const success = (message: string, duration?: number) => notify('success', message, duration)
  const error = (message: string, duration?: number) => notify('error', message, duration)
  const warning = (message: string, duration?: number) => notify('warning', message, duration)
  const info = (message: string, duration?: number) => notify('info', message, duration)

  return { notifications, notify, dismiss, success, error, warning, info }
})
