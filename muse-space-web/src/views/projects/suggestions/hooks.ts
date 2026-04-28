import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import {
  getSuggestions,
  acceptSuggestion,
  applySuggestion,
  ignoreSuggestion,
  batchResolveSuggestions,
  triggerConsistencyCheck,
  triggerCharacterConsistencyCheck,
  importOutline,
} from '@/api/suggestions'
import { useToast } from '@/composables/useToast'
import type {
  AgentSuggestionResponse,
  SuggestionStatus,
  OutlineChapterItem,
} from '@/types/models'
import { parseOutlineChapters } from './utils'

export function initSuggestionsState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string

  const suggestions = ref<AgentSuggestionResponse[]>([])
  const loading = ref(false)
  const filterCategory = ref<string>('')
  const filterStatus = ref<SuggestionStatus | ''>('')

  // 选中的建议 ID
  const selectedIds = ref<Set<string>>(new Set())

  // 手动触发检查（世界观 / 角色）
  const checkModalOpen = ref(false)
  const checkDraftText = ref('')
  const checkLoading = ref(false)
  const checkType = ref<'consistency' | 'character'>('consistency')

  // 操作中的建议 ID（防止重复点击）
  const actionLoadingIds = ref<Set<string>>(new Set())

  const filteredSuggestions = computed(() => {
    return suggestions.value.filter((s) => {
      if (filterCategory.value && s.category !== filterCategory.value) return false
      if (filterStatus.value && s.status !== filterStatus.value) return false
      return true
    })
  })

  const pendingCount = computed(
    () => suggestions.value.filter((s) => s.status === 'Pending').length,
  )

  const allFilteredSelected = computed(() => {
    const visible = filteredSuggestions.value
    return visible.length > 0 && visible.every((s) => selectedIds.value.has(s.id))
  })

  async function loadSuggestions(): Promise<void> {
    loading.value = true
    try {
      suggestions.value = await getSuggestions(projectId)
    } catch {
      // handled by interceptor
    } finally {
      loading.value = false
    }
  }

  function toggleSelect(id: string): void {
    if (selectedIds.value.has(id)) {
      selectedIds.value.delete(id)
    } else {
      selectedIds.value.add(id)
    }
  }

  function toggleSelectAll(): void {
    const visible = filteredSuggestions.value
    if (allFilteredSelected.value) {
      visible.forEach((s) => selectedIds.value.delete(s.id))
    } else {
      visible.forEach((s) => selectedIds.value.add(s.id))
    }
  }

  async function accept(id: string): Promise<void> {
    actionLoadingIds.value.add(id)
    try {
      const updated = await acceptSuggestion(projectId, id)
      updateSuggestionInList(updated)
      toast.success('已接受建议')
    } catch {
      // handled
    } finally {
      actionLoadingIds.value.delete(id)
    }
  }

  async function apply(id: string): Promise<void> {
    actionLoadingIds.value.add(id)
    try {
      const updated = await applySuggestion(projectId, id)
      updateSuggestionInList(updated)
      toast.success('建议已应用')
    } catch {
      // handled
    } finally {
      actionLoadingIds.value.delete(id)
    }
  }

  async function ignore(id: string): Promise<void> {
    actionLoadingIds.value.add(id)
    try {
      const updated = await ignoreSuggestion(projectId, id)
      updateSuggestionInList(updated)
      toast.success('已忽略建议')
    } catch {
      // handled
    } finally {
      actionLoadingIds.value.delete(id)
    }
  }

  async function batchAccept(): Promise<void> {
    const ids = getPendingSelectedIds()
    if (!ids.length) {
      toast.info('没有可接受的待处理建议')
      return
    }
    try {
      const count = await batchResolveSuggestions(projectId, { ids, action: 'Accept' })
      toast.success(`已接受 ${count} 条建议`)
      selectedIds.value.clear()
      await loadSuggestions()
    } catch {
      // handled
    }
  }

  async function batchIgnore(): Promise<void> {
    const ids = getPendingSelectedIds()
    if (!ids.length) {
      toast.info('没有可忽略的待处理建议')
      return
    }
    try {
      const count = await batchResolveSuggestions(projectId, { ids, action: 'Ignore' })
      toast.success(`已忽略 ${count} 条建议`)
      selectedIds.value.clear()
      await loadSuggestions()
    } catch {
      // handled
    }
  }

  async function submitConsistencyCheck(): Promise<void> {
    if (!checkDraftText.value.trim()) return
    checkLoading.value = true
    try {
      if (checkType.value === 'character') {
        await triggerCharacterConsistencyCheck(projectId, { draftText: checkDraftText.value })
      } else {
        await triggerConsistencyCheck(projectId, { draftText: checkDraftText.value })
      }
      toast.success('检查已提交，结果将在后台处理完成后出现')
      checkModalOpen.value = false
      checkDraftText.value = ''
    } catch {
      // handled
    } finally {
      checkLoading.value = false
    }
  }

  function getPendingSelectedIds(): string[] {
    return suggestions.value
      .filter((s) => s.status === 'Pending' && selectedIds.value.has(s.id))
      .map((s) => s.id)
  }

  function updateSuggestionInList(updated: AgentSuggestionResponse): void {
    const idx = suggestions.value.findIndex((s) => s.id === updated.id)
    if (idx !== -1) suggestions.value[idx] = updated
  }

  // ── 大纲导入弹窗 ──────────────────────────────────────────────
  const outlineImportOpen = ref(false)
  const outlineImportChapters = ref<OutlineChapterItem[]>([])
  const outlineImportLoading = ref(false)
  const outlineImportSuggestionId = ref<string>('')

  function openOutlineImport(s: AgentSuggestionResponse): void {
    const items = parseOutlineChapters(s.contentJson)
    outlineImportChapters.value = items.map((ch) => ({ ...ch }))
    outlineImportSuggestionId.value = s.id
    outlineImportOpen.value = true
  }

  function removeOutlineChapter(index: number): void {
    outlineImportChapters.value.splice(index, 1)
  }

  async function submitOutlineImport(): Promise<void> {
    if (outlineImportChapters.value.length === 0) return
    outlineImportLoading.value = true
    try {
      const count = await importOutline(projectId, {
        chapters: outlineImportChapters.value.map((ch) => ({
          number: ch.number,
          title: ch.title,
          goal: ch.goal || undefined,
          summary: ch.summary || undefined,
        })),
      })
      toast.success(`已导入 ${count} 个章节`)
      outlineImportOpen.value = false
      // 将原建议标记为已忽略（已通过自定义导入完成）
      try {
        await ignoreSuggestion(projectId, outlineImportSuggestionId.value)
        await loadSuggestions()
      } catch {
        // non-critical
      }
    } catch {
      // handled
    } finally {
      outlineImportLoading.value = false
    }
  }

  onMounted(loadSuggestions)

  return {
    projectId,
    suggestions,
    filteredSuggestions,
    loading,
    filterCategory,
    filterStatus,
    selectedIds,
    allFilteredSelected,
    pendingCount,
    checkModalOpen,
    checkDraftText,
    checkLoading,
    checkType,
    actionLoadingIds,
    loadSuggestions,
    toggleSelect,
    toggleSelectAll,
    accept,
    apply,
    ignore,
    batchAccept,
    batchIgnore,
    submitConsistencyCheck,
    outlineImportOpen,
    outlineImportChapters,
    outlineImportLoading,
    openOutlineImport,
    removeOutlineChapter,
    submitOutlineImport,
  }
}
