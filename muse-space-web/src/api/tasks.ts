import request from './http'

export interface BackgroundTaskResponse {
  id: string
  taskType: string
  status: string
  progress: number
  title: string
  statusMessage?: string
  errorMessage?: string
  createdAt: string
  updatedAt: string
}

export function getActiveTasks(): Promise<BackgroundTaskResponse[]> {
  return request.get('/tasks/active')
}

export function getRecentTasks(limit = 50): Promise<BackgroundTaskResponse[]> {
  return request.get('/tasks', { params: { limit } })
}
