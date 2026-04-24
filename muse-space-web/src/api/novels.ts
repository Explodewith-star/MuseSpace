import request from './http'
import type { NovelResponse } from '@/types/models'

export function getNovels(projectId: string): Promise<NovelResponse[]> {
  return request.get(`/projects/${projectId}/novels`)
}

export function uploadNovel(
  projectId: string,
  file: File,
  title?: string,
): Promise<NovelResponse> {
  const form = new FormData()
  form.append('file', file)
  const url = title
    ? `/projects/${projectId}/novels?title=${encodeURIComponent(title)}`
    : `/projects/${projectId}/novels`
  return request.post(url, form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

export function getNovelStatus(projectId: string, novelId: string): Promise<NovelResponse> {
  return request.get(`/projects/${projectId}/novels/${novelId}/status`)
}

export function deleteNovel(projectId: string, novelId: string): Promise<void> {
  return request.delete(`/projects/${projectId}/novels/${novelId}`)
}
