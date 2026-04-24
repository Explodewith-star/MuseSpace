import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { login, adminLogin, getMe } from '@/api/auth'
import type { LoginRequest, AdminLoginRequest, UserResponse } from '@/api/auth'

const TOKEN_KEY = 'musespace_token'
const USER_KEY = 'musespace_user'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const user = ref<UserResponse | null>(JSON.parse(localStorage.getItem(USER_KEY) ?? 'null'))

  const isLoggedIn = computed(() => !!token.value)
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isGuest = computed(() => !token.value)

  function setSession(t: string, u: UserResponse) {
    token.value = t
    user.value = u
    localStorage.setItem(TOKEN_KEY, t)
    localStorage.setItem(USER_KEY, JSON.stringify(u))
  }

  function clearSession() {
    token.value = null
    user.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  }

  async function loginAsUser(data: LoginRequest) {
    const res = await login(data)
    setSession(res.token, {
      id: res.userId,
      phoneNumber: res.phoneNumber,
      role: res.role,
      createdAt: new Date().toISOString(),
    })
  }

  async function loginAsAdmin(data: AdminLoginRequest) {
    const res = await adminLogin(data)
    setSession(res.token, {
      id: res.userId,
      phoneNumber: res.phoneNumber,
      role: res.role,
      createdAt: new Date().toISOString(),
    })
  }

  function logout() {
    clearSession()
  }

  /** 校验本地 token 是否仍有效（通过 exp 字段判断，不发请求） */
  function isTokenValid(): boolean {
    if (!token.value) return false
    try {
      const payload = JSON.parse(atob(token.value.split('.')[1]))
      return payload.exp * 1000 > Date.now()
    } catch {
      return false
    }
  }

  return {
    token,
    user,
    isLoggedIn,
    isAdmin,
    isGuest,
    loginAsUser,
    loginAsAdmin,
    logout,
    isTokenValid,
    setSession,
    clearSession,
  }
})
