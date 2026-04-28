import type { AgentSuggestionResponse, SuggestionStatus, OutlineChapterItem } from '@/types/models'

export const CATEGORY_LABELS: Record<string, string> = {
  Consistency: '世界观一致性',
  Character: '角色一致性',
  Outline: '大纲规划',
}

export const CATEGORY_ICONS: Record<string, string> = {
  Consistency: 'i-lucide-shield-alert',
  Character: 'i-lucide-user-check',
  Outline: 'i-lucide-list-tree',
}

export const STATUS_LABELS: Record<SuggestionStatus, string> = {
  Pending: '待处理',
  Accepted: '已接受',
  Applied: '已应用',
  Ignored: '已忽略',
}

export const STATUS_VARIANTS: Record<
  SuggestionStatus,
  'default' | 'primary' | 'accent' | 'success' | 'danger' | 'muted'
> = {
  Pending: 'accent',
  Accepted: 'primary',
  Applied: 'success',
  Ignored: 'muted',
}

export const SEVERITY_LABELS: Record<string, string> = {
  high: '高',
  medium: '中',
  low: '低',
}

export const SEVERITY_VARIANTS: Record<
  string,
  'default' | 'primary' | 'accent' | 'success' | 'danger' | 'muted'
> = {
  high: 'danger',
  medium: 'accent',
  low: 'muted',
}

export function parseContentJson(raw: string): Record<string, unknown> {
  try {
    return JSON.parse(raw)
  } catch {
    return {}
  }
}

export function canAccept(s: AgentSuggestionResponse): boolean {
  return s.status === 'Pending'
}

export function canApply(s: AgentSuggestionResponse): boolean {
  return s.status === 'Accepted'
}

export function canIgnore(s: AgentSuggestionResponse): boolean {
  return s.status === 'Pending'
}

export function isOutline(s: AgentSuggestionResponse): boolean {
  return s.category === 'Outline'
}

export function parseOutlineChapters(raw: string): OutlineChapterItem[] {
  try {
    const data = JSON.parse(raw)
    return Array.isArray(data) ? data : []
  } catch {
    return []
  }
}
