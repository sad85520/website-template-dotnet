export interface ApiResponse<T = unknown> {
  success: boolean
  data: T | null
  message: string | null
  errors: FieldError[] | null
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
