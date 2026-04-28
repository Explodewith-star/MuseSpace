import request from './http'
import type {
  AgentSuggestionResponse,
  ConsistencyCheckRequest,
  BatchResolveSuggestionsRequest,
  OutlinePlanRequest,
  ImportOutlineRequest,
  SuggestionStatus,
} from '@/types/models'

export function getSuggestions(
  projectId: string,
  params?: { category?: string; status?: SuggestionStatus },
): Promise<AgentSuggestionResponse[]> {
  return request.get(`/projects/${projectId}/suggestions`, { params })
}

export function getSuggestionById(
  projectId: string,
  id: string,
): Promise<AgentSuggestionResponse> {
  return request.get(`/projects/${projectId}/suggestions/${id}`)
}

export function acceptSuggestion(
  projectId: string,
  id: string,
): Promise<AgentSuggestionResponse> {
  return request.post(`/projects/${projectId}/suggestions/${id}/accept`)
}

export function applySuggestion(
  projectId: string,
  id: string,
): Promise<AgentSuggestionResponse> {
  return request.post(`/projects/${projectId}/suggestions/${id}/apply`)
}

export function ignoreSuggestion(
  projectId: string,
  id: string,
): Promise<AgentSuggestionResponse> {
  return request.post(`/projects/${projectId}/suggestions/${id}/ignore`)
}

export function batchResolveSuggestions(
  projectId: string,
  data: BatchResolveSuggestionsRequest,
): Promise<number> {
  return request.post(`/projects/${projectId}/suggestions/batch-resolve`, data)
}

export function triggerConsistencyCheck(
  projectId: string,
  data: ConsistencyCheckRequest,
): Promise<string> {
  return request.post(`/projects/${projectId}/suggestions/consistency-check`, data)
}

export function triggerCharacterConsistencyCheck(
  projectId: string,
  data: ConsistencyCheckRequest,
): Promise<string> {
  return request.post(`/projects/${projectId}/suggestions/character-consistency-check`, data)
}

export function triggerOutlinePlan(
  projectId: string,
  data: OutlinePlanRequest,
): Promise<string> {
  return request.post(`/projects/${projectId}/suggestions/outline-plan`, data)
}

export function importOutline(
  projectId: string,
  data: ImportOutlineRequest,
): Promise<number> {
  return request.post(`/projects/${projectId}/suggestions/outline-import`, data)
}
