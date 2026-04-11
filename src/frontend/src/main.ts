import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './assets/styles/main.css'

const app = createApp(App)

// Pinia 必須在 router 之前安裝，因為 router guard（beforeEach）
// 中使用了 useAuthStore()，store 必須先初始化才能被存取。
app.use(createPinia())
app.use(router)
app.mount('#app')
