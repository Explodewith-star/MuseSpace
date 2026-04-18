import axios from 'axios'
import type { AxiosInstance, AxiosRequestConfig, InternalAxiosRequestConfig } from 'axios'
import type { ApiResponse } from '@/types/api'

type ExtendedConfig = AxiosRequestConfig & { silent?: boolean }

const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_APP_BASE_API,
  timeout: 120000,
  headers: {
    'Content-Type': 'application/json',
  },
})

// 请求拦截器
http.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
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
    if (!silent) {
      const msgMap: Record<number, string> = {
        400: '请求参数错误',
        404: '资源不存在',
        500: '服务器错误，请稍后重试',
      }
      const msg = msgMap[status] ?? '网络异常，请检查连接'
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
