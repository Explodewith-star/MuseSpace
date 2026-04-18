import request from '@/utils/request'

// 示例 API 接口
export function getExampleList(params?: Record<string, unknown>) {
  return request.get('/example/list', { params })
}

export function createExample(data: Record<string, unknown>) {
  return request.post('/example', data)
}
