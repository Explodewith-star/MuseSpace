import request from './http'
import type { ChapterResponse, CreateChapterRequest, UpdateChapterRequest } from '@/types/models'

export function getChapters(projectId: string): Promise<ChapterResponse[]> {
  return request.get(`/projects/${projectId}/chapters`)
}

export function getChapter(projectId: string, chapterId: string): Promise<ChapterResponse> {
  return request.get(`/projects/${projectId}/chapters/${chapterId}`)
}

export function createChapter(
  projectId: string,
  data: CreateChapterRequest,
): Promise<ChapterResponse> {
  return request.post(`/projects/${projectId}/chapters`, data)
}

export function updateChapter(
  projectId: string,
  chapterId: string,
  data: UpdateChapterRequest,
): Promise<ChapterResponse> {
  return request.put(`/projects/${projectId}/chapters/${chapterId}`, data)
}

export function deleteChapter(projectId: string, chapterId: string): Promise<void> {
  return request.delete(`/projects/${projectId}/chapters/${chapterId}`)
}
