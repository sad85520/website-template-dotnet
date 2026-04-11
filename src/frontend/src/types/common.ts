// ApiResponse 為後端統一信封格式（envelope pattern）：
// 所有 API 回應（成功或失敗）皆使用相同結構，
// 讓前端不需依賴 HTTP status code 決定如何解析回應體。
export interface ApiResponse<T = unknown> {
  success: boolean
  data: T | null
  message: string | null
  // errors 用於欄位層級的驗證錯誤（如表單驗證），
  // 與 message 的全域錯誤訊息分開存放。
  errors: FieldError[] | null
  // meta 僅在分頁列表 API 中出現，其他端點回傳 null。
  meta: PaginationMeta | null
}

export interface FieldError {
  field: string
  message: string
}

export interface PaginationMeta {
  total: number
  page: number
  limit: number
  totalPages: number
}

export interface PaginationQuery {
  page?: number
  limit?: number
  search?: string
}

