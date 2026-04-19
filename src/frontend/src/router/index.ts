import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores'

// Augment vue-router RouteMeta 以讓 `to.meta.requiresAuth` 等欄位擁有正確型別，
// 避免下游開發以 `(to.meta as any).requiresAuth` 繞過檢查，並在 IDE 獲得 autocomplete。
declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean
    guestOnly?: boolean
  }
}

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    component: () => import('@/components/layout/AppLayout.vue'),
    children: [
      {
        path: '',
        name: 'home',
        component: () => import('@/views/HomeView.vue'),
      },
      {
        path: 'dashboard',
        name: 'dashboard',
        component: () => import('@/views/DashboardView.vue'),
        meta: { requiresAuth: true },
      },
    ],
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { guestOnly: true },
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('@/views/RegisterView.vue'),
    meta: { guestOnly: true },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
  },
]

// createWebHistory 使用 HTML5 History API，路由路徑不帶 # 號，
// 需要後端（或反向代理）設定 fallback 將所有未匹配路徑回傳 index.html。
const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach(async (to, _from, next) => {
  const authStore = useAuthStore()

  // 頁面刷新時嘗試透過 refresh token 恢復認證狀態
  if (!authStore.isAuthenticated) {
    await authStore.tryRefreshToken()
  }

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    return next({ name: 'login', query: { redirect: to.fullPath } })
  }

  // guestOnly 路由（如登入、註冊頁）在已登入時自動重導向首頁，
  // 防止已登入使用者重複登入造成 token 狀態混亂。
  if (to.meta.guestOnly && authStore.isAuthenticated) {
    return next({ name: 'home' })
  }

  next()
})

export default router
