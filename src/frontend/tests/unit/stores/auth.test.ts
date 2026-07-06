import { describe, it, expect, vi, beforeEach } from 'vitest'
import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth.store'
import { authApi } from '@/api'
import type { LoginResponse, ProblemDetails, UserDto } from '@/types'

// 共用的 AxiosResponse 最小 shape；成功回應直接是資源本體（不再有信封，見 ADR-004），
// 用 `satisfies` 讓 TS 在欄位不足時直接擋下。
function makeAxiosResponse<T>(data: T, status = 200): AxiosResponse<T> {
  const headers = new AxiosHeaders()
  return {
    data,
    status,
    statusText: '',
    headers,
    config: { headers } as AxiosResponse<T>['config'],
  } satisfies AxiosResponse<T>
}

function makeAxiosError<T>(data: T, status = 400): AxiosError<T> {
  const headers = new AxiosHeaders()
  const response: AxiosResponse<T> = {
    data,
    status,
    statusText: '',
    headers,
    config: { headers } as AxiosResponse<T>['config'],
  }
  return new AxiosError<T>('request failed', String(status), undefined, undefined, response)
}

vi.mock('@/api', () => ({
  authApi: {
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    refresh: vi.fn(),
    getMe: vi.fn(),
  },
}))

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('初始狀態：未認證', () => {
    const store = useAuthStore()
    expect(store.isAuthenticated).toBe(false)
    expect(store.accessToken).toBeNull()
    expect(store.currentUser).toBeNull()
  })

  it('登入成功後更新 token 與使用者，且回傳 success=true 的資源本體', async () => {
    const mockUser: UserDto = {
      id: '1',
      email: 'test@example.com',
      displayName: 'Test',
      role: 'user',
      createdAt: '2024-01-01',
    }
    const loginPayload: LoginResponse = { accessToken: 'token123', expiresIn: 900 }

    vi.mocked(authApi.login).mockResolvedValue(makeAxiosResponse(loginPayload))
    vi.mocked(authApi.getMe).mockResolvedValue(makeAxiosResponse(mockUser))

    const store = useAuthStore()
    const result = await store.login({ email: 'test@example.com', password: 'password' })

    // 斷言 return shape 而非只看 store 狀態；下游若改 LoginResponse 欄位會被
    // 型別系統與 assertion 雙重攔截。
    expect(result.success).toBe(true)
    expect(result.data).toEqual(loginPayload)
    expect(store.isAuthenticated).toBe(true)
    expect(store.accessToken).toBe('token123')
    expect(store.currentUser).toEqual(mockUser)
  })

  it('clearAuth 清除認證狀態', () => {
    const store = useAuthStore()
    store.setAccessToken('sometoken')
    store.clearAuth()

    expect(store.isAuthenticated).toBe(false)
    expect(store.accessToken).toBeNull()
  })

  it('登出後清除狀態', async () => {
    // 登出成功回傳 204 No Content，主體為 undefined。
    vi.mocked(authApi.logout).mockResolvedValue(makeAxiosResponse(undefined, 204))

    const store = useAuthStore()
    store.setAccessToken('sometoken')
    await store.logout()

    expect(store.isAuthenticated).toBe(false)
  })

  it('註冊成功回傳資源本體', async () => {
    const mockUser: UserDto = {
      id: '2',
      email: 'new@example.com',
      displayName: 'New',
      role: 'user',
      createdAt: '2024-01-01',
    }
    vi.mocked(authApi.register).mockResolvedValue(makeAxiosResponse(mockUser, 201))

    const store = useAuthStore()
    const result = await store.register({
      email: 'new@example.com',
      password: 'Password123!',
      displayName: 'New',
    })

    expect(result.success).toBe(true)
    expect(result.data).toEqual(mockUser)
  })

  it('登入成功但 fetchCurrentUser 失敗時，token 仍保留、currentUser 維持 null', async () => {
    // fetchCurrentUser 刻意吞下 /users/me 的錯誤（見 auth.store.ts 註解）：
    // token 已經拿到、cookie 已寫入，個人資料暫時抓不到不該讓整個 login 變成失敗。
    const loginPayload: LoginResponse = { accessToken: 'token456', expiresIn: 900 }

    vi.mocked(authApi.login).mockResolvedValue(makeAxiosResponse(loginPayload))
    vi.mocked(authApi.getMe).mockRejectedValue(new Error('network'))

    const store = useAuthStore()
    const result = await store.login({ email: 'test@example.com', password: 'password' })

    expect(result.success).toBe(true)
    expect(store.isAuthenticated).toBe(true)
    expect(store.currentUser).toBeNull()
  })

  it('註冊遇到 ProblemDetails 錯誤時回傳 detail 訊息', async () => {
    vi.mocked(authApi.register).mockRejectedValue(
      makeAxiosError<ProblemDetails>(
        { status: 409, title: 'Conflict', detail: 'Email is already registered.' },
        409,
      ),
    )

    const store = useAuthStore()
    const result = await store.register({
      email: 'dup@example.com',
      password: 'Password123!',
      displayName: 'Dup',
    })

    expect(result.success).toBe(false)
    expect(result.message).toBe('Email is already registered.')
  })

  it('tryRefreshToken 成功時恢復狀態', async () => {
    const mockUser: UserDto = {
      id: '1',
      email: 'test@example.com',
      displayName: 'Test',
      role: 'user',
      createdAt: '2024-01-01',
    }
    const refreshPayload: LoginResponse = { accessToken: 'new-token', expiresIn: 900 }

    vi.mocked(authApi.refresh).mockResolvedValue(makeAxiosResponse(refreshPayload))
    vi.mocked(authApi.getMe).mockResolvedValue(makeAxiosResponse(mockUser))

    const store = useAuthStore()
    const ok = await store.tryRefreshToken()

    expect(ok).toBe(true)
    expect(store.accessToken).toBe('new-token')
    expect(store.currentUser).toEqual(mockUser)
  })

  it('tryRefreshToken 拋錯（refresh token 過期/無效）時清除狀態並回 false', async () => {
    vi.mocked(authApi.refresh).mockRejectedValue(
      makeAxiosError<ProblemDetails>(
        { status: 401, title: 'Unauthorized', detail: 'Refresh token has expired.' },
        401,
      ),
    )

    const store = useAuthStore()
    store.setAccessToken('stale')
    const ok = await store.tryRefreshToken()

    expect(ok).toBe(false)
    expect(store.isAuthenticated).toBe(false)
  })

  it('tryRefreshToken 遇到非 Axios 錯誤時也清除狀態並回 false', async () => {
    vi.mocked(authApi.refresh).mockRejectedValue(new Error('network'))

    const store = useAuthStore()
    store.setAccessToken('stale')
    const ok = await store.tryRefreshToken()

    expect(ok).toBe(false)
    expect(store.isAuthenticated).toBe(false)
  })

  it('login 遇到 ProblemDetails 錯誤時回傳 backend detail 與 errors 且 store 不變', async () => {
    // 對應 #35 audit：原先無此測試，後端回 401 / 驗證失敗（AxiosError）的
    // 分支只在 store 內以 extractApiError 處理，但沒測試涵蓋，容易悄悄退化。
    vi.mocked(authApi.login).mockRejectedValue(
      makeAxiosError<ProblemDetails>(
        {
          status: 401,
          title: 'Unauthorized',
          detail: 'Invalid credentials',
        },
        401,
      ),
    )

    const store = useAuthStore()
    const result = await store.login({ email: 'x@example.com', password: 'bad' })

    expect(result.success).toBe(false)
    expect(result.message).toBe('Invalid credentials')
    expect(store.isAuthenticated).toBe(false)
    expect(store.accessToken).toBeNull()
  })

  it('login 遇到 ValidationProblemDetails 時回傳欄位層級 errors', async () => {
    vi.mocked(authApi.login).mockRejectedValue(
      makeAxiosError<ProblemDetails>(
        {
          status: 400,
          title: 'One or more validation errors occurred.',
          errors: { Password: ['The Password field is required.'] },
        },
        400,
      ),
    )

    const store = useAuthStore()
    const result = await store.login({ email: 'x@example.com', password: '' })

    expect(result.success).toBe(false)
    expect(result.errors).toEqual({ Password: ['The Password field is required.'] })
  })

  it('login 非 Axios 錯誤時回傳預設網路錯誤訊息', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new Error('boom'))

    const store = useAuthStore()
    const result = await store.login({ email: 'x@example.com', password: 'bad' })

    expect(result.success).toBe(false)
    expect(result.message).toBe('網路錯誤，請稍後再試')
  })
})
