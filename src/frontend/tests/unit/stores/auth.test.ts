import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth.store'
import { authApi } from '@/api'

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

  it('登入成功後更新 token 與使用者', async () => {
    const mockUser = { id: '1', email: 'test@example.com', displayName: 'Test', role: 'user' as const, createdAt: '2024-01-01' }

    vi.mocked(authApi.login).mockResolvedValue({
      data: { success: true, data: { accessToken: 'token123', expiresIn: 900 }, message: null, errors: null, meta: null },
    } as any)

    vi.mocked(authApi.getMe).mockResolvedValue({
      data: { success: true, data: mockUser, message: null, errors: null, meta: null },
    } as any)

    const store = useAuthStore()
    await store.login({ email: 'test@example.com', password: 'password' })

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
    vi.mocked(authApi.logout).mockResolvedValue({ data: { success: true, data: null, message: null, errors: null, meta: null } } as any)

    const store = useAuthStore()
    store.setAccessToken('sometoken')
    await store.logout()

    expect(store.isAuthenticated).toBe(false)
  })

  it('註冊回傳 envelope', async () => {
    const mockUser = { id: '2', email: 'new@example.com', displayName: 'New', role: 'user' as const, createdAt: '2024-01-01' }
    vi.mocked(authApi.register).mockResolvedValue({
      data: { success: true, data: mockUser, message: null, errors: null, meta: null },
    } as any)

    const store = useAuthStore()
    const result = await store.register({ email: 'new@example.com', password: 'Password123!', displayName: 'New' } as any)

    expect(result.success).toBe(true)
    expect(result.data).toEqual(mockUser)
  })

  it('tryRefreshToken 成功時恢復狀態', async () => {
    const mockUser = { id: '1', email: 'test@example.com', displayName: 'Test', role: 'user' as const, createdAt: '2024-01-01' }

    vi.mocked(authApi.refresh).mockResolvedValue({
      data: { success: true, data: { accessToken: 'new-token', expiresIn: 900 }, message: null, errors: null, meta: null },
    } as any)
    vi.mocked(authApi.getMe).mockResolvedValue({
      data: { success: true, data: mockUser, message: null, errors: null, meta: null },
    } as any)

    const store = useAuthStore()
    const ok = await store.tryRefreshToken()

    expect(ok).toBe(true)
    expect(store.accessToken).toBe('new-token')
    expect(store.currentUser).toEqual(mockUser)
  })

  it('tryRefreshToken envelope 成功但無 data 時回 false', async () => {
    vi.mocked(authApi.refresh).mockResolvedValue({
      data: { success: false, data: null, message: 'expired', errors: null, meta: null },
    } as any)

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
    vi.mocked(authApi.login).mockResolvedValue({
      data: { success: false, data: null, message: '帳密錯誤', errors: null, meta: null },
    } as any)

    const store = useAuthStore()
    const result = await store.login({ email: 'bad@example.com', password: 'bad' })

    expect(result.success).toBe(false)
    expect(store.accessToken).toBeNull()
  })
})
