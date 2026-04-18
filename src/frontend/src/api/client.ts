import axios, { type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import type { ApiResponse, LoginResponse } from '@/types'

// isRefreshing 防止多個 401 錯誤同時觸發多次 refresh 請求（並發競態問題）。
// 第一個 401 開始 refresh，後續的 401 請求進入 refreshQueue 等待新 token。
let isRefreshing = false
let refreshQueue: Array<{
  resolve: (token: string) => void
  reject: (error: unknown) => void
}> = []

// refresh 完成後（無論成功或失敗）批次處理佇列中的所有等待請求，
// 成功時以新 token 重試，失敗時將錯誤傳遞給所有等待的呼叫方。
function processRefreshQueue(token: string | null, error: unknown = null): void {
  refreshQueue.forEach(({ resolve, reject }) => {
    if (token) {
      resolve(token)
    } else {
      reject(error)
    }
  })
  refreshQueue = []
}

const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 15000,
  withCredentials: true, // 讓 httpOnly cookie (refresh token) 自動帶入
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request interceptor：自動附加 JWT access token
apiClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const authStore = useAuthStore()
  if (authStore.accessToken) {
    config.headers.Authorization = `Bearer ${authStore.accessToken}`
  }
  return config
})

// Response interceptor：401 時自動 refresh token
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    // _retry 旗標防止 refresh 請求本身若回傳 401 時觸發無限遞迴刷新。
    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error)
    }

    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        refreshQueue.push({ resolve, reject })
      }).then((token) => {
        originalRequest.headers.Authorization = `Bearer ${token}`
        return apiClient(originalRequest)
      })
    }

    originalRequest._retry = true
    isRefreshing = true

    try {
      // 直接使用 axios 而非 apiClient 發送 refresh 請求，
      // 避免 apiClient 的 response interceptor 再次攔截此請求的 401，
      // 造成無限遞迴。withCredentials 確保 httpOnly cookie 被帶入。
      const response = await axios.post<ApiResponse<LoginResponse>>(
        `${import.meta.env.VITE_API_BASE_URL}/v1/auth/refresh`,
        {},
        { withCredentials: true }
      )

      const newToken = response.data.data?.accessToken
      if (!newToken) {
        // refresh 回 200 但沒有 token — 後端契約異常。不要以空字串帶入 Authorization
        // 而讓請求繼續重試（會導致無限 401 迴圈），改為 throw 交給下方 catch 清 auth state。
        throw new Error('Refresh response did not contain an access token.')
      }
      const authStore = useAuthStore()
      authStore.setAccessToken(newToken)

      processRefreshQueue(newToken)
      originalRequest.headers.Authorization = `Bearer ${newToken}`
      return apiClient(originalRequest)
    } catch (refreshError) {
      processRefreshQueue(null, refreshError)
      const authStore = useAuthStore()
      authStore.clearAuth()
      // 使用 window.location.href 強制全頁跳轉而非 router.push，
      // 確保所有 Pinia store 狀態被重置，避免殘留的認證狀態。
      window.location.href = '/login'
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  }
)

export default apiClient
