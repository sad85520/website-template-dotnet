import { describe, it, expect, vi, beforeEach } from 'vitest'
import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth.store'
import { authApi } from '@/api'
import type { ApiResponse, LoginResponse, UserDto } from '@/types'

// 共用的 AxiosResponse 最小 shape；用 `satisfies` 讓 TS 在欄位不足時
// 直接擋下（原本全部 `as any`，改動 ApiResponse / LoginResponse 型別時 mock 不會被抓）。
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

function makeEnvelope<T>(data: T, overrides: Partial<ApiResponse<T>> = {}): ApiResponse<T> {
  return {
    success: true,
    data,
    message: null,
    errors: null,
    meta: null,
    ...overrides,
  }
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

  it('登入成功後更新 token 與使用者，且回傳 envelope success=true', async () => {
    const mockUser: UserDto = {
      id: '1',
      email: 'test@example.com',
      displayName: 'Test',
      role: 'user',
      createdAt: '2024-01-01',
    }
    const loginPayload: LoginResponse = { accessToken: 'token123', expiresIn: 900 }

    vi.mocked(authApi.login).mockResolvedValue(makeAxiosResponse(makeEnvelope(loginPayload)))
    vi.mocked(authApi.getMe).mockResolvedValue(makeAxiosResponse(makeEnvelope(mockUser)))

    const store = useAuthStore()
    const result = await store.login({ email: 'test@example.com', password: 'password' })

    // 斷言 return shape 而非只看 store 狀態；下游若改 LoginResponse 欄位或 envelope 會
    // 被型別系統與 assertion 雙重攔截。
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
    vi.mocked(authApi.logout).mockResolvedValue(makeAxiosResponse(makeEnvelope(null)))

    const store = useAuthStore()
    store.setAccessToken('sometoken')
    await store.logout()

    expect(store.isAuthenticated).toBe(false)
  })

  it('註冊成功回傳 envelope', async () => {
    const mockUser: UserDto = {
      id: '2',
      email: 'new@example.com',
      displayName: 'New',
      role: 'user',
      createdAt: '2024-01-01',
    }
    vi.mocked(authApi.register).mockResolvedValue(makeAxiosResponse(makeEnvelope(mockUser)))

    const store = useAuthStore()
    const result = await store.register({
      email: 'new@example.com',
      password: 'Password123!',
      displayName: 'New',
    })

    expect(result.success).toBe(true)
    expect(result.data).toEqual(mockUser)
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

    vi.mocked(authApi.refresh).mockResolvedValue(makeAxiosResponse(makeEnvelope(refreshPayload)))
    vi.mocked(authApi.getMe).mockResolvedValue(makeAxiosResponse(makeEnvelope(mockUser)))

    const store = useAuthStore()
    const ok = await store.tryRefreshToken()

    expect(ok).toBe(true)
    expect(store.accessToken).toBe('new-token')
    expect(store.currentUser).toEqual(mockUser)
  })

  it('tryRefreshToken envelope 成功但無 data 時回 false', async () => {
    vi.mocked(authApi.refresh).mockResolvedValue(
      makeAxiosResponse(
        makeEnvelope<LoginResponse | null>(null, { success: false, message: 'expired' }),
      ),
    )

    const store = useAuthStore()
    const ok = await store.tryRefreshToken()

    expect(ok).toBe(false)
  })

  it('tryRefreshToken 拋錯時清除狀態並回 false', async () => {
    vi.mocked(authApi.refresh).mockRejectedValue(new Error('network'))

    const store = useAuthStore()
    store.setAccessToken('stale')
    const ok = await store.tryRefreshToken()

    expect(ok).toBe(false)
    expect(store.isAuthenticated).toBe(false)
  })

  it('login envelope 失敗時不更新 token', async () => {
    vi.mocked(authApi.login).mockResolvedValue(
      makeAxiosResponse(
        makeEnvelope<LoginResponse | null>(null, { success: false, message: '帳密錯誤' }),
      ),
    )

    const store = useAuthStore()
    const result = await store.login({ email: 'bad@example.com', password: 'bad' })

    expect(result.success).toBe(false)
    expect(store.accessToken).toBeNull()
  })

  it('login AxiosError 時回傳 backend message 與 errors 且 store 不變', async () => {
    // 對應 #35 audit：原先無此測試，後端回 401 / 驗證失敗（AxiosError）的
    // 分支只在 store 內以 extractApiError 處理，但沒測試涵蓋，容易悄悄退化。
    vi.mocked(authApi.login).mockRejectedValue(
      makeAxiosError<ApiResponse<null>>(
        {
          success: false,
          data: null,
          message: 'Invalid credentials',
          errors: [{ field: 'email', message: 'bad email' }],
          meta: null,
        },
        401,
      ),
    )

    const store = useAuthStore()
    const result = await store.login({ email: 'x@example.com', password: 'bad' })

    expect(result.success).toBe(false)
    expect(result.message).toBe('Invalid credentials')
    expect(result.errors).toEqual([{ field: 'email', message: 'bad email' }])
    expect(store.isAuthenticated).toBe(false)
    expect(store.accessToken).toBeNull()
  })

  it('login 非 Axios 錯誤時回傳預設網路錯誤訊息', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new Error('boom'))

    const store = useAuthStore()
    const result = await store.login({ email: 'x@example.com', password: 'bad' })

    expect(result.success).toBe(false)
    expect(result.message).toBe('網路錯誤，請稍後再試')
  })
})
