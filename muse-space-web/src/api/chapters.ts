import request from './http'
import type { ChapterResponse, CreateChapterRequest, UpdateChapterRequest } from '@/types/models'

export function getChapters(
  projectId: string,
  storyOutlineId?: string,
): Promise<ChapterResponse[]> {
  return request.get(`/projects/${projectId}/chapters`, {
    params: storyOutlineId ? { storyOutlineId } : undefined,
  })
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

/**
 * 批量重排章节编号。chapterIds 顺序即为目标编号（首项 → startNumber，默认 1）。
 * 用于消除"删除章节后编号空洞"。返回实际更新数量。
 */
export function batchReorderChapters(
  projectId: string,
  chapterIds: string[],
  startNumber = 1,
  storyOutlineId?: string,
): Promise<number> {
  return request.post(`/projects/${projectId}/chapters/batch-reorder`, {
    storyOutlineId,
    chapterIds,
    startNumber,
  })
}

/** 触发章节自动规划（写回 conflict/emotionCurve/keyCharacterIds/mustIncludePoints） */
export function autoPlanChapter(projectId: string, chapterId: string): Promise<string> {
  return request.post(`/projects/${projectId}/chapters/${chapterId}/auto-plan`)
}

/** 触发章节草稿生成 */
export interface GenerateChapterDraftRequest {
  referenceText?: string
  referenceFocus?: string
  referenceStrength?: string
  includeNovelContext?: boolean
  // Module E：续写/外传模式
  generationMode?: 'Original' | 'ContinueFromOriginal' | 'SideStoryFromOriginal' | 'ExpandOrRewrite'
  sourceNovelId?: string
  continuationStartChapterNumber?: number
  originalRangeStart?: number
  originalRangeEnd?: number
  relatedCharacterIds?: string[]
  branchTopic?: string
  divergencePolicy?: 'StrictCanon' | 'SoftCanon' | 'AlternateTimeline'
}

export function generateChapterDraft(
  projectId: string,
  chapterId: string,
  data?: GenerateChapterDraftRequest,
): Promise<string> {
  return request.post(`/projects/${projectId}/chapters/${chapterId}/generate-draft`, data ?? {})
}

/** 一键采用为定稿的响应体 */
export interface AdoptDraftResponse {
  adopted: boolean
  finalLength: number
  previousFinalLength: number
  draftLength: number
}

/**
 * 一键将草稿采用为定稿。
 * - 草稿为空 → 抛错（toast 提示）。
 * - 定稿已有内容且未传 overrideExisting=true → 后端返回 409，调用方应捕获并弹二次确认。
 *   409 时 axios 会进入 reject 分支，需通过 silent + 自行解析 error.response.data 处理。
 */
export function adoptChapterDraft(
  projectId: string,
  chapterId: string,
  overrideExisting = false,
): Promise<AdoptDraftResponse> {
  return request.post(
    `/projects/${projectId}/chapters/${chapterId}/adopt-draft`,
    { overrideExisting },
    // 静默模式：让调用方自行处理 409，不要走全局 toast
    { silent: true },
  )
}

// ─── A3 批量章节草稿生成 ───────────────────────────────────────────────────

/// <summary>批量生成草稿请求体。</summary>
export interface BatchGenerateDraftRequest {
  storyOutlineId?: string
  fromNumber: number
  toNumber: number
  /** 是否跳过已有草稿的章节，默认 false（覆盖原草稿）。 */
  skipChaptersWithDraft?: boolean
  /** 是否在生成草稿前自动填充写作计划，默认 true。 */
  autoFillPlan?: boolean
}

/** 批量生成任务的运行状态。 */
export interface ChapterBatchDraftRunResponse {
  id: string
  storyProjectId: string
  storyOutlineId: string
  fromNumber: number
  toNumber: number
  totalCount: number
  completedCount: number
  failedCount: number
  skippedCount: number
  failedChapterIds: string[]
  currentChapterId?: string | null
  status:
    | 'Pending'
    | 'Running'
    | 'Completed'
    | 'PartiallyFailed'
    | 'Cancelled'
    | 'Failed'
  cancelRequested: boolean
  createdAt: string
  startedAt?: string | null
  finishedAt?: string | null
  errorMessage?: string | null
}

/** 默认上限。 */
export const DEFAULT_BATCH_DRAFT_SIZE = 5

/** 硬上限。 */
export const HARD_MAX_BATCH_DRAFT_SIZE = 10

/** 提交批量生成任务。 */
export function batchGenerateDrafts(
  projectId: string,
  payload: BatchGenerateDraftRequest,
): Promise<ChapterBatchDraftRunResponse> {
  return request.post(
    `/projects/${projectId}/chapters/batch-generate-draft`,
    payload,
  )
}

/** 查询单个批次状态。 */
export function getChapterBatchRun(
  projectId: string,
  runId: string,
): Promise<ChapterBatchDraftRunResponse> {
  return request.get(`/projects/${projectId}/chapter-batch-runs/${runId}`)
}

/** 列出最近批次（默认 10 条）。 */
export function listChapterBatchRuns(
  projectId: string,
  take = 10,
  storyOutlineId?: string,
): Promise<ChapterBatchDraftRunResponse[]> {
  return request.get(`/projects/${projectId}/chapter-batch-runs`, {
    params: storyOutlineId ? { take, storyOutlineId } : { take },
  })
}

/** 请求中止：当前章节完成后停止后续。 */
export function cancelChapterBatchRun(
  projectId: string,
  runId: string,
): Promise<boolean> {
  return request.post(
    `/projects/${projectId}/chapter-batch-runs/${runId}/cancel`,
  )
}
