import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import pinia from './store'
import { initTheme } from '@/theme'

import '@/styles/reset.css'
import 'virtual:uno.css'

// 在挂载前恢复用户主题偏好
initTheme()

const app = createApp(App)

app.use(pinia)
app.use(router)
app.mount('#app')
