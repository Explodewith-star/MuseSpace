import request from './http'

export type AgentType =
  // 资产提取
  | 'character-extract'
  | 'worldrule-extract'
  | 'styleprofile-extract'
  | 'extract-all'
  // 一致性审查
  | 'consistency-check'
  | 'character-consistency'
  | 'style-consistency'
  // 章节规划
  | 'chapter-auto-plan'
  // 项目摘要
  | 'project-summary'
  // 伏笔追踪
  | 'plot-thread-scan'

/**
 * 一致性审查的文本来源：
 * - latest-draft：取 chapterId 对应章节的 DraftText（默认）
 * - raw-text：直接使用 rawText 字段
 * - all-drafts：拼接项目所有章节的草稿（自动截断）
 */
export type ConsistencyScope = 'latest-draft' | 'raw-text' | 'all-drafts'

export interface AgentTaskRequest {
  agentType: AgentType
  /** 用户的一句话目标 / 补充约束（可选） */
  userInput?: string
  /** 指定原著 ID；不传则后端默认选最近完成索引的一本 */
  novelId?: string
  /** 章节 ID（一致性审查 / 章节自动规划 用） */
  chapterId?: string
  /** 自定义文本（scope=raw-text 时必填） */
  rawText?: string
  /** 一致性审查的文本来源 */
  scope?: ConsistencyScope
}

export interface AgentTaskResponse {
  taskId: string
  /** SignalR 进度事件的 taskType，用于过滤 */
  taskType: string
  message: string
}

export interface ActiveAgentTaskInfo {
  taskType: string
  stage: string
  startedAt: string
}

/**
 * D3-2 菜单 Agent 化统一入口。
 * 任意菜单（角色 / 世界观 / 文风 / 概览 / 章节）都通过这个接口触发后端 Agent，
 * 结果统一进入建议中心 (AgentSuggestion) 等待审核。
 */
export function triggerAgentTask(
  projectId: string,
  payload: AgentTaskRequest,
): Promise<AgentTaskResponse> {
  return request.post(`/projects/${projectId}/agent-tasks`, payload)
}

/** 拉取当前项目下进行中的 Agent 任务列表（用于 SignalR 重连后恢复进度展示）。 */
export function getActiveAgentTasks(projectId: string): Promise<ActiveAgentTaskInfo[]> {
  return request.get(`/projects/${projectId}/agent-tasks/active`)
}

