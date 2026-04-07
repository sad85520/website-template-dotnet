import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores'
import { useNotificationStore } from '@/stores'
import type { LoginRequest, RegisterRequest } from '@/types'

export function useAuth() {
  const router = useRouter()
  const route = useRoute()
  const authStore = useAuthStore()
  const notificationStore = useNotificationStore()

  async function login(credentials: LoginRequest) {
    const result = await authStore.login(credentials)

    if (result.success) {
      notificationStore.success('登入成功')
      const redirect = (route.query.redirect as string) || '/'
      await router.push(redirect)
    } else {
      notificationStore.error(result.message ?? '登入失敗，請確認帳號密碼')
    }

    return result
  }

  async function register(data: RegisterRequest) {
    const result = await authStore.register(data)

    if (result.success) {
      notificationStore.success('註冊成功，請登入')
      await router.push({ name: 'login' })
    } else {
      notificationStore.error(result.message ?? '註冊失敗，請稍後再試')
    }

    return result
  }

  async function logout() {
    await authStore.logout()
    notificationStore.info('已登出')
    await router.push({ name: 'login' })
  }

  return {
    isAuthenticated: authStore.isAuthenticated,
    currentUser: authStore.currentUser,
    isLoading: authStore.isLoading,
    login,
    register,
    logout,
  }
}
