import apiClient from './client'
import type { LoginRequest, LoginResponse, RegisterRequest, UserDto } from '@/types'

// 成功回應直接是資源本體，不再有信封（見 docs/adr/ADR-004-rfc7807-problem-details.md）；
// 錯誤時 Axios 會以 rejected Promise 拋出 AxiosError<ProblemDetails>，由呼叫端（auth.store.ts）
// 的 extractApiError 統一轉譯。
export const authApi = {
  login(data: LoginRequest) {
    return apiClient.post<LoginResponse>('/v1/auth/login', data)
  },

  register(data: RegisterRequest) {
    return apiClient.post<UserDto>('/v1/auth/register', data)
  },

  refresh() {
    return apiClient.post<LoginResponse>('/v1/auth/refresh')
  },

  logout() {
    // 204 No Content：登出成功不回傳任何主體。
    return apiClient.post<void>('/v1/auth/logout')
  },

  getMe() {
    return apiClient.get<UserDto>('/v1/users/me')
  },
}
