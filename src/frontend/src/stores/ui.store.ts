import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useUiStore = defineStore('ui', () => {
  const sidebarOpen = ref(false)
  const globalLoading = ref(false)
  const theme = ref<'light' | 'dark'>('light')

  function toggleSidebar() {
    sidebarOpen.value = !sidebarOpen.value
  }

  function setTheme(newTheme: 'light' | 'dark') {
    theme.value = newTheme
    // Tailwind CSS 的 dark mode 透過 <html> 元素上的 'dark' class 控制，
    // 必須直接操作 DOM，因為 Tailwind 在編譯期決定 class，非執行期。
    // SSR guard：若未來改用 SSR（Nuxt / vite-ssg）首次 render 於 Node 端，直接存
    // document 會 ReferenceError。typeof 守門後 client hydrate 時會再觸發一次。
    if (typeof document !== 'undefined') {
      document.documentElement.classList.toggle('dark', newTheme === 'dark')
    }
  }

  function setLoading(loading: boolean) {
    globalLoading.value = loading
  }

  return {
    sidebarOpen,
    globalLoading,
    theme,
    toggleSidebar,
    setTheme,
    setLoading,
  }
})
