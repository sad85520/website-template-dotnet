import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { isAxiosError } from 'axios'
import { authApi } from '@/api'
import type {
  AuthActionResult,
  LoginRequest,
  LoginResponse,
  ProblemDetails,
  RegisterRequest,
  UserDto,
} from '@/types'

// 後端已不再回傳信封（見 docs/adr/ADR-004-rfc7807-problem-details.md）：
// 成功回應直接是資源本體，失敗回應是 RFC 7807 ProblemDetails（application/problem+json）。
// 這裡把 AxiosError<ProblemDetails> 轉成 AuthActionResult，讓 caller 不需要各自寫 try/catch。
function extractApiError<T>(error: unknown): AuthActionResult<T> {
  if (isAxiosError<ProblemDetails>(error) && error.response?.data) {
    const problem = error.response.data
    return {
      success: false,
      data: null,
      message: problem.detail ?? problem.title ?? '請求失敗，請稍後再試',
      errors: problem.errors ?? null,
    }
  }
  return {
    success: false,
    data: null,
    message: '網路錯誤，請稍後再試',
    errors: null,
  }
}

export const useAuthStore = defineStore('auth', () => {
  // accessToken 僅存於記憶體（Pinia reactive state），不存入 localStorage，
  // 防止 XSS 攻擊直接竊取 token；頁面重新整理後透過 tryRefreshToken 以 httpOnly cookie 還原。
  const accessToken = ref<string | null>(null)
  const currentUser = ref<UserDto | null>(null)
  const isLoading = ref(false)

  const isAuthenticated = computed(() => !!accessToken.value)

  function setAccessToken(token: string): void {
    accessToken.value = token
  }

  function clearAuth(): void {
    accessToken.value = null
    currentUser.value = null
  }

  async function login(credentials: LoginRequest): Promise<AuthActionResult<LoginResponse>> {
    isLoading.value = true
    try {
      const response = await authApi.login(credentials)
      accessToken.value = response.data.accessToken
      await fetchCurrentUser()
      return { success: true, data: response.data, message: null, errors: null }
    } catch (error: unknown) {
      // 沒有 catch 的話 AxiosError 會成為 unhandled rejection，UI 僅能靠全域
      // interceptor 收拾且 caller 拿不到錯誤訊息。轉為 AuthActionResult，
      // 讓 composable/useAuth 以單一路徑處理 success=false 與 message。
      return extractApiError<LoginResponse>(error)
    } finally {
      isLoading.value = false
    }
  }

  async function register(data: RegisterRequest): Promise<AuthActionResult<UserDto>> {
    isLoading.value = true
    try {
      const response = await authApi.register(data)
      return { success: true, data: response.data, message: null, errors: null }
    } catch (error: unknown) {
      return extractApiError<UserDto>(error)
    } finally {
      isLoading.value = false
    }
  }

  async function logout(): Promise<void> {
    try {
      await authApi.logout()
    } finally {
      // 即使 API 請求失敗（例如網路中斷），仍清除本地認證狀態，
      // 確保使用者在客戶端被登出，不因 API 錯誤卡在已登入狀態。
      clearAuth()
    }
  }

  async function fetchCurrentUser(): Promise<void> {
    // 刻意吞下錯誤：本函式從 login / tryRefreshToken 成功路徑補抓使用者資料，
    // /users/me 暫時性失敗不應讓整個 login 變成失敗（token 已取得、cookie 已寫入）。
    // currentUser 保持 null；UI 可顯示「資料載入中」或略過個人化區塊。
    try {
      const response = await authApi.getMe()
      currentUser.value = response.data
    } catch (error: unknown) {
      currentUser.value = null
      if (import.meta.env.DEV) {
        console.warn('fetchCurrentUser failed:', error)
      }
    }
  }

  // 頁面刷新後 Pinia 狀態會清空，此方法透過 httpOnly cookie 中的 refresh token
  // 靜默重建認證狀態，讓使用者不需要重新登入。
  // 由 router guard 在每次導航前呼叫，僅在 isAuthenticated 為 false 時執行。
  async function tryRefreshToken(): Promise<boolean> {
    try {
      const response = await authApi.refresh()
      accessToken.value = response.data.accessToken
      await fetchCurrentUser()
      return true
    } catch {
      clearAuth()
      return false
    }
  }

  return {
    accessToken,
    currentUser,
    isLoading,
    isAuthenticated,
    setAccessToken,
    clearAuth,
    login,
    register,
    logout,
    fetchCurrentUser,
    tryRefreshToken,
  }
})
