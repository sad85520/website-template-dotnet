import apiClient from './client'
import type { ApiResponse, LoginRequest, LoginResponse, RegisterRequest, UserDto } from '@/types'

export const authApi = {
  login(data: LoginRequest) {
    return apiClient.post<ApiResponse<LoginResponse>>('/v1/auth/login', data)
  },

  register(data: RegisterRequest) {
    return apiClient.post<ApiResponse<UserDto>>('/v1/auth/register', data)
  },

  refresh() {
    return apiClient.post<ApiResponse<LoginResponse>>('/v1/auth/refresh')
  },

  logout() {
    return apiClient.post<ApiResponse<null>>('/v1/auth/logout')
  },

  getMe() {
    return apiClient.get<ApiResponse<UserDto>>('/v1/users/me')
  },
}
