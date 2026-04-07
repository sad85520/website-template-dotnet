import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/api'
import type { UserDto, LoginRequest, RegisterRequest } from '@/types'

export const useAuthStore = defineStore('auth', () => {
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
      clearAuth()
    }
  }

  async function fetchCurrentUser() {
    const response = await authApi.getMe()
    if (response.data.success && response.data.data) {
      currentUser.value = response.data.data
    }
  }

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
