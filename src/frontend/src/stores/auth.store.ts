import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/api'
import type { UserDto, LoginRequest, RegisterRequest } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  // accessToken 僅存於記憶體（Pinia reactive state），不存入 localStorage，
  // 防止 XSS 攻擊直接竊取 token；頁面重新整理後透過 tryRefreshToken 以 httpOnly cookie 還原。
  const accessToken = ref<string | null>(null)
  const currentUser = ref<UserDto | null>(null)
  const isLoading = ref(false)

  const isAuthenticated = computed(() => !!accessToken.value)

  function setAccessToken(token: string) {
    accessToken.value = token
  }

  function clearAuth() {
    accessToken.value = null
    currentUser.value = null
  }

  async function login(credentials: LoginRequest) {
    isLoading.value = true
    try {
      const response = await authApi.login(credentials)
      if (response.data.success && response.data.data) {
        accessToken.value = response.data.data.accessToken
        await fetchCurrentUser()
      }
      return response.data
    } finally {
      isLoading.value = false
    }
  }

  async function register(data: RegisterRequest) {
    isLoading.value = true
    try {
      const response = await authApi.register(data)
      return response.data
    } finally {
      isLoading.value = false
    }
  }

  async function logout() {
    try {
      await authApi.logout()
    } finally {
      // 即使 API 請求失敗（例如網路中斷），仍清除本地認證狀態，
      // 確保使用者在客戶端被登出，不因 API 錯誤卡在已登入狀態。
      clearAuth()
    }
  }

  async function fetchCurrentUser() {
    const response = await authApi.getMe()
    if (response.data.success && response.data.data) {
      currentUser.value = response.data.data
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
