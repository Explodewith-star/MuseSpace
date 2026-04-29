import type {
  AgentSuggestionResponse,
  SuggestionStatus,
  OutlineChapterItem,
  OutlineVolumeItem,
  OutlinePayload,
} from '@/types/models'

export const CATEGORY_LABELS: Record<string, string> = {
  Consistency: '世界观一致性',
  Character: '角色',
  Outline: '大纲规划',
  WorldRule: '世界观规则',
  StyleProfile: '文风画像',
}

export const CATEGORY_ICONS: Record<string, string> = {
  Consistency: 'i-lucide-shield-alert',
  Character: 'i-lucide-user-check',
  Outline: 'i-lucide-list-tree',
  WorldRule: 'i-lucide-globe',
  StyleProfile: 'i-lucide-pen-tool',
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
    if (Array.isArray(data)) return data
    // 兼容分卷结构：拍平所有卷的 chapters
    if (data && Array.isArray(data.volumes)) {
      return data.volumes.flatMap((v: OutlineVolumeItem) => v.chapters ?? [])
    }
    return []
  } catch {
    return []
  }
}

/** 解析大纲 ContentJson 为卷结构；老格式打包为单卷 */
export function parseOutlineVolumes(raw: string): OutlineVolumeItem[] {
  try {
    const data = JSON.parse(raw)
    if (data && Array.isArray(data.volumes)) {
      return data.volumes as OutlineVolumeItem[]
    }
    if (Array.isArray(data)) {
      // 平铺老格式→包装为单卷
      return [
        {
          number: 1,
          title: '全部章节',
          theme: '',
          chapters: data as OutlineChapterItem[],
        },
      ]
    }
    return []
  } catch {
    return []
  }
}

/** 序列化大纲为 payload 字符串 */
export function stringifyOutlinePayload(volumes: OutlineVolumeItem[]): string {
  const payload: OutlinePayload = { volumes }
  return JSON.stringify(payload)
}

/** 判断是否为资产提取类建议（角色/世界观/文风） */
export function isExtractedAsset(s: AgentSuggestionResponse): boolean {
  return s.category === 'Character' || s.category === 'WorldRule' || s.category === 'StyleProfile'
}

/** 判断建议是否为候选资产（标题包含"候选"） */
export function isCandidate(s: AgentSuggestionResponse): boolean {
  return s.title.startsWith('候选')
}
