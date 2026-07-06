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

// 前端內部使用的動作結果型別：後端已不再回傳信封（見 ADR-004），
// 這裡是 auth store 呼叫 API 後，把「成功資源／AxiosError 解析出的 ProblemDetails」
// 統一收斂成一個型別，讓 useAuth / 表單元件不需要各自寫 try/catch 分支。
// errors 的 key 為欄位名稱、value 為訊息陣列，直接對應後端 ValidationProblemDetails.errors 的格式。
export interface AuthActionResult<T> {
  success: boolean
  data: T | null
  message: string | null
  errors: Record<string, string[]> | null
}
