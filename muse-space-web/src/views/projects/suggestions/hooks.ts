import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import {
  getSuggestions,
  acceptSuggestion,
  applySuggestion,
  ignoreSuggestion,
  reApplySuggestion,
  deleteSuggestion,
  batchResolveSuggestions,
  triggerConsistencyCheck,
  triggerCharacterConsistencyCheck,
  importOutline,
} from '@/api/suggestions'
import { getStoryOutlines } from '@/api/outlines'
import { useToast } from '@/composables/useToast'
import type {
  AgentSuggestionResponse,
  SuggestionStatus,
  OutlineChapterItem,
  StoryOutlineResponse,
} from '@/types/models'
import { parseOutlineChapters } from './utils'

export function initSuggestionsState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string

  const suggestions = ref<AgentSuggestionResponse[]>([])
  const loading = ref(false)
  const filterCategory = ref<string>('')
  const filterStatus = ref<SuggestionStatus | 'AcceptedApplied' | ''>('')

  // 选中的建议 ID
  const selectedIds = ref<Set<string>>(new Set())

  // 手动触发检查（世界观 / 角色）
  const checkModalOpen = ref(false)
  const checkDraftText = ref('')
  const checkLoading = ref(false)
  const checkType = ref<'consistency' | 'character'>('consistency')

  // 操作中的建议 ID（防止重复点击）
  const actionLoadingIds = ref<Set<string>>(new Set())

  // 批量操作 loading
  const batchLoading = ref(false)

  const filteredSuggestions = computed(() => {
    return suggestions.value.filter((s) => {
      if (filterCategory.value && s.category !== filterCategory.value) return false
      if (filterStatus.value === 'AcceptedApplied') return s.status === 'Accepted' || s.status === 'Applied'
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

  const hasPendingSelected = computed(() =>
    [...selectedIds.value].some((id) => {
      const s = suggestions.value.find((x) => x.id === id)
      return s?.status === 'Pending'
    }),
  )

  const hasAppliedSelected = computed(() =>
    [...selectedIds.value].some((id) => {
      const s = suggestions.value.find((x) => x.id === id)
      return s?.status === 'Applied' || s?.status === 'Accepted'
    }),
  )

  const hasIgnoredSelected = computed(() =>
    [...selectedIds.value].some((id) => {
      const s = suggestions.value.find((x) => x.id === id)
      return s?.status === 'Ignored'
    }),
  )

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
    const s = suggestions.value.find((x) => x.id === id)
    const wasApplied = s?.status === 'Applied'
    actionLoadingIds.value.add(id)
    try {
      await ignoreSuggestion(projectId, id)
      // 无论 Pending 还是 Applied，后端均标记为 Ignored（Applied 还会删除对应资产）
      const idx = suggestions.value.findIndex((x) => x.id === id)
      if (idx !== -1) suggestions.value[idx] = { ...suggestions.value[idx], status: 'Ignored' }
      toast.success(wasApplied ? '已忽略，对应资产已从项目中移除' : '已忽略')
    } catch {
      // handled
    } finally {
      actionLoadingIds.value.delete(id)
    }
  }

  async function quickApply(id: string): Promise<void> {
    actionLoadingIds.value.add(id)
    try {
      await acceptSuggestion(projectId, id)
      const updated = await applySuggestion(projectId, id)
      updateSuggestionInList(updated)
      toast.success('已导入到项目资产')
    } catch {
      // handled
    } finally {
      actionLoadingIds.value.delete(id)
    }
  }

  async function reApply(id: string): Promise<void> {
    actionLoadingIds.value.add(id)
    try {
      const updated = await reApplySuggestion(projectId, id)
      updateSuggestionInList(updated)
      toast.success('已重新导入')
    } catch {
      // handled
    } finally {
      actionLoadingIds.value.delete(id)
    }
  }

  async function deleteIgnored(id: string): Promise<void> {
    actionLoadingIds.value.add(id)
    try {
      await deleteSuggestion(projectId, id)
      suggestions.value = suggestions.value.filter((s) => s.id !== id)
      toast.success('已删除')
    } catch {
      // handled
    } finally {
      actionLoadingIds.value.delete(id)
    }
  }

  async function batchApply(): Promise<void> {
    const pendingIds = getPendingSelectedIds()
    const ignoredIds = getIgnoredSelectedIds()
    if (!pendingIds.length && !ignoredIds.length) {
      toast.info('没有可应用的建议')
      return
    }
    batchLoading.value = true
    try {
      let total = 0
      if (pendingIds.length) {
        const count = await batchResolveSuggestions(projectId, { ids: pendingIds, action: 'QuickApply' })
        total += count
      }
      if (ignoredIds.length) {
        const count = await batchResolveSuggestions(projectId, { ids: ignoredIds, action: 'ReApply' })
        total += count
      }
      toast.success(`已应用 ${total} 条建议`)
      selectedIds.value.clear()
      await loadSuggestions()
    } catch {
      // handled
    } finally {
      batchLoading.value = false
    }
  }

  async function batchIgnore(): Promise<void> {
    const ids = [...getPendingSelectedIds(), ...getAppliedSelectedIds()]
    if (!ids.length) {
      toast.info('没有可忽略的建议')
      return
    }
    batchLoading.value = true
    try {
      const count = await batchResolveSuggestions(projectId, { ids, action: 'Ignore' })
      toast.success(`已忽略 ${count} 条建议`)
      selectedIds.value.clear()
      await loadSuggestions()
    } catch {
      // handled
    } finally {
      batchLoading.value = false
    }
  }

  async function batchDelete(): Promise<void> {
    const ids = getIgnoredSelectedIds()
    if (!ids.length) {
      toast.info('没有可删除的已忽略建议')
      return
    }
    batchLoading.value = true
    try {
      const count = await batchResolveSuggestions(projectId, { ids, action: 'Delete' })
      toast.success(`已删除 ${count} 条建议`)
      selectedIds.value.clear()
      await loadSuggestions()
    } catch {
      // handled
    } finally {
      batchLoading.value = false
    }
  }

  // ── 大纲专用删除（单个 / 批量 / 全部） ─────────────────────
  // 删除大纲建议会同时触发后端 RetractAsync 级联删除关联章节（含草稿）
  async function deleteOutline(id: string): Promise<void> {
    batchLoading.value = true
    try {
      // Applied/Pending → Ignore 先触发 RetractAsync 删除章节，再物理删除建议
      const s = suggestions.value.find((x) => x.id === id)
      if (s?.status === 'Applied' || s?.status === 'Pending') {
        await ignoreSuggestion(projectId, id)
      }
      await deleteSuggestion(projectId, id)
      suggestions.value = suggestions.value.filter((x) => x.id !== id)
      selectedIds.value.delete(id)
      toast.success('大纲及关联章节已删除')
    } catch {
      // handled
    } finally {
      batchLoading.value = false
    }
  }

  async function batchDeleteOutlines(): Promise<void> {
    const ids = [...selectedIds.value].filter((id) => {
      const s = suggestions.value.find((x) => x.id === id)
      return s?.category === 'Outline'
    })
    if (!ids.length) {
      toast.info('没有选中的大纲建议')
      return
    }
    batchLoading.value = true
    try {
      // 先 Ignore（触发章节级联删除），再 Delete（删除建议记录）
      const appliedOrPending = ids.filter((id) => {
        const s = suggestions.value.find((x) => x.id === id)
        return s?.status === 'Applied' || s?.status === 'Pending'
      })
      if (appliedOrPending.length) {
        await batchResolveSuggestions(projectId, { ids: appliedOrPending, action: 'Ignore' })
      }
      const count = await batchResolveSuggestions(projectId, { ids, action: 'Delete' })
      toast.success(`已删除 ${count} 条大纲及其关联章节`)
      selectedIds.value.clear()
      await loadSuggestions()
    } catch {
      // handled
    } finally {
      batchLoading.value = false
    }
  }

  async function deleteAllOutlines(): Promise<void> {
    const allOutlineIds = suggestions.value
      .filter((s) => s.category === 'Outline')
      .map((s) => s.id)
    if (!allOutlineIds.length) return
    batchLoading.value = true
    try {
      const appliedOrPending = suggestions.value
        .filter((s) => s.category === 'Outline' && (s.status === 'Applied' || s.status === 'Pending'))
        .map((s) => s.id)
      if (appliedOrPending.length) {
        await batchResolveSuggestions(projectId, { ids: appliedOrPending, action: 'Ignore' })
      }
      const count = await batchResolveSuggestions(projectId, { ids: allOutlineIds, action: 'Delete' })
      toast.success(`已删除全部 ${count} 条大纲及其关联章节`)
      selectedIds.value.clear()
      await loadSuggestions()
    } catch {
      // handled
    } finally {
      batchLoading.value = false
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

  function getAppliedSelectedIds(): string[] {
    return suggestions.value
      .filter((s) => s.status === 'Applied' && selectedIds.value.has(s.id))
      .map((s) => s.id)
  }

  function getIgnoredSelectedIds(): string[] {
    return suggestions.value
      .filter((s) => s.status === 'Ignored' && selectedIds.value.has(s.id))
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
  const outlineImportTargetId = ref<string>('')
  const outlines = ref<StoryOutlineResponse[]>([])

  function openOutlineImport(s: AgentSuggestionResponse): void {
    const items = parseOutlineChapters(s.contentJson)
    outlineImportChapters.value = items.map((ch) => ({ ...ch }))
    outlineImportSuggestionId.value = s.id
    outlineImportTargetId.value = s.targetEntityId ?? ''
    void getStoryOutlines(projectId).then((res) => {
      outlines.value = res
      if (!outlineImportTargetId.value) {
        outlineImportTargetId.value = res.find((o) => o.isDefault)?.id ?? res[0]?.id ?? ''
      }
    })
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
        storyOutlineId: outlineImportTargetId.value || undefined,
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

  onMounted(() => {
    void loadSuggestions()
  })

  return {
    projectId,
    suggestions,
    filteredSuggestions,
    loading,
    filterCategory,
    filterStatus,
    selectedIds,
    allFilteredSelected,
    hasPendingSelected,
    hasAppliedSelected,
    hasIgnoredSelected,
    pendingCount,
    checkModalOpen,
    checkDraftText,
    checkLoading,
    checkType,
    actionLoadingIds,
    batchLoading,
    loadSuggestions,
    toggleSelect,
    toggleSelectAll,
    accept,
    apply,
    ignore,
    quickApply,
    reApply,
    deleteIgnored,
    batchApply,
    batchIgnore,
    batchDelete,
    deleteOutline,
    batchDeleteOutlines,
    deleteAllOutlines,
    submitConsistencyCheck,
    outlineImportOpen,
    outlineImportChapters,
    outlineImportLoading,
    outlineImportTargetId,
    outlines,
    openOutlineImport,
    removeOutlineChapter,
    submitOutlineImport,
  }
}
