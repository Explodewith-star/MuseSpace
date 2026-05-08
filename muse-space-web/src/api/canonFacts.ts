import request from './http'

// ── Chapter Events ──────────────────────────────────────────────

export interface ChapterEventResponse {
  id: string
  storyProjectId: string
  chapterId: string
  order: number
  eventType: string
  eventText: string
  actorCharacterIds?: string[]
  targetCharacterIds?: string[]
  location?: string
  timePoint?: string
  importance?: string
  isIrreversible: boolean
  createdAt: string
  updatedAt: string
}

export interface UpsertChapterEventRequest {
  id?: string
  order?: number
  eventType: string
  eventText: string
  actorCharacterIds?: string[]
  targetCharacterIds?: string[]
  location?: string
  timePoint?: string
  importance?: string
  isIrreversible?: boolean
}

export function getChapterEvents(
  projectId: string,
  chapterId: string,
): Promise<ChapterEventResponse[]> {
  return request.get(`/projects/${projectId}/chapters/${chapterId}/events`)
}

export function createChapterEvent(
  projectId: string,
  chapterId: string,
  data: UpsertChapterEventRequest,
): Promise<ChapterEventResponse> {
  return request.post(`/projects/${projectId}/chapters/${chapterId}/events`, data)
}

export function updateChapterEvent(
  projectId: string,
  chapterId: string,
  id: string,
  data: UpsertChapterEventRequest,
): Promise<ChapterEventResponse> {
  return request.put(`/projects/${projectId}/chapters/${chapterId}/events/${id}`, data)
}

export function deleteChapterEvent(
  projectId: string,
  chapterId: string,
  id: string,
): Promise<void> {
  return request.delete(`/projects/${projectId}/chapters/${chapterId}/events/${id}`)
}

export function replaceChapterEvents(
  projectId: string,
  chapterId: string,
  events: UpsertChapterEventRequest[],
): Promise<ChapterEventResponse[]> {
  return request.put(`/projects/${projectId}/chapters/${chapterId}/events`, { events })
}

// ── Canon Facts ─────────────────────────────────────────────────

export type CanonFactType =
  | 'Relationship'
  | 'Identity'
  | 'LifeStatus'
  | 'WorldState'
  | 'UniqueEvent'

export interface CanonFactResponse {
  id: string
  storyProjectId: string
  factType: string
  subjectId?: string
  objectId?: string
  factKey: string
  factValue: string
  sourceChapterId?: string
  confidence: number
  isLocked: boolean
  invalidatedByChapterId?: string
  notes?: string
  createdAt: string
  updatedAt: string
}

export interface UpsertCanonFactRequest {
  factType: string
  subjectId?: string
  objectId?: string
  factKey: string
  factValue: string
  sourceChapterId?: string
  confidence?: number
  isLocked?: boolean
  invalidatedByChapterId?: string
  notes?: string
}

export interface PatchCanonFactRequest {
  factValue?: string
  isLocked?: boolean
  invalidatedByChapterId?: string
  notes?: string
}

export interface CanonFactQuery {
  onlyActive?: boolean
  onlyLocked?: boolean
}

export function getCanonFacts(
  projectId: string,
  query: CanonFactQuery = {},
): Promise<CanonFactResponse[]> {
  const params: Record<string, string> = {}
  if (query.onlyActive) params.onlyActive = 'true'
  if (query.onlyLocked) params.onlyLocked = 'true'
  return request.get(`/projects/${projectId}/canon-facts`, { params })
}

export function createCanonFact(
  projectId: string,
  data: UpsertCanonFactRequest,
): Promise<CanonFactResponse> {
  return request.post(`/projects/${projectId}/canon-facts`, data)
}

export function updateCanonFact(
  projectId: string,
  id: string,
  data: UpsertCanonFactRequest,
): Promise<CanonFactResponse> {
  return request.put(`/projects/${projectId}/canon-facts/${id}`, data)
}

export function patchCanonFact(
  projectId: string,
  id: string,
  data: PatchCanonFactRequest,
): Promise<CanonFactResponse> {
  return request.patch(`/projects/${projectId}/canon-facts/${id}`, data)
}

export function deleteCanonFact(projectId: string, id: string): Promise<void> {
  return request.delete(`/projects/${projectId}/canon-facts/${id}`)
}
