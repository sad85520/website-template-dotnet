import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores'
import { useNotificationStore } from '@/stores'
import type { AuthActionResult, LoginRequest, LoginResponse, RegisterRequest, UserDto } from '@/types'

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
    // authStore.login 已將 AxiosError 在內部轉為 AuthActionResult，正常流程不會 throw；
    // 但若底層有非預期例外（如 pinia plugin 異常、記憶體不足），仍需在 composable
    // 層兜底避免整個 UI 回傳 unhandled rejection 導致使用者看不到任何錯誤提示。
    try {
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
    } catch (error: unknown) {
      if (import.meta.env.DEV) {
        console.error('useAuth.login unexpected error:', error)
      }
      const fallback: AuthActionResult<LoginResponse> = {
        success: false,
        data: null,
        message: '登入失敗，請稍後再試',
        errors: null,
      }
      notificationStore.error(fallback.message ?? '登入失敗，請稍後再試')
      return fallback
    }
  }

  async function register(data: RegisterRequest) {
    try {
      const result = await authStore.register(data)

      if (result.success) {
        notificationStore.success('註冊成功，請登入')
        await router.push({ name: 'login' })
      } else {
        notificationStore.error(result.message ?? '註冊失敗，請稍後再試')
      }

      return result
    } catch (error: unknown) {
      if (import.meta.env.DEV) {
        console.error('useAuth.register unexpected error:', error)
      }
      const fallback: AuthActionResult<UserDto> = {
        success: false,
        data: null,
        message: '註冊失敗，請稍後再試',
        errors: null,
      }
      notificationStore.error(fallback.message ?? '註冊失敗，請稍後再試')
      return fallback
    }
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
