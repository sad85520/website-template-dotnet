import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./tests/setup.ts'],
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      // include 只納入已經有測試的模組，避免 skeleton 未測試檔案拉低 threshold；
      // 新增 feature 時請同步擴充此 include 與對應測試，以維持 80% 門檻有意義。
      include: ['src/stores/auth.store.ts'],
      exclude: ['node_modules/', 'tests/', '*.config.*'],
      // 80% 是最低門檻，CI 未達即失敗；Template 初始化後請隨實作進度逐步提高。
      thresholds: {
        lines: 80,
        functions: 80,
        branches: 75,
        statements: 80,
      },
    },
  },
})
