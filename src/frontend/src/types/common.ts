// 後端統一以 ASP.NET Core 原生的 RFC 7807 Problem Details 回傳錯誤（application/problem+json）；
// 成功回應不再有信封，直接是資源本體（見 docs/adr/ADR-004-rfc7807-problem-details.md）。
// `errors` 只有 model validation 失敗（框架原生 ValidationProblemDetails）時才會出現。
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
  traceId?: string
}

export interface PaginationQuery {
  page?: number
  limit?: number
  search?: string
}

// 分頁列表端點（例如 GET /v1/users）不再有信封可夾帶 meta，改由回應標頭傳遞：
// X-Pagination-Total / X-Pagination-Page / X-Pagination-Limit / X-Pagination-Total-Pages。
// 消費端可用 parsePaginationHeaders()（見 @/api/client）從 AxiosResponse.headers 組出這個型別。
export interface PaginationMeta {
  total: number
  page: number
  limit: number
  totalPages: number
}
