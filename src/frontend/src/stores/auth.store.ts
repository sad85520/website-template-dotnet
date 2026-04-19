import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { isAxiosError } from 'axios'
import { authApi } from '@/api'
import type { ApiResponse, LoginRequest, LoginResponse, RegisterRequest, UserDto } from '@/types'

// 抽取後端統一 envelope 的錯誤欄位，若 response 不是 ApiResponse（網路錯誤 / CORS / 5xx HTML）
// 則組一個「看起來一致」的 fail 回傳，caller 就不需要額外 try/catch 去分支。
function extractApiError<T>(error: unknown): ApiResponse<T> {
  if (isAxiosError<ApiResponse<T>>(error) && error.response?.data) {
    const payload = error.response.data
    return {
      success: false,
      data: null,
      message: payload.message ?? '請求失敗，請稍後再試',
      errors: payload.errors ?? null,
      meta: null,
    }
  }
  return {
    success: false,
    data: null,
    message: '網路錯誤，請稍後再試',
    errors: null,
    meta: null,
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

  async function login(credentials: LoginRequest): Promise<ApiResponse<LoginResponse>> {
    isLoading.value = true
    try {
      const response = await authApi.login(credentials)
      if (response.data.success && response.data.data) {
        accessToken.value = response.data.data.accessToken
        await fetchCurrentUser()
      }
      return response.data
    } catch (error: unknown) {
      // 沒有 catch 的話 AxiosError 會成為 unhandled rejection，UI 僅能靠全域
      // interceptor 收拾且 caller 拿不到錯誤訊息。轉為 ApiResponse shape，
      // 讓 composable/useAuth 以單一路徑處理 success=false 與 message。
      return extractApiError<LoginResponse>(error)
    } finally {
      isLoading.value = false
    }
  }

  async function register(data: RegisterRequest): Promise<ApiResponse<UserDto>> {
    isLoading.value = true
    try {
      const response = await authApi.register(data)
      return response.data
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
      if (response.data.success && response.data.data) {
        currentUser.value = response.data.data
      }
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
      if (response.data.success && response.data.data) {
        accessToken.value = response.data.data.accessToken
        await fetchCurrentUser()
        return true
      }
      return false
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
