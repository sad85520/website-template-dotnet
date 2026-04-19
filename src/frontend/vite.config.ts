import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    // host '0.0.0.0' 讓 docker-compose 內其他 container（以及 host 機透過 port mapping）
    // 連進來；搭配 allowedHosts 限制 Host header 白名單，避免 Vite 5+ 的 DNS rebinding
    // 攻擊（惡意網站以 a.attacker.com 指向 127.0.0.1 繞過 SOP 存取 dev server）。
    host: '0.0.0.0',
    allowedHosts: ['localhost', '127.0.0.1', 'frontend'],
    proxy: {
      '/api': {
        target: 'http://backend:8080',
        changeOrigin: true,
      },
    },
  },
})
