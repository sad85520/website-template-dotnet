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
    document.documentElement.classList.toggle('dark', newTheme === 'dark')
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
