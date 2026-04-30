import request from './http'

export type AgentRunStatus = 'Running' | 'Succeeded' | 'Failed'

export interface AgentRunListItem {
  id: string
  agentName: string
  userId?: string | null
  projectId?: string | null
  status: AgentRunStatus
  inputTokens: number
  outputTokens: number
  durationMs: number
  startedAt: string
  finishedAt?: string | null
  errorMessage?: string | null
}

export interface AgentRunListResponse {
  total: number
  page: number
  pageSize: number
  items: AgentRunListItem[]
}

export interface AgentNameStat {
  agentName: string
  total: number
  succeeded: number
  avgDurationMs: number
}

export interface AgentRunStats {
  totalRuns: number
  succeededRuns: number
  failedRuns: number
  successRate: number
  avgDurationMs: number
  avgTotalTokens: number
  byAgent: AgentNameStat[]
}

export function getAdminAgentRuns(params: {
  agentName?: string
  status?: AgentRunStatus
  page?: number
  pageSize?: number
}) {
  return request.get<AgentRunListResponse>('/admin/agent-runs', { params })
}

export function getAdminAgentRunDetail(id: string) {
  return request.get<AgentRunDetail>(`/admin/agent-runs/${id}`)
}

export interface AgentRunDetail extends AgentRunListItem {
  stepCount: number
  inputPreview?: string | null
  outputPreview?: string | null
  inputFull?: string | null
  outputFull?: string | null
}

export function getAdminAgentRunStats(days = 7) {
  return request.get<AgentRunStats>('/admin/agent-runs/stats', { params: { days } })
}

// ── 功能开关 ────────────────────────────────────────────────────────────────
export interface FeatureFlag {
  key: string
  description?: string | null
  isEnabled: boolean
  updatedAt: string
}

export function getFeatureFlags() {
  return request.get<FeatureFlag[]>('/admin/feature-flags')
}

export function upsertFeatureFlag(data: {
  key: string
  isEnabled: boolean
  description?: string
}) {
  return request.put<unknown>('/admin/feature-flags', data)
}
