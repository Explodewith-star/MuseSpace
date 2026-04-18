import request from './http'
import type { WorldRuleResponse, CreateWorldRuleRequest, UpdateWorldRuleRequest } from '@/types/models'

export function getWorldRules(projectId: string): Promise<WorldRuleResponse[]> {
  return request.get(`/projects/${projectId}/world-rules`)
}

export function createWorldRule(
  projectId: string,
  data: CreateWorldRuleRequest,
): Promise<WorldRuleResponse> {
  return request.post(`/projects/${projectId}/world-rules`, data)
}

export function updateWorldRule(
  projectId: string,
  ruleId: string,
  data: UpdateWorldRuleRequest,
): Promise<WorldRuleResponse> {
  return request.put(`/projects/${projectId}/world-rules/${ruleId}`, data)
}

export function deleteWorldRule(projectId: string, ruleId: string): Promise<void> {
  return request.delete(`/projects/${projectId}/world-rules/${ruleId}`)
}
