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

/** 批量删除章节（级联删除 Scene / 草稿 / 定稿） */
export function batchDeleteChapters(projectId: string, chapterIds: string[]): Promise<number> {
  return request.post(`/projects/${projectId}/chapters/batch-delete`, { chapterIds })
}

/** 触发章节自动规划（写回 conflict/emotionCurve/keyCharacterIds/mustIncludePoints） */
export function autoPlanChapter(projectId: string, chapterId: string): Promise<string> {
  return request.post(`/projects/${projectId}/chapters/${chapterId}/auto-plan`)
}

/** 触发章节草稿生成 */
export function generateChapterDraft(projectId: string, chapterId: string): Promise<string> {
  return request.post(`/projects/${projectId}/chapters/${chapterId}/generate-draft`)
}
