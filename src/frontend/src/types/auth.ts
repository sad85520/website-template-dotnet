export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  displayName: string
}

export interface LoginResponse {
  accessToken: string
  expiresIn: number
}

export interface RefreshTokenRequest {
  // refresh token 透過 httpOnly cookie 自動帶入，不需要 body
}

export interface UserDto {
  id: string
  email: string
  displayName: string
  role: 'admin' | 'user'
  createdAt: string
}
