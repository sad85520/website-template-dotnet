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
  // expiresIn 單位為秒（對應後端 OAuth 2.0 慣例），
  // auth store 或呼叫端可用此值設定自動刷新計時器。
  expiresIn: number
}

export interface UserDto {
  id: string
  email: string
  displayName: string
  // role 由後端以小寫字串回傳，使用字串聯集而非 enum，
  // 方便直接與 API 回應做型別安全的字串比對。
  role: 'admin' | 'user'
  createdAt: string
}
