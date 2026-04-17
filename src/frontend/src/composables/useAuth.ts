import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores'
import { useNotificationStore } from '@/stores'
import type { LoginRequest, RegisterRequest } from '@/types'

// 僅允許同源的相對路徑作為登入後的重導向目標，
// 防止攻擊者透過 ?redirect=//evil.example 進行 Open Redirect 攻擊。
// 必須以單一 "/" 開頭、且不得以 "//" 或 "/\\" 開頭（後者為 protocol-relative URL）。
function safeRedirect(raw: unknown): string {
  if (typeof raw !== 'string' || raw.length === 0) return '/'
  if (!raw.startsWith('/')) return '/'
  if (raw.startsWith('//') || raw.startsWith('/\\')) return '/'
  return raw
}

export function useAuth() {
  const router = useRouter()
  const route = useRoute()
  const authStore = useAuthStore()
  const notificationStore = useNotificationStore()

  async function login(credentials: LoginRequest) {
    const result = await authStore.login(credentials)

    if (result.success) {
      notificationStore.success('登入成功')
      // redirect 參數由 router guard 在未授權跳轉時帶入，
      // 讓使用者登入後回到原本想前往的頁面而非固定首頁；
      // 經過 safeRedirect 過濾以避免 Open Redirect。
      await router.push(safeRedirect(route.query.redirect))
    } else {
      notificationStore.error(result.message ?? '登入失敗，請確認帳號密碼')
    }

    return result
  }

  async function register(data: RegisterRequest) {
    const result = await authStore.register(data)

    if (result.success) {
      notificationStore.success('註冊成功，請登入')
      await router.push({ name: 'login' })
    } else {
      notificationStore.error(result.message ?? '註冊失敗，請稍後再試')
    }

    return result
  }

  async function logout() {
    await authStore.logout()
    notificationStore.info('已登出')
    await router.push({ name: 'login' })
  }

  return {
    isAuthenticated: authStore.isAuthenticated,
    currentUser: authStore.currentUser,
    isLoading: authStore.isLoading,
    login,
    register,
    logout,
  }
}
