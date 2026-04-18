import pluginVue from 'eslint-plugin-vue'
import tsParser from '@typescript-eslint/parser'
import tsPlugin from '@typescript-eslint/eslint-plugin'
import vueParser from 'vue-eslint-parser'

export default [
  {
    ignores: ['dist/', 'coverage/', 'node_modules/'],
  },
  ...pluginVue.configs['flat/recommended'],
  {
    files: ['**/*.{ts,tsx,vue}'],
    languageOptions: {
      parser: vueParser,
      parserOptions: {
        parser: tsParser,
        extraFileExtensions: ['.vue'],
        sourceType: 'module',
        // projectService 讓 ts-eslint 以 TypeScript 自己的 project graph 解析型別，
        // 能自動處理 tsconfig.app.json + tsconfig.node.json 的分層引用，
        // 是 type-checked rules（no-floating-promises / no-misused-promises 等）
        // 能發現錯誤的前提。
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      '@typescript-eslint': tsPlugin,
    },
    rules: {
      '@typescript-eslint/no-unused-vars': 'error',
      // any 完全禁用：API envelope 已有明確 TypeScript 型別，真正需要時以 unknown + 窄化處理。
      '@typescript-eslint/no-explicit-any': 'error',
      // 未 await 的 Promise 容易導致「看起來成功但實際未完成」的 bug。
      '@typescript-eslint/no-floating-promises': 'error',
      // 把 async 函式當作 event handler 時容易遺失錯誤；此規則擋住常見誤用。
      '@typescript-eslint/no-misused-promises': 'error',
      '@typescript-eslint/await-thenable': 'error',
      'vue/multi-word-component-names': 'off',
      'vue/component-definition-name-casing': ['error', 'PascalCase'],
    },
  },
  {
    // 測試檔可放寬 any 與 unsafe-* 規則 — mock/spy 天然需要 any。
    files: ['tests/**/*', '**/*.spec.ts', '**/*.test.ts'],
    rules: {
      '@typescript-eslint/no-explicit-any': 'off',
      '@typescript-eslint/no-floating-promises': 'off',
    },
  },
]
