import request from './http'
import type { StyleProfileResponse, UpsertStyleProfileRequest } from '@/types/models'

// silent: true 避免 404（未配置）时弹出错误 toast
export function getStyleProfile(projectId: string): Promise<StyleProfileResponse> {
  return request.get(`/projects/${projectId}/style-profile`, { silent: true })
}

export function upsertStyleProfile(
  projectId: string,
  data: UpsertStyleProfileRequest,
): Promise<StyleProfileResponse> {
  return request.put(`/projects/${projectId}/style-profile`, data)
}
