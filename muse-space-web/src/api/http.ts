import axios from 'axios'
import type { AxiosInstance, AxiosRequestConfig, InternalAxiosRequestConfig } from 'axios'
import type { ApiResponse } from '@/types/api'

type ExtendedConfig = AxiosRequestConfig & { silent?: boolean }

const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_APP_BASE_API || '/api',
  timeout: 120000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// 清除本地登录会话并跳转登录页
function clearSessionAndRedirect() {
  localStorage.removeItem('musespace_token')
  localStorage.removeItem('musespace_user')
  if (window.location.pathname !== '/login') {
    window.location.href = '/login'
  }
}

// 判断 JWT token 是否仍在有效期内（纯本地解码，不发请求）
function isTokenAlive(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now()
  } catch {
    return false
  }
}

// 请求拦截器
http.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem('musespace_token')
    if (token) {
      if (isTokenAlive(token)) {
        config.headers['Authorization'] = `Bearer ${token}`
      } else {
        // token 已过期：清除本地会话并跳转登录，避免后端静默降级为游客
        clearSessionAndRedirect()
      }
    }
    return config
  },
  (error) => Promise.reject(error),
)

// 响应拦截器：自动解包 ApiResponse<T>，失败时 toast 并 reject
http.interceptors.response.use(
  (response) => {
    const body = response.data as ApiResponse<unknown>
    if (!body.success) {
      // 延迟导入避免循环依赖，toast 通知由 useToast 提供
      import('@/composables/useToast').then(({ useToast }) => {
        useToast().error(body.errorMessage ?? '请求失败')
      })
      return Promise.reject(new Error(body.errorMessage ?? '请求失败'))
    }
    return body.data as never
  },
  (error) => {
    const status = error.response?.status
    const silent = (error.config as ExtendedConfig)?.silent
    if (status === 401) {
      clearSessionAndRedirect()
      return Promise.reject(error)
    }
    if (!silent) {
      const backendMsg: string | undefined = error.response?.data?.errorMessage
      const msgMap: Record<number, string> = {
        400: '请求参数错误',
        401: '无权限或登录已过期，请重新登录',
        403: '权限不足',
        404: '资源不存在',
        409: '该文件已导入过，请勿重复上传',
        500: '服务器错误，请稍后重试',
      }
      const msg = backendMsg ?? msgMap[status] ?? '网络异常，请检查连接'
      import('@/composables/useToast').then(({ useToast }) => {
        useToast().error(msg)
      })
    }
    return Promise.reject(error)
  },
)

const request = {
  get<T = unknown>(url: string, config?: ExtendedConfig): Promise<T> {
    return http.get(url, config)
  },
  post<T = unknown>(url: string, data?: unknown, config?: ExtendedConfig): Promise<T> {
    return http.post(url, data, config)
  },
  put<T = unknown>(url: string, data?: unknown, config?: ExtendedConfig): Promise<T> {
    return http.put(url, data, config)
  },
  delete<T = unknown>(url: string, config?: ExtendedConfig): Promise<T> {
    return http.delete(url, config)
  },
}

export default request
