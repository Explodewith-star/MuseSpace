<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AppModal from '@/components/base/AppModal.vue'
import { initChaptersState } from './hooks'
import { triggerOutlinePlan } from '@/api/suggestions'
import { getSuggestions } from '@/api/suggestions'
import { createStoryOutline, getStoryOutlines, deleteStoryOutline, adjustOutline } from '@/api/outlines'
import { getCharacters } from '@/api/characters'
import { getWorldRules } from '@/api/worldRules'
import { exportProjectChapters, type ExportFormat } from '@/api/export'
import {
  batchGenerateDrafts,
  batchDeleteChapters,
  cancelChapterBatchRun,
  getChapterBatchRun,
  listChapterBatchRuns,
  DEFAULT_BATCH_DRAFT_SIZE,
  HARD_MAX_BATCH_DRAFT_SIZE,
  type ChapterBatchDraftRunResponse,
} from '@/api/chapters'
import type { AdjustOutlineRequest } from '@/api/outlines'
import { useAgentProgress } from '@/composables/useAgentProgress'
import { useToast } from '@/composables/useToast'
import type { GenerationMode, StoryOutlineResponse } from '@/types/models'

const router = useRouter()
const route = useRoute()
const toast = useToast()
const projectId = route.params.id as string

const outlines = ref<StoryOutlineResponse[]>([])
const outlinesLoading = ref(false)
const selectedOutlineId = ref('')
const selectedOutline = computed(() =>
  outlines.value.find((o) => o.id === selectedOutlineId.value) ?? null,
)
const selectedOutlineChapters = computed(() => chapters.value)
const hasSelectedOutline = computed(() => !!selectedOutline.value)
const hasChapters = computed(() => chapters.value.length > 0)

// ── Mode Tab + 批次子导航 ──────────────────────────────────
const ALL_MODES: GenerationMode[] = [
  'Original',
  'ContinueFromOriginal',
  'SideStoryFromOriginal',
  'ExpandOrRewrite',
]

const MODE_LABELS: Record<GenerationMode, string> = {
  Original: '原创主线',
  ContinueFromOriginal: '原著续写',
  SideStoryFromOriginal: '支线番外',
  ExpandOrRewrite: '扩写/改写',
}

const MODE_ICONS: Record<GenerationMode, string> = {
  Original: 'i-lucide-pen-line',
  ContinueFromOriginal: 'i-lucide-book-copy',
  SideStoryFromOriginal: 'i-lucide-git-branch',
  ExpandOrRewrite: 'i-lucide-text-cursor-input',
}

const selectedMode = ref<GenerationMode>('Original')

/** 每个模式下有多少条大纲 */
const modeOutlineCounts = computed(() => {
  const counts: Record<string, number> = {}
  for (const m of ALL_MODES) counts[m] = 0
  for (const o of outlines.value) counts[o.mode] = (counts[o.mode] ?? 0) + 1
  return counts
})

/** 当前模式下的大纲列表（按创建时间排序） */
const modeOutlines = computed(() =>
  outlines.value
    .filter((o) => o.mode === selectedMode.value)
    .sort((a, b) => (a.chainIndex || 0) - (b.chainIndex || 0) || a.createdAt.localeCompare(b.createdAt)),
)

function selectMode(mode: GenerationMode) {
  selectedMode.value = mode
  // 切换模式后，自动选中该模式下第一个大纲
  const first = modeOutlines.value[0]
  if (first) {
    selectOutline(first.id)
  } else {
    selectedOutlineId.value = ''
  }
}

async function loadOutlines() {
  outlinesLoading.value = true
  try {
    outlines.value = await getStoryOutlines(projectId)
    if (!selectedOutlineId.value || !outlines.value.some((o) => o.id === selectedOutlineId.value)) {
      // 先选中默认大纲，再据此确定 mode tab
      const defaultOutline = outlines.value.find((o) => o.isDefault) ?? outlines.value[0]
      if (defaultOutline) {
        selectedOutlineId.value = defaultOutline.id
        selectedMode.value = defaultOutline.mode as GenerationMode
      }
    }
  } catch {
    // handled
  } finally {
    outlinesLoading.value = false
  }
}

function selectOutline(id: string) {
  if (selectedOutlineId.value === id) return
  selectedOutlineId.value = id
}

const outlineModalOpen = ref(false)
const outlineSaving = ref(false)
const outlineForm = reactive({
  name: '',
  mode: 'Original' as GenerationMode,
  outlineSummary: '',
  branchTopic: '',
  // AI 规划选项
  withAiPlan: false,
  aiGoal: '',
  aiChapterCount: '10',
})

function openOutlineModal(mode?: GenerationMode) {
  Object.assign(outlineForm, {
    name: '',
    mode: mode ?? selectedMode.value,
    outlineSummary: '',
    branchTopic: '',
    withAiPlan: false,
    aiGoal: '',
    aiChapterCount: '10',
  })
  loadContextStats()
  outlineModalOpen.value = true
}

async function submitOutline() {
  if (!outlineForm.name.trim()) return
  if (outlineForm.withAiPlan && !outlineForm.aiGoal.trim()) return
  outlineSaving.value = true
  try {
    // 同模式下已有批次时，将最后一个批次作为前驱（支持 OutlinePlanJob 读取前驱摘要）
    const sameModeOutlines = modeOutlines.value // 已按 chainIndex/createdAt 排序
    const lastInMode = sameModeOutlines.length > 0 ? sameModeOutlines[sameModeOutlines.length - 1] : null
    const outline = await createStoryOutline(projectId, {
      name: outlineForm.name.trim(),
      mode: outlineForm.mode,
      outlineSummary: outlineForm.outlineSummary.trim() || undefined,
      branchTopic: outlineForm.branchTopic.trim() || undefined,
      previousOutlineId: lastInMode?.id,
      chainId: lastInMode?.chainId ?? undefined,
    })
    outlines.value.push(outline)
    selectedOutlineId.value = outline.id
    outlineModalOpen.value = false

    if (outlineForm.withAiPlan) {
      // 创建完立即触发 AI 规划
      await triggerOutlinePlan(projectId, {
        storyOutlineId: outline.id,
        goal: outlineForm.aiGoal.trim(),
        chapterCount: Number(outlineForm.aiChapterCount),
        mode: 'new',
      })
      toast.success('大纲已创建，AI 规划已提交，请关注进度')
    } else {
      toast.success('大纲已创建')
    }
  } catch {
    // handled
  } finally {
    outlineSaving.value = false
  }
}

const {
  chapters,
  loading,
  loadChapters,
  drawerOpen,
  createForm,
  createLoading,
  openCreate,
  submitCreate,
  deleteTarget,
  deleteLoading,
  openDelete,
  cancelDelete,
  confirmDelete,
  reorderAll,
  reorderLoading,
} = initChaptersState({ getSelectedOutlineId: () => selectedOutlineId.value || undefined })

watch(selectedOutlineId, async (id) => {
  if (!id) {
    chapters.value = []
    return
  }
  await loadChapters()
  await refreshPendingOutline()
}, { flush: 'post' })

const STATUS_VARIANTS: Record<number, string> = {
  0: 'muted',
  1: 'accent',
  2: 'primary',
  3: 'success',
}
const STATUS_LABELS: Record<number, string> = { 0: '计划中', 1: '草稿中', 2: '修改中', 3: '已定稿' }

function goDetail(chapterId: string) {
  router.push(`/projects/${route.params.id}/chapters/${chapterId}`)
}

// ── AI 大纲规划 ─────────────────────────────────────────────
const planModalOpen = ref(false)
const planForm = reactive({
  goal: '',
  chapterCount: '10',
  mode: 'new' as 'new' | 'continue' | 'extra',
})
const planLoading = ref(false)

// 上下文统计
const characterCount = ref(0)
const worldRuleCount = ref(0)

async function loadContextStats() {
  try {
    const [chars, rules] = await Promise.all([getCharacters(projectId), getWorldRules(projectId)])
    characterCount.value = chars.length
    worldRuleCount.value = rules.length
  } catch {
    // ignore
  }
}

// "继续规划" —— 仅用于已有大纲追加章节计划的场景
function openPlanModal() {
  planForm.mode = hasChapters.value ? 'continue' : 'new'
  planForm.goal = ''
  planForm.chapterCount = selectedOutline.value?.targetChapterCount
    ? String(selectedOutline.value.targetChapterCount)
    : '10'
  loadContextStats()
  planModalOpen.value = true
}

const planActionLabel = computed(() => (hasChapters.value ? '继续规划' : 'AI 规划章节'))
const planActionTitle = computed(() =>
  hasChapters.value
    ? '在当前大纲基础上继续 AI 规划更多章节'
    : '为当前大纲发起首次 AI 章节规划',
)
const planModalTitle = computed(() => (hasChapters.value ? '继续规划大纲' : 'AI 规划章节'))
const planHintText = computed(() =>
  hasChapters.value
    ? `当前大纲已有 ${chapters.value.length} 章，AI 会在此基础上继续规划后续章节。`
    : '当前大纲还没有章节，AI 会基于大纲目标生成首批章节计划。',
)
const planGoalLabel = computed(() => (hasChapters.value ? '续写方向 *' : '故事目标 *'))
const planGoalPlaceholder = computed(() =>
  hasChapters.value
    ? '描述接下来希望故事走向哪里，如：主角踏入皇城，与旧敌决战，最终揭开身世之谜...'
    : '描述这条大纲希望展开成什么故事，如：主角在旧城鬼域中成长，逐步揭开家族秘密并对抗幕后黑手... ',
)
const planCountLabel = computed(() => (hasChapters.value ? '追加章节数' : '规划章节数'))
const planSubmitLabel = computed(() => (hasChapters.value ? '开始规划' : '生成章节规划'))

async function submitPlan() {
  if (!planForm.goal.trim()) return
  planLoading.value = true
  try {
    await triggerOutlinePlan(projectId, {
      storyOutlineId: selectedOutlineId.value || undefined,
      goal: planForm.goal,
      chapterCount: Number(planForm.chapterCount),
      mode: planForm.mode,
    })
    planModalOpen.value = false
    toast.success('大纲规划已提交，请关注下方进度状态')
  } catch {
    // handled by interceptor
  } finally {
    planLoading.value = false
  }
}

// ── 一键导出 ────────────────────────────────────────────────
const exportModalOpen = ref(false)
const exportLoading = ref(false)
const exportForm = reactive({
  format: 'md' as ExportFormat,
  rangeMode: 'all' as 'all' | 'custom',
  fromNumber: 1,
  toNumber: 1,
  onlyFinal: true,
  includeDraft: false,
})

const finalChapterCount = computed(() => chapters.value.filter((c) => c.status === 3).length)

function openExportModal() {
  exportForm.format = 'md'
  exportForm.rangeMode = 'all'
  exportForm.onlyFinal = true
  exportForm.includeDraft = false
  if (chapters.value.length > 0) {
    exportForm.fromNumber = chapters.value[0].number
    exportForm.toNumber = chapters.value[chapters.value.length - 1].number
  }
  exportModalOpen.value = true
}

async function submitExport() {
  exportLoading.value = true
  try {
    await exportProjectChapters(projectId, {
      storyOutlineId: selectedOutlineId.value || undefined,
      format: exportForm.format,
      from: exportForm.rangeMode === 'custom' ? exportForm.fromNumber : undefined,
      to: exportForm.rangeMode === 'custom' ? exportForm.toNumber : undefined,
      onlyFinal: exportForm.onlyFinal,
      includeDraft: !exportForm.onlyFinal && exportForm.includeDraft,
    })
    exportModalOpen.value = false
    toast.success('已开始下载')
  } catch (err) {
    const msg = err instanceof Error ? err.message : '导出失败'
    toast.error(msg)
  } finally {
    exportLoading.value = false
  }
}

// ── SignalR 进度订阅 ──────────────────────────────────────────
const { latestEvent, joinProject, leaveProject } = useAgentProgress()

const outlineStage = computed(() => {
  const e = latestEvent.value
  if (!e || e.taskType !== 'outline') return null
  return e
})

const adjustStage = computed(() => {
  const e = latestEvent.value
  if (!e || e.taskType !== 'outline-adjust') return null
  return e
})

/** 本地状态：提交后直到 SignalR 返回 done/failed 期间显示进度 */
const isAdjusting = ref(false)

// ── A3 批量生成草稿 ─────────────────────────────────────────
const batchModalOpen = ref(false)
const batchSubmitLoading = ref(false)
const batchForm = reactive({
  fromNumber: 1,
  toNumber: 1,
  skipChaptersWithDraft: false,
  autoFillPlan: true,
})
const activeBatchRun = ref<ChapterBatchDraftRunResponse | null>(null)
let batchPollTimer: ReturnType<typeof setInterval> | null = null
const cancelBatchLoading = ref(false)

const batchSize = computed(() => {
  const n = batchForm.toNumber - batchForm.fromNumber + 1
  return Number.isFinite(n) && n > 0 ? n : 0
})

const batchProgressPercent = computed(() => {
  const r = activeBatchRun.value
  if (!r || r.totalCount === 0) return 0
  return Math.min(
    100,
    Math.round(((r.completedCount + r.failedCount + r.skippedCount) / r.totalCount) * 100),
  )
})

const isBatchRunning = computed(() => {
  const s = activeBatchRun.value?.status
  return s === 'Pending' || s === 'Running'
})

const BATCH_STATUS_LABELS: Record<ChapterBatchDraftRunResponse['status'], string> = {
  Pending: '排队中',
  Running: '生成中',
  Completed: '已完成',
  PartiallyFailed: '部分失败',
  Cancelled: '已中止',
  Failed: '失败',
}

function openBatchModal() {
  if (selectedOutlineChapters.value.length === 0) return
  const first = selectedOutlineChapters.value[0].number
  const planned = selectedOutlineChapters.value.find((c) => c.status === 0)
  const start = planned ? planned.number : first
  batchForm.fromNumber = start
  batchForm.toNumber = Math.min(
    selectedOutlineChapters.value[selectedOutlineChapters.value.length - 1].number,
    start + DEFAULT_BATCH_DRAFT_SIZE - 1,
  )
  batchForm.skipChaptersWithDraft = false
  batchForm.autoFillPlan = true
  batchModalOpen.value = true
}

function startBatchPolling(runId: string) {
  stopBatchPolling()
  batchPollTimer = setInterval(async () => {
    try {
      const r = await getChapterBatchRun(projectId, runId)
      activeBatchRun.value = r
      if (!isBatchRunning.value) {
        stopBatchPolling()
        // 完成后刷新章节列表（草稿字段会变化）
        await loadChapters()
        const evtMap: Record<string, () => void> = {
          Completed: () => toast.success(`批量生成完成：${r.completedCount}/${r.totalCount}`),
          PartiallyFailed: () =>
            toast.warning(`部分完成：成功 ${r.completedCount}，失败 ${r.failedCount}`),
          Cancelled: () => toast.success(`已中止：成功 ${r.completedCount}/${r.totalCount}`),
          Failed: () => toast.error(r.errorMessage ?? '批量生成失败'),
        }
        evtMap[r.status]?.()
      }
    } catch {
      stopBatchPolling()
    }
  }, 3000)
}

function stopBatchPolling() {
  if (batchPollTimer) {
    clearInterval(batchPollTimer)
    batchPollTimer = null
  }
}

async function submitBatch() {
  if (batchSize.value <= 0) {
    toast.error('请填写有效的章节范围')
    return
  }
  if (batchSize.value > HARD_MAX_BATCH_DRAFT_SIZE) {
    toast.error(`单批最多 ${HARD_MAX_BATCH_DRAFT_SIZE} 章`)
    return
  }
  batchSubmitLoading.value = true
  try {
    const run = await batchGenerateDrafts(projectId, {
      storyOutlineId: selectedOutlineId.value || undefined,
      fromNumber: batchForm.fromNumber,
      toNumber: batchForm.toNumber,
      skipChaptersWithDraft: batchForm.skipChaptersWithDraft,
      autoFillPlan: batchForm.autoFillPlan,
    })
    activeBatchRun.value = run
    batchModalOpen.value = false
    toast.success(`已提交批量生成任务（${run.totalCount} 章）`)
    startBatchPolling(run.id)
  } catch {
    // 全局拦截器已 toast
  } finally {
    batchSubmitLoading.value = false
  }
}

async function cancelBatch() {
  const r = activeBatchRun.value
  if (!r) return
  cancelBatchLoading.value = true
  try {
    await cancelChapterBatchRun(projectId, r.id)
    // 立即更新本地状态，使按钮可用，无需等轮询
    activeBatchRun.value = { ...r, status: 'Cancelled', cancelRequested: true }
    stopBatchPolling()
    toast.success('已中止批量任务')
  } catch {
    // ignore
  } finally {
    cancelBatchLoading.value = false
  }
}

const stageLabels: Record<string, string> = {
  started: '正在收集项目设定...',
  generating: 'AI 正在规划大纲...',
  done: '大纲已生成',
  failed: '生成失败',
}

const adjustStageLabels: Record<string, string> = {
  started: '正在收集章节上下文…',
  generating: 'AI 正在调整大纲章节…',
  done: '大纲调整已完成',
  failed: '调整失败',
}

// ── 持久化"待处理大纲"计数 ──────────────────────────────────
const pendingOutlineCount = ref(0)

async function refreshPendingOutline() {
  try {
    const list = await getSuggestions(projectId, {
      category: 'Outline',
      status: 'Pending',
      targetEntityId: selectedOutlineId.value || undefined,
    })
    pendingOutlineCount.value = list.length
  } catch {
    // ignore
  }
}

onMounted(async () => {
  joinProject(projectId)
  await loadOutlines()
  await loadChapters()
  refreshPendingOutline()
  // 恢复可能在进行中的批量任务（防止刷新后丢失轮询）
  try {
    const runs = await listChapterBatchRuns(projectId, 1, selectedOutlineId.value || undefined)
    const active = runs[0]
    if (active && (active.status === 'Pending' || active.status === 'Running')) {
      activeBatchRun.value = active
      startBatchPolling(active.id)
    }
  } catch {
    // ignore
  }
})
onUnmounted(() => {
  leaveProject(projectId)
  stopBatchPolling()
})
watch(outlineStage, (e) => {
  if (e?.stage === 'done') {
    toast.success(e.summary ?? '大纲已生成，前往建议中心查看')
    refreshPendingOutline()
  } else if (e?.stage === 'failed') {
    toast.error(e.error ?? '大纲生成失败')
  }
})

// ── 大纲删除 ─────────────────────────────────────────────
const outlineDeleteTarget = ref<(typeof outlines.value)[0] | null>(null)
const outlineDeleteLoading = ref(false)

function openOutlineDelete(outline: (typeof outlines.value)[0]) {
  outlineDeleteTarget.value = outline
}

function cancelOutlineDelete() {
  outlineDeleteTarget.value = null
}

async function confirmOutlineDelete() {
  const target = outlineDeleteTarget.value
  if (!target) return
  outlineDeleteLoading.value = true
  try {
    await deleteStoryOutline(projectId, target.id)
    outlines.value = outlines.value.filter((o) => o.id !== target.id)
    // 若删除的是当前选中大纲，切换到第一个
    if (selectedOutlineId.value === target.id) {
      const first = outlines.value[0]
      if (first) {
        selectedOutlineId.value = first.id
        selectedMode.value = first.mode as GenerationMode
      } else {
        selectedOutlineId.value = ''
      }
    }
    outlineDeleteTarget.value = null
    toast.success(`大纲《${target.name}》已删除`)
  } catch {
    // handled by interceptor
  } finally {
    outlineDeleteLoading.value = false
  }
}

// ── 编辑模式（批量章节删除 + AI 调整） ────────────────────
const editMode = ref(false)
const selectedChapterIds = ref<Set<string>>(new Set())
const batchChapterDeleteLoading = ref(false)
const batchChapterDeleteConfirmVisible = ref(false)

function toggleEditMode() {
  editMode.value = !editMode.value
  if (!editMode.value) selectedChapterIds.value = new Set()
}

function toggleChapterSelect(id: string) {
  const s = new Set(selectedChapterIds.value)
  if (s.has(id)) s.delete(id)
  else s.add(id)
  selectedChapterIds.value = s
}

const allChaptersSelected = computed(
  () => chapters.value.length > 0 && selectedChapterIds.value.size === chapters.value.length,
)

function toggleSelectAll() {
  if (allChaptersSelected.value) {
    selectedChapterIds.value = new Set()
  } else {
    selectedChapterIds.value = new Set(chapters.value.map((c) => c.id))
  }
}

async function confirmBatchChapterDelete() {
  if (selectedChapterIds.value.size === 0) return
  batchChapterDeleteLoading.value = true
  try {
    const ids = [...selectedChapterIds.value]
    await batchDeleteChapters(projectId, ids)
    chapters.value = chapters.value.filter((c) => !selectedChapterIds.value.has(c.id))
    // 更新大纲章节数
    if (selectedOutline.value) {
      const o = outlines.value.find((x) => x.id === selectedOutline.value!.id)
      if (o) o.chapterCount = Math.max(0, o.chapterCount - ids.length)
    }
    selectedChapterIds.value = new Set()
    batchChapterDeleteConfirmVisible.value = false
    toast.success(`已删除 ${ids.length} 个章节`)
  } catch {
    // handled
  } finally {
    batchChapterDeleteLoading.value = false
  }
}

// ── AI 续规建议 ────────────────────────────────────────────
// 基于当前模式已有大纲的上下文，辅助用户快速规划「下一批」大纲
const chainPlanModalOpen = ref(false)
const chainPlanForm = reactive({
  goal: '',
  chapterCount: '15',
})
const chainPlanLoading = ref(false)

/** 当前模式下最后一个大纲（作为续写前驱） */
const lastModeOutline = computed(() =>
  modeOutlines.value.length > 0 ? modeOutlines.value[modeOutlines.value.length - 1] : null,
)

function openChainPlanModal() {
  chainPlanForm.goal = ''
  chainPlanForm.chapterCount = '15'
  loadContextStats()
  chainPlanModalOpen.value = true
}

async function submitChainPlan() {
  if (!chainPlanForm.goal.trim()) return
  chainPlanLoading.value = true
  try {
    const last = lastModeOutline.value
    // 自动命名：「模式 · 续篇 N」
    const autoName = `${MODE_LABELS[selectedMode.value]} · 续篇 ${modeOutlines.value.length + 1}`
    const outline = await createStoryOutline(projectId, {
      name: autoName,
      mode: selectedMode.value,
      previousOutlineId: last?.id,
      chainId: last?.chainId ?? undefined,
    })
    outlines.value.push(outline)
    selectedOutlineId.value = outline.id
    chainPlanModalOpen.value = false
    await triggerOutlinePlan(projectId, {
      storyOutlineId: outline.id,
      goal: chainPlanForm.goal.trim(),
      chapterCount: Number(chainPlanForm.chapterCount),
      mode: 'continue',
    })
    toast.success('续篇规划已提交，请关注进度')
  } catch {
    // handled
  } finally {
    chainPlanLoading.value = false
  }
}

// ── AI 大纲调整 ────────────────────────────────────────────
const adjustModalOpen = ref(false)
const adjustForm = reactive({
  instruction: '',
  targetCount: '',
})
const adjustLoading = ref(false)

function openAdjustModal() {
  adjustForm.instruction = ''
  adjustForm.targetCount = ''
  adjustModalOpen.value = true
}

async function submitAdjust() {
  if (!adjustForm.instruction.trim()) return
  if (selectedChapterIds.value.size === 0) return
  adjustLoading.value = true
  try {
    const targetNumbers = chapters.value
      .filter((c) => selectedChapterIds.value.has(c.id))
      .map((c) => c.number)
    const req: AdjustOutlineRequest = {
      instruction: adjustForm.instruction.trim(),
      targetChapterNumbers: targetNumbers,
      targetCount: adjustForm.targetCount ? Number(adjustForm.targetCount) : undefined,
    }
    await adjustOutline(projectId, selectedOutlineId.value, req)
    adjustModalOpen.value = false
    selectedChapterIds.value = new Set()
    editMode.value = false
    isAdjusting.value = true  // 显示进度条，等待 SignalR 通知
    toast.success('大纲调整任务已提交，AI 正在处理…')
  } catch {
    // handled
  } finally {
    adjustLoading.value = false
  }
}

// 监听 outline-adjust 进度事件，完成后刷新章节列表
watch(latestEvent, async (e) => {
  if (!e || e.taskType !== 'outline-adjust') return
  if (e.stage === 'done') {
    isAdjusting.value = false
    toast.success(e.summary ?? '大纲调整已完成')
    await loadChapters()
  } else if (e.stage === 'failed') {
    isAdjusting.value = false
    toast.error(e.error ?? '大纲调整失败')
  }
})
</script>

<template>
  <div class="page">
    <!-- Mode Tabs -->
    <div class="mode-tabs">
      <button
        v-for="mode in ALL_MODES"
        :key="mode"
        type="button"
        :class="['mode-tab', { 'mode-tab--active': selectedMode === mode }]"
        @click="selectMode(mode)"
      >
        <i :class="MODE_ICONS[mode]" />
        <span class="mode-tab__label">{{ MODE_LABELS[mode] }}</span>
        <span v-if="modeOutlineCounts[mode]" class="mode-tab__count">{{ modeOutlineCounts[mode] }}</span>
      </button>
    </div>

    <!-- 批次子导航 + 工具栏 -->
    <div class="batch-nav">
      <div class="batch-nav__tabs">
        <button
          v-for="(outline, idx) in modeOutlines"
          :key="outline.id"
          type="button"
          :class="['batch-tab', { 'batch-tab--active': outline.id === selectedOutlineId }]"
          @click="selectOutline(outline.id)"
        >
          <span class="batch-tab__name">{{ outline.name || `批次 ${idx + 1}` }}</span>
          <span class="batch-tab__meta">{{ outline.chapterCount }} 章</span>
          <i v-if="outline.isDefault" class="i-lucide-star batch-tab__star" />
          <button
            v-if="!outline.isDefault"
            type="button"
            class="batch-tab__delete"
            title="删除此大纲"
            @click.stop="openOutlineDelete(outline)"
          >
            <i class="i-lucide-x" />
          </button>
        </button>
        <button
          type="button"
          class="batch-tab batch-tab--add"
          @click="openOutlineModal(selectedMode)"
        >
          <i class="i-lucide-plus" />
          <span>新建大纲</span>
        </button>
      </div>
      <div class="batch-nav__actions">
        <AppButton
          v-if="chapters.length > 0"
          variant="ghost"
          size="sm"
          :loading="reorderLoading"
          title="按当前顺序重排所有章节编号为 1..N"
          @click="reorderAll"
        >
          <i class="i-lucide-list-ordered" />
          重排编号
        </AppButton>
        <AppButton
          v-if="chapters.length > 0"
          variant="ghost"
          size="sm"
          title="导出已定稿章节为 md / txt"
          @click="openExportModal"
        >
          <i class="i-lucide-download" />
          导出
        </AppButton>
        <AppButton
          v-if="chapters.length > 0"
          variant="ghost"
          size="sm"
          :disabled="isBatchRunning"
          :title="isBatchRunning ? '当前已有进行中的批量任务' : '一次最多生成 10 章草稿'"
          @click="openBatchModal"
        >
          <i class="i-lucide-layers" />
          批量生成草稿
        </AppButton>
        <AppButton
          v-if="hasSelectedOutline"
          variant="ghost"
          size="sm"
          :title="planActionTitle"
          @click="openPlanModal"
        >
          <i class="i-lucide-sparkles" />
          {{ planActionLabel }}
        </AppButton>
        <AppButton
          v-if="modeOutlines.length > 0"
          variant="ghost"
          size="sm"
          title="AI 分析当前模式已有大纲，辅助规划下一段故事"
          @click="openChainPlanModal"
        >
          <i class="i-lucide-brain" />
          AI 续规建议
        </AppButton>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          添加章节
        </AppButton>
        <AppButton
          :variant="editMode ? 'primary' : 'ghost'"
          size="sm"
          @click="toggleEditMode"
        >
          <i :class="editMode ? 'i-lucide-check' : 'i-lucide-edit-3'" />
          {{ editMode ? '完成' : '编辑' }}
        </AppButton>
      </div>
    </div>

    <!-- 当前批次概览 -->
    <div v-if="selectedOutline" class="batch-info">
      <div class="batch-info__left">
        <span class="batch-info__name">{{ selectedOutline.name }}</span>
        <AppBadge size="sm" variant="default">{{ MODE_LABELS[selectedOutline.mode] }}</AppBadge>
      </div>
      <span v-if="selectedOutline.outlineSummary" class="batch-info__summary">{{ selectedOutline.outlineSummary }}</span>
      <span v-else-if="selectedOutline.branchTopic" class="batch-info__summary">{{ selectedOutline.branchTopic }}</span>
    </div>

    <!-- Agent 进度条：大纲规划 -->
    <div v-if="outlineStage && outlineStage.stage !== 'done'" class="agent-progress-bar">
      <div class="progress-indicator" :class="outlineStage.stage">
        <i v-if="outlineStage.stage === 'failed'" class="i-lucide-alert-circle" />
        <i v-else class="i-lucide-loader-2 spin" />
        <span>{{ stageLabels[outlineStage.stage] ?? outlineStage.stage }}</span>
        <span v-if="outlineStage.error" class="progress-error">{{ outlineStage.error }}</span>
      </div>
    </div>

    <!-- Agent 进度条：大纲调整 -->
    <div v-if="isAdjusting || (adjustStage && adjustStage.stage !== 'done')" class="agent-progress-bar agent-progress-bar--adjust">
      <div class="progress-indicator" :class="adjustStage?.stage ?? 'started'">
        <i v-if="adjustStage?.stage === 'failed'" class="i-lucide-alert-circle" />
        <i v-else class="i-lucide-loader-2 spin" />
        <span>{{ adjustStage ? (adjustStageLabels[adjustStage.stage] ?? 'AI 正在调整大纲…') : 'AI 正在调整大纲…' }}</span>
        <span v-if="adjustStage?.error" class="progress-error">{{ adjustStage.error }}</span>
      </div>
    </div>

    <!-- 持久化：待处理大纲提示横幅 -->
    <div v-if="pendingOutlineCount > 0 && !outlineStage" class="outline-pending-banner">
      <i class="i-lucide-sparkles banner-icon" />
      <span
        >有
        <strong>{{ pendingOutlineCount }}</strong>
        份待处理大纲草案，前往建议中心查看并导入章节</span
      >
      <AppButton size="sm" @click="router.push(`/projects/${projectId}/suggestions`)">
        前往查看
      </AppButton>
    </div>

    <!-- A3 批量生成草稿进度面板 -->
    <div v-if="activeBatchRun" class="batch-progress-panel" :data-status="activeBatchRun.status">
      <div class="batch-progress-header">
        <i v-if="isBatchRunning" class="i-lucide-loader-2 spin" />
        <i v-else-if="activeBatchRun.status === 'Completed'" class="i-lucide-check-circle-2" />
        <i v-else-if="activeBatchRun.status === 'Cancelled'" class="i-lucide-octagon-pause" />
        <i v-else class="i-lucide-alert-triangle" />
        <span class="batch-progress-title">
          批量生成草稿 · 第 {{ activeBatchRun.fromNumber }} – {{ activeBatchRun.toNumber }} 章
          <span class="batch-progress-status">
            {{ BATCH_STATUS_LABELS[activeBatchRun.status] }}
          </span>
        </span>
        <div class="batch-progress-actions">
          <AppButton
            v-if="isBatchRunning"
            size="sm"
            variant="ghost"
            :loading="cancelBatchLoading"
            @click="cancelBatch"
          >
            {{ activeBatchRun.cancelRequested ? '强制终止' : '中止' }}
          </AppButton>
          <AppButton v-else size="sm" variant="ghost" @click="activeBatchRun = null">
            关闭
          </AppButton>
        </div>
      </div>
      <div class="batch-progress-bar-wrap">
        <div class="batch-progress-bar" :style="{ width: batchProgressPercent + '%' }" />
      </div>
      <div class="batch-progress-stats">
        <span
          >已完成 <strong>{{ activeBatchRun.completedCount }}</strong></span
        >
        <span v-if="activeBatchRun.failedCount > 0" class="stat-failed">
          失败 <strong>{{ activeBatchRun.failedCount }}</strong>
        </span>
        <span v-if="activeBatchRun.skippedCount > 0">
          已跳过 <strong>{{ activeBatchRun.skippedCount }}</strong>
        </span>
        <span
          >共 <strong>{{ activeBatchRun.totalCount }}</strong> 章</span
        >
      </div>
      <p v-if="activeBatchRun.errorMessage" class="batch-progress-error">
        {{ activeBatchRun.errorMessage }}
      </p>
    </div>

    <!-- 编辑模式工具栏 -->
    <div v-if="editMode && chapters.length > 0" class="edit-toolbar">
      <label class="edit-toolbar__select-all">
        <input
          type="checkbox"
          :checked="allChaptersSelected"
          :indeterminate="selectedChapterIds.size > 0 && !allChaptersSelected"
          @change="toggleSelectAll"
        />
        全选（{{ selectedChapterIds.size }}/{{ chapters.length }}）
      </label>
      <div class="edit-toolbar__actions">
        <AppButton
          v-if="selectedChapterIds.size > 0"
          variant="ghost"
          size="sm"
          @click="openAdjustModal"
        >
          <i class="i-lucide-sparkles" />
          AI 调整（{{ selectedChapterIds.size }} 章）
        </AppButton>
        <AppButton
          v-if="selectedChapterIds.size > 0"
          variant="danger"
          size="sm"
          @click="batchChapterDeleteConfirmVisible = true"
        >
          <i class="i-lucide-trash-2" />
          删除选中（{{ selectedChapterIds.size }}）
        </AppButton>
      </div>
    </div>

    <!-- 骨架屏 -->
    <div v-if="loading" class="chapter-list">
      <div v-for="i in 4" :key="i" class="chapter-row skeleton-row">
        <AppSkeleton width="60px" height="14px" />
        <AppSkeleton width="200px" height="14px" />
        <AppSkeleton width="40%" height="14px" />
      </div>
    </div>

    <!-- 空状态 -->
    <AppEmpty
      v-else-if="!chapters.length"
      icon="i-lucide-book-text"
      :title="selectedOutline ? '当前大纲暂无章节' : '开始规划你的故事'"
      :description="selectedOutline ? '点击「AI 规划章节」生成首批章节，或手动添加' : '点击「规划大纲」新建大纲，可选择是否立即让 AI 生成章节计划'"
    >
      <template #action>
        <div class="empty-actions">
          <AppButton v-if="!selectedOutline" @click="openOutlineModal(selectedMode)">
            <i class="i-lucide-sparkles" />
            规划大纲
          </AppButton>
          <template v-else>
            <AppButton @click="openPlanModal">
              <i class="i-lucide-sparkles" />
              {{ planActionLabel }}
            </AppButton>
            <AppButton variant="ghost" @click="openCreate">
              <i class="i-lucide-plus" />
              手动添加章节
            </AppButton>
          </template>
        </div>
      </template>
    </AppEmpty>

    <!-- 章节列表 -->
    <div v-else class="chapter-list">
      <div
        v-for="chapter in chapters"
        :key="chapter.id"
        class="chapter-row"
        :class="{ 'chapter-row--selected': editMode && selectedChapterIds.has(chapter.id) }"
        @click="editMode ? toggleChapterSelect(chapter.id) : goDetail(chapter.id)"
      >
        <input
          v-if="editMode"
          type="checkbox"
          class="chapter-row__checkbox"
          :checked="selectedChapterIds.has(chapter.id)"
          @click.stop
          @change="toggleChapterSelect(chapter.id)"
        />
        <span class="chapter-num">第 {{ chapter.number }} 章</span>
        <span class="chapter-title">{{ chapter.title || '未命名' }}</span>
        <span class="chapter-summary">{{ chapter.summary || '—' }}</span>
        <AppBadge :variant="STATUS_VARIANTS[chapter.status] as any" size="sm">
          {{ STATUS_LABELS[chapter.status] }}
        </AppBadge>
        <button
          v-if="!editMode"
          class="row-delete-btn"
          title="删除章节"
          @click.stop="openDelete(chapter)"
        >
          <i class="i-lucide-trash-2" />
        </button>
      </div>
    </div>

    <!-- 添加章节抽屉 -->
    <AppDrawer v-model="drawerOpen" title="添加章节">
      <div class="form-fields">
        <AppInput
          v-model="createForm.number"
          label="章节编号 *"
          placeholder="如：1"
          type="number"
        />
        <AppInput v-model="createForm.title" label="章节标题" placeholder="如：序章·暗涌" />
        <AppTextarea
          v-model="createForm.summary"
          label="章节摘要"
          placeholder="简述本章发生的关键事件..."
          :rows="4"
        />
        <AppTextarea
          v-model="createForm.goal"
          label="章节目标"
          placeholder="本章希望达成的叙事目的..."
          :rows="3"
        />
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="drawerOpen = false">取消</AppButton>
        <AppButton :loading="createLoading" :disabled="!createForm.number" @click="submitCreate">
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 删除确认 -->
    <AppConfirm
      :model-value="!!deleteTarget"
      title="删除章节"
      :message="`确定删除第 ${deleteTarget?.number} 章《${deleteTarget?.title ?? '未命名'}》吗？`"
      variant="danger"
      confirm-text="删除"
      :loading="deleteLoading"
      @update:model-value="cancelDelete"
      @confirm="confirmDelete"
    />

    <!-- AI 大纲规划弹窗（继续规划：已有章节追加） -->
    <AppModal v-model="planModalOpen" :title="planModalTitle" width="560px">
      <div class="plan-form">
        <div v-if="selectedOutline" class="plan-outline-card">
          <div>
            <span class="plan-outline-label">目标大纲</span>
            <strong>{{ selectedOutline.name }}</strong>
          </div>
          <AppBadge size="sm" variant="default">
            {{ MODE_LABELS[selectedOutline.mode] }}
          </AppBadge>
        </div>

        <p class="plan-hint">
          {{ planHintText }}
        </p>

        <!-- 上下文统计 -->
        <div class="context-info">
          <span><i class="i-lucide-users" /> {{ characterCount }} 位角色</span>
          <span><i class="i-lucide-globe" /> {{ worldRuleCount }} 条世界观规则</span>
          <span v-if="!characterCount && !worldRuleCount" class="context-warn">
            建议先在「角色」「世界观」中添加设定，效果更好
          </span>
        </div>

        <AppTextarea
          v-model="planForm.goal"
          :label="planGoalLabel"
          :placeholder="planGoalPlaceholder"
          :rows="4"
        />

        <AppInput
          v-model="planForm.chapterCount"
          :label="planCountLabel"
          type="number"
          placeholder="10"
        />
        <p class="plan-count-tip">
          <i class="i-lucide-lightbulb" />
          建议每次规划 <strong>10～20 章</strong>，超过 30 章 AI 容易出现重复情节
        </p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="planModalOpen = false">取消</AppButton>
        <AppButton :loading="planLoading" :disabled="!planForm.goal.trim()" @click="submitPlan">
          <i class="i-lucide-sparkles" />
          {{ planSubmitLabel }}
        </AppButton>
      </template>
    </AppModal>

    <!-- 一键导出 Modal -->
    <AppModal v-model="exportModalOpen" title="导出章节" width="520px">
      <div class="export-form">
        <div class="export-section">
          <label class="export-label">导出格式</label>
          <div class="export-options">
            <button
              type="button"
              :class="['export-option', { active: exportForm.format === 'md' }]"
              @click="exportForm.format = 'md'"
            >
              <i class="i-lucide-file-text" /> Markdown (.md)
              <span class="option-desc">通用、保留章节层次</span>
            </button>
            <button
              type="button"
              :class="['export-option', { active: exportForm.format === 'txt' }]"
              @click="exportForm.format = 'txt'"
            >
              <i class="i-lucide-file" /> 纯文本 (.txt)
              <span class="option-desc">兼容 Kindle / 简单阅读器</span>
            </button>
          </div>
        </div>

        <div class="export-section">
          <label class="export-label">章节范围</label>
          <div class="export-options export-options--row">
            <button
              type="button"
              :class="['export-option', { active: exportForm.rangeMode === 'all' }]"
              @click="exportForm.rangeMode = 'all'"
            >
              全部章节
            </button>
            <button
              type="button"
              :class="['export-option', { active: exportForm.rangeMode === 'custom' }]"
              @click="exportForm.rangeMode = 'custom'"
            >
              自定义
            </button>
          </div>
          <div v-if="exportForm.rangeMode === 'custom'" class="range-inputs">
            <label class="range-input-wrap">
              <span>起始章号</span>
              <input
                v-model.number="exportForm.fromNumber"
                type="number"
                min="1"
                class="range-input"
              />
            </label>
            <label class="range-input-wrap">
              <span>结束章号</span>
              <input
                v-model.number="exportForm.toNumber"
                type="number"
                min="1"
                class="range-input"
              />
            </label>
          </div>
        </div>

        <div class="export-section">
          <label class="export-checkbox">
            <input v-model="exportForm.onlyFinal" type="checkbox" />
            <span>仅导出已定稿章节（推荐）</span>
          </label>
          <label v-if="!exportForm.onlyFinal" class="export-checkbox">
            <input v-model="exportForm.includeDraft" type="checkbox" />
            <span>未定稿时使用草稿（带 [草稿] 前缀）</span>
          </label>
        </div>

        <p class="export-tip">
          <i class="i-lucide-info" />
          当前共 <strong>{{ chapters.length }}</strong> 章，已定稿
          <strong>{{ finalChapterCount }}</strong> 章。
        </p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="exportModalOpen = false">取消</AppButton>
        <AppButton :loading="exportLoading" @click="submitExport">
          <i class="i-lucide-download" />
          下载
        </AppButton>
      </template>
    </AppModal>

    <!-- A3 批量生成草稿 Modal -->
    <AppModal v-model="batchModalOpen" title="批量生成章节草稿" width="480px">
      <div class="batch-form">
        <div class="batch-section">
          <label class="batch-label">章节范围</label>
          <div class="range-inputs">
            <label class="range-input-wrap">
              <span>起始章号</span>
              <input
                v-model.number="batchForm.fromNumber"
                type="number"
                min="1"
                class="range-input"
              />
            </label>
            <label class="range-input-wrap">
              <span>结束章号</span>
              <input
                v-model.number="batchForm.toNumber"
                type="number"
                min="1"
                class="range-input"
              />
            </label>
          </div>
          <p
            class="batch-tip"
            :class="{ 'batch-tip--error': batchSize > HARD_MAX_BATCH_DRAFT_SIZE }"
          >
            <i class="i-lucide-info" />
            将生成 <strong>{{ batchSize }}</strong> 章，单批最多
            <strong>{{ HARD_MAX_BATCH_DRAFT_SIZE }}</strong> 章。
          </p>
        </div>

        <div class="batch-section">
          <label class="export-checkbox">
            <input v-model="batchForm.autoFillPlan" type="checkbox" />
            <span>
              自动填充写作计划（推荐）
              <span class="batch-option-hint"
                >仅对无计划字段的章节生效，先填充再生草稿，质量更高</span
              >
            </span>
          </label>
        </div>

        <div class="batch-section">
          <label class="export-checkbox">
            <input v-model="batchForm.skipChaptersWithDraft" type="checkbox" />
            <span>跳过已有草稿的章节</span>
          </label>
        </div>

        <p class="batch-tip">
          <i class="i-lucide-clock" />
          顺序串行生成，单章上限 10 分钟、整批上限 45 分钟；可中途中止。
        </p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="batchModalOpen = false">取消</AppButton>
        <AppButton
          :loading="batchSubmitLoading"
          :disabled="batchSize <= 0 || batchSize > HARD_MAX_BATCH_DRAFT_SIZE"
          @click="submitBatch"
        >
          <i class="i-lucide-play" />
          开始生成
        </AppButton>
      </template>
    </AppModal>

    <!-- 新建大纲 Modal（主入口：创建壳 + 可选 AI 规划） -->
    <AppModal v-model="outlineModalOpen" title="新建大纲" width="560px">
      <div class="outline-form">
        <!-- 当前模式标签（只读展示，不可修改） -->
        <div class="outline-current-mode">
          <i :class="MODE_ICONS[outlineForm.mode]" class="outline-mode-icon" />
          <span class="outline-mode-name">{{ MODE_LABELS[outlineForm.mode] }}</span>
          <span class="outline-mode-tip">（由当前选中的模式 Tab 决定）</span>
        </div>

        <!-- 大纲名称 -->
        <AppInput
          v-model="outlineForm.name"
          label="大纲名称 *"
          placeholder="如：原创主线 / 番外：旧城雨夜"
        />

        <!-- 大纲说明（可选） -->
        <AppTextarea
          v-model="outlineForm.outlineSummary"
          label="故事概述（选填）"
          placeholder="这条故事线的核心主题、阶段目标，如：少年觉醒异能，历经三城争霸，揭开家族秘密..."
          :rows="3"
        />

        <!-- 番外时才显示的番外主题 -->
        <AppInput
          v-if="outlineForm.mode === 'SideStoryFromOriginal'"
          v-model="outlineForm.branchTopic"
          label="番外主题"
          placeholder="如：围绕配角少年时期展开十章支线"
        />

        <!-- 分隔线 -->
        <div class="outline-form__divider">
          <label class="outline-form__toggle-row">
            <input
              v-model="outlineForm.withAiPlan"
              type="checkbox"
              class="outline-form__checkbox"
            />
            <span class="outline-form__toggle-label">
              <i class="i-lucide-sparkles" />
              创建后立即 AI 规划章节
            </span>
            <span class="outline-form__toggle-hint">让 AI 根据你的描述生成完整章节计划</span>
          </label>
        </div>

        <!-- AI 规划展开区 -->
        <template v-if="outlineForm.withAiPlan">
          <!-- 上下文提示 -->
          <div class="context-info context-info--compact">
            <span><i class="i-lucide-users" /> {{ characterCount }} 位角色</span>
            <span><i class="i-lucide-globe" /> {{ worldRuleCount }} 条世界观规则</span>
            <span v-if="!characterCount && !worldRuleCount" class="context-warn">
              建议先在「角色」「世界观」中添加设定，AI 效果更好
            </span>
          </div>

          <AppTextarea
            v-model="outlineForm.aiGoal"
            label="故事目标 *"
            placeholder="描述你希望的故事发展方向，如：少年从山村出发，经历三城争霸，最终统一天下..."
            :rows="4"
          />

          <AppInput
            v-model="outlineForm.aiChapterCount"
            label="预计章节数"
            type="number"
            placeholder="10"
          />
          <p class="plan-count-tip">
            <i class="i-lucide-lightbulb" />
            建议设置 <strong>15～25 章</strong>；超过 30 章 AI 容易出现重复情节
          </p>
        </template>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="outlineModalOpen = false">取消</AppButton>
        <AppButton
          :loading="outlineSaving"
          :disabled="!outlineForm.name.trim() || (outlineForm.withAiPlan && !outlineForm.aiGoal.trim())"
          @click="submitOutline"
        >
          <i v-if="outlineForm.withAiPlan" class="i-lucide-sparkles" />
          {{ outlineForm.withAiPlan ? '创建并开始规划' : '创建大纲' }}
        </AppButton>
      </template>
    </AppModal>

    <!-- 删除大纲确认 -->
    <AppConfirm
      :model-value="!!outlineDeleteTarget"
      title="删除大纲"
      variant="danger"
      confirm-text="确认删除"
      :loading="outlineDeleteLoading"
      @update:model-value="cancelOutlineDelete"
      @confirm="confirmOutlineDelete"
    >
      <template #message>
        <p>是否要删除大纲《<strong>{{ outlineDeleteTarget?.name }}</strong>》？</p>
        <p class="confirm-sub-text">
          此操作将永久删除该大纲及其
          <strong>{{ outlineDeleteTarget?.chapterCount ?? 0 }}</strong>
          个章节、所有章节草稿、摘要、事件记录与 AI 建议，<strong>无法恢复</strong>。
        </p>
      </template>
    </AppConfirm>

    <!-- 批量章节删除确认 -->
    <AppConfirm
      v-model="batchChapterDeleteConfirmVisible"
      title="批量删除章节"
      :message="`将删除选中的 ${selectedChapterIds.size} 个章节，包含已有草稿与摘要数据，确认删除？`"
      variant="danger"
      confirm-text="确认删除"
      :loading="batchChapterDeleteLoading"
      @confirm="confirmBatchChapterDelete"
    />

    <!-- AI 续规建议 Modal -->
    <AppModal v-model="chainPlanModalOpen" title="AI 续规建议" width="560px">
      <div class="plan-form">
        <!-- 前驱大纲上下文 -->
        <div v-if="lastModeOutline" class="chain-context-card">
          <div class="chain-context-header">
            <i class="i-lucide-link" />
            <span>已有 <strong>{{ modeOutlines.length }}</strong> 个大纲，接续上一段规划</span>
          </div>
          <div class="chain-context-last">
            <span class="chain-context-label">上一段大纲：</span>
            <strong>{{ lastModeOutline.name }}</strong>
            <span class="chain-context-meta">{{ lastModeOutline.chapterCount }} 章</span>
          </div>
          <p v-if="lastModeOutline.outlineSummary" class="chain-context-summary">
            {{ lastModeOutline.outlineSummary }}
          </p>
        </div>

        <!-- 上下文统计 -->
        <div class="context-info">
          <span><i class="i-lucide-users" /> {{ characterCount }} 位角色</span>
          <span><i class="i-lucide-globe" /> {{ worldRuleCount }} 条世界观规则</span>
        </div>

        <AppTextarea
          v-model="chainPlanForm.goal"
          label="接下来的故事方向 *"
          placeholder="描述后续想让故事往哪里走，AI 会结合已有大纲上下文接续规划..."
          :rows="4"
        />

        <AppInput
          v-model="chainPlanForm.chapterCount"
          label="预计章节数"
          type="number"
          placeholder="15"
        />
        <p class="plan-count-tip">
          <i class="i-lucide-lightbulb" />
          建议设置 <strong>10～20 章</strong>；超过 30 章 AI 容易出现重复情节
        </p>

        <p class="plan-hint">
          <i class="i-lucide-info" />
          系统会自动创建一个新大纲并锁定模式为「{{ MODE_LABELS[selectedMode] }}」，AI 规划完成后请前往「建议中心」查看并导入章节。
        </p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="chainPlanModalOpen = false">取消</AppButton>
        <AppButton
          :loading="chainPlanLoading"
          :disabled="!chainPlanForm.goal.trim()"
          @click="submitChainPlan"
        >
          <i class="i-lucide-sparkles" />
          生成续篇规划
        </AppButton>
      </template>
    </AppModal>

    <!-- AI 大纲调整 Modal -->
    <AppModal v-model="adjustModalOpen" title="AI 调整大纲" width="560px">
      <div class="adjust-form">
        <div class="adjust-target-info">
          <i class="i-lucide-info" />
          <span>将对以下 <strong>{{ selectedChapterIds.size }}</strong> 个章节进行调整：</span>
          <div class="adjust-target-chapters">
            <span
              v-for="chapter in chapters.filter(c => selectedChapterIds.has(c.id))"
              :key="chapter.id"
              class="adjust-chapter-tag"
            >
              第 {{ chapter.number }} 章
            </span>
          </div>
        </div>

        <AppTextarea
          v-model="adjustForm.instruction"
          label="调整指令 *"
          placeholder="描述你的调整需求，例如：把这1章扩展为10章，重点铺垫主角与反派之间的矛盾冲突；或：把这5章铺垫剧情压缩为2章，去掉啰嗦部分"
          :rows="5"
        />

        <AppInput
          v-model="adjustForm.targetCount"
          label="期望调整后章节数（可选）"
          type="number"
          placeholder="展开时填写，如：10；合并时可不填"
        />

        <p class="adjust-hint">
          <i class="i-lucide-lightbulb" />
          AI 将只修改目标章节范围，保持前后文连贯。完成后章节列表自动刷新。
        </p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="adjustModalOpen = false">取消</AppButton>
        <AppButton
          :loading="adjustLoading"
          :disabled="!adjustForm.instruction.trim()"
          @click="submitAdjust"
        >
          <i class="i-lucide-sparkles" />
          提交调整
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
/* ── Mode Tabs ────────────────────────────────────────── */
.mode-tabs {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: var(--color-bg-elevated);
  border-radius: 12px;
  margin-bottom: 12px;
}

.mode-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  justify-content: center;
  padding: 8px 12px;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s;
  white-space: nowrap;
}

.mode-tab:hover {
  color: var(--color-text-primary);
  background: color-mix(in srgb, var(--color-bg-surface) 60%, transparent);
}

.mode-tab--active {
  background: var(--color-bg-surface);
  color: var(--color-primary);
  font-weight: 600;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
}

.mode-tab i {
  font-size: 15px;
  flex-shrink: 0;
}

.mode-tab__count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  border-radius: 9px;
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  font-size: 11px;
  font-weight: 600;
}

.mode-tab--active .mode-tab__count {
  background: var(--color-primary);
  color: #fff;
}

/* ── 批次子导航 + 工具栏 ──────────────────────────────── */
.batch-nav {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
  min-height: 40px;
}

.batch-nav__tabs {
  display: flex;
  gap: 6px;
  flex: 1;
  overflow-x: auto;
  scrollbar-width: thin;
}

.batch-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border: 1px solid var(--color-border);
  border-radius: 20px;
  background: var(--color-bg-surface);
  color: var(--color-text-secondary);
  font-size: 13px;
  cursor: pointer;
  white-space: nowrap;
  transition: all 0.15s;
}

.batch-tab:hover {
  border-color: var(--color-primary);
  color: var(--color-text-primary);
}

.batch-tab--active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  color: var(--color-primary);
  font-weight: 600;
}

.batch-tab__meta {
  font-size: 11px;
  color: var(--color-text-muted);
  font-weight: 400;
}

.batch-tab--active .batch-tab__meta {
  color: color-mix(in srgb, var(--color-primary) 60%, transparent);
}

.batch-tab__star {
  font-size: 11px;
  color: var(--color-warning, #f59e0b);
}

.batch-tab--add {
  border-style: dashed;
  color: var(--color-text-muted);
  font-weight: 400;
}

.batch-tab--add:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 5%, transparent);
}

.batch-nav__actions {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}

/* ── 批次概览 ─────────────────────────────────────────── */
.batch-info {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 14px;
  padding: 8px 14px;
  border-radius: 8px;
  background: var(--color-bg-elevated);
  font-size: 13px;
  color: var(--color-text-secondary);
}

.batch-info__left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.batch-info__name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.batch-info__summary {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-actions {
  display: flex;
  gap: 8px;
}

/* Agent 进度条 */
.agent-progress-bar {
  margin-bottom: 16px;
}

.agent-progress-bar--adjust .progress-indicator {
  background: color-mix(in srgb, var(--color-warning, #f59e0b) 10%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-warning, #f59e0b) 25%, transparent);
  color: color-mix(in srgb, var(--color-warning, #f59e0b) 80%, var(--color-text-primary));
}

.outline-pending-banner {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 16px;
  padding: 10px 16px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-accent) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-accent) 25%, transparent);
  font-size: 13px;
  color: var(--color-text-secondary);
}

.outline-pending-banner strong {
  color: var(--color-accent);
}

.banner-icon {
  color: var(--color-accent);
  flex-shrink: 0;
}

.outline-pending-banner > span {
  flex: 1;
}

.progress-indicator {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
}

.progress-indicator.started,
.progress-indicator.generating {
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-primary) 20%, transparent);
  color: var(--color-primary);
}

.progress-indicator.failed {
  background: color-mix(in srgb, var(--color-danger) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-danger) 20%, transparent);
  color: var(--color-danger);
}

.progress-error {
  font-weight: 400;
  margin-left: 4px;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.spin {
  animation: spin 1s linear infinite;
}

.chapter-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.chapter-row {
  display: grid;
  grid-template-columns: 80px 180px 1fr auto 40px;
  align-items: center;
  gap: 16px;
  padding: 12px 16px;
  border-radius: 8px;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  cursor: pointer;
  transition: border-color 0.15s;
}

.chapter-row:hover {
  border-color: var(--color-primary);
}

.chapter-num {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-primary);
  white-space: nowrap;
}

.chapter-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chapter-summary {
  font-size: 13px;
  color: var(--color-text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.skeleton-row {
  height: 48px;
  background-color: var(--color-bg-surface);
}

.row-delete-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 15px;
  opacity: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.chapter-row:hover .row-delete-btn {
  opacity: 1;
}

.row-delete-btn:hover {
  background-color: color-mix(in srgb, var(--color-danger) 12%, transparent);
  color: var(--color-danger);
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 规划弹窗 */
.plan-form {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.plan-mode-row {
  display: flex;
  gap: 8px;
}

.plan-mode-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 18px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: transparent;
  color: var(--color-text-muted);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s;
  flex: 1;
  justify-content: center;
}

.plan-mode-btn:hover:not(:disabled) {
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.plan-mode-btn.active {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  border-color: var(--color-primary);
  color: var(--color-primary);
  font-weight: 500;
}

.plan-mode-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.plan-hint {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.5;
}

.plan-count-tip {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin: -6px 0 0;
  line-height: 1.5;
}

.plan-count-tip i {
  font-size: 13px;
  color: var(--color-warning, #f59e0b);
  flex-shrink: 0;
}

.plan-count-tip strong {
  color: var(--color-text-secondary);
}

.plan-outline-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-elevated);
}

.plan-outline-card > div {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.plan-outline-label {
  font-size: 12px;
  color: var(--color-text-muted);
}

.outline-form {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.outline-form__section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.outline-form__label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.outline-mode-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
}

.outline-mode-option {
  height: 36px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-elevated);
  color: var(--color-text-secondary);
  cursor: pointer;
}

.outline-mode-option.active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  color: var(--color-primary);
  font-weight: 600;
}

/* AI 规划开关分隔线 */
.outline-form__divider {
  border-top: 1px solid var(--color-border);
  padding-top: 14px;
  margin-top: 2px;
}

.outline-form__toggle-row {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  user-select: none;
}

.outline-form__checkbox {
  width: 15px;
  height: 15px;
  flex-shrink: 0;
  accent-color: var(--color-primary);
  cursor: pointer;
}

.outline-form__toggle-label {
  display: flex;
  align-items: center;
  gap: 5px;
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.outline-form__toggle-label i {
  color: var(--color-primary);
}

.outline-form__toggle-hint {
  font-size: 12px;
  color: var(--color-text-muted);
  margin-left: auto;
}

.context-info {
  display: flex;
  gap: 14px;
  font-size: 12px;
  color: var(--color-text-muted);
  padding: 8px 12px;
  background: var(--color-bg-elevated);
  border-radius: 6px;
}

.context-info--compact {
  padding: 6px 10px;
  flex-wrap: wrap;
  gap: 10px;
}

.context-info i {
  margin-right: 3px;
}

.context-warn {
  color: var(--color-accent);
  font-style: italic;
}

/* ── 导出 Modal ───────────────────────────────────── */
.export-form {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.export-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.export-label {
  font-size: 13px;
  color: var(--color-text-muted);
  font-weight: 500;
}

.export-options {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.export-options--row {
  grid-template-columns: 1fr 1fr;
}

.export-option {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
  cursor: pointer;
  font-size: 13px;
  transition: all 0.15s;
  text-align: left;
}

.export-option:hover {
  border-color: var(--color-primary);
}

.export-option.active {
  border-color: var(--color-primary);
  background: var(--color-primary-soft, rgba(59, 130, 246, 0.08));
  color: var(--color-primary);
}

.option-desc {
  font-size: 11px;
  color: var(--color-text-muted);
}

.range-inputs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.range-input-wrap {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.range-input {
  height: 32px;
  padding: 0 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
  font-size: 13px;
  outline: none;
}

.range-input:focus {
  border-color: var(--color-primary);
}

.export-checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  cursor: pointer;
  color: var(--color-text-primary);
  margin-top: 4px;
}

.export-tip {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

/* A3 批量生成草稿 */
.batch-form {
  display: flex;
  flex-direction: column;
  gap: 18px;
}
.batch-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.batch-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
}
.batch-tip {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}
.batch-tip--error {
  color: var(--color-danger, #d63838);
}
.batch-option-hint {
  display: block;
  font-size: 11px;
  color: var(--color-text-muted);
  margin-top: 2px;
  font-weight: 400;
}

.batch-progress-panel {
  margin-bottom: 16px;
  padding: 14px 16px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-elev);
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.batch-progress-panel[data-status='Failed'],
.batch-progress-panel[data-status='PartiallyFailed'] {
  border-color: var(--color-warning, #d97706);
}
.batch-progress-panel[data-status='Completed'] {
  border-color: var(--color-success, #16a34a);
}
.batch-progress-header {
  display: flex;
  align-items: center;
  gap: 8px;
}
.batch-progress-title {
  flex: 1;
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}
.batch-progress-status {
  margin-left: 8px;
  font-size: 12px;
  font-weight: 400;
  color: var(--color-text-muted);
}
.batch-progress-actions {
  display: flex;
  gap: 6px;
}
.batch-progress-bar-wrap {
  height: 6px;
  border-radius: 3px;
  background: var(--color-bg-muted, rgba(0, 0, 0, 0.08));
  overflow: hidden;
}
.batch-progress-bar {
  height: 100%;
  background: var(--color-primary);
  transition: width 0.4s ease;
}
.batch-progress-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  font-size: 12px;
  color: var(--color-text-secondary);
}
.batch-progress-stats strong {
  color: var(--color-text-primary);
}
.batch-progress-stats .stat-failed {
  color: var(--color-danger, #d63838);
}
.batch-progress-stats .stat-failed strong {
  color: var(--color-danger, #d63838);
}
.batch-progress-error {
  margin: 0;
  font-size: 12px;
  color: var(--color-danger, #d63838);
}

/* ── 大纲 Modal: 当前模式只读展示 ────────────────────── */
.outline-current-mode {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-primary) 20%, transparent);
  margin-bottom: 4px;
}

.outline-mode-icon {
  font-size: 16px;
  color: var(--color-primary);
  flex-shrink: 0;
}

.outline-mode-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-primary);
}

.outline-mode-tip {
  font-size: 12px;
  color: var(--color-text-muted);
  margin-left: 4px;
}

/* ── AI 续规建议: 前驱大纲上下文卡片 ─────────────────── */
.chain-context-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px 14px;
  border-radius: 8px;
  background: var(--color-bg-elevated);
  border-left: 3px solid var(--color-primary);
  margin-bottom: 4px;
}

.chain-context-header {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--color-text-secondary);
}

.chain-context-last {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.chain-context-label {
  color: var(--color-text-muted);
  flex-shrink: 0;
}

.chain-context-meta {
  font-size: 12px;
  color: var(--color-text-muted);
  margin-left: auto;
}

.chain-context-summary {
  font-size: 12px;
  color: var(--color-text-muted);
  line-height: 1.6;
  margin: 0;
  border-top: 1px solid var(--color-border);
  padding-top: 8px;
}

/* ── 大纲 Tab 删除按钮 ────────────────────────────────── */
.batch-tab__delete {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border: none;
  background: none;
  cursor: pointer;
  border-radius: 4px;
  color: var(--color-text-muted);
  font-size: 12px;
  padding: 0;
  margin-left: 2px;
  opacity: 0;
  transition: opacity 0.15s, color 0.15s, background 0.15s;
}

.batch-tab:hover .batch-tab__delete,
.batch-tab--active .batch-tab__delete {
  opacity: 1;
}

.batch-tab__delete:hover {
  color: var(--color-danger, #d63838);
  background: color-mix(in srgb, var(--color-danger, #d63838) 12%, transparent);
}

/* ── 编辑模式工具栏 ────────────────────────────────────── */
.edit-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 14px;
  margin-bottom: 8px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-primary) 6%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-primary) 20%, transparent);
}

.edit-toolbar__select-all {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
  cursor: pointer;
  user-select: none;
}

.edit-toolbar__actions {
  display: flex;
  gap: 8px;
}

/* ── 章节行勾选状态 ────────────────────────────────────── */
.chapter-row__checkbox {
  flex-shrink: 0;
  cursor: pointer;
  width: 16px;
  height: 16px;
}

.chapter-row--selected {
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  border-color: color-mix(in srgb, var(--color-primary) 30%, transparent);
}

/* ── 删除大纲确认弹窗辅助文字 ────────────────────────── */
.confirm-sub-text {
  margin-top: 8px;
  font-size: 13px;
  color: var(--color-text-muted);
  line-height: 1.6;
}

.confirm-sub-text strong {
  color: var(--color-danger, #d63838);
}

/* ── AI 大纲调整 Modal ──────────────────────────────── */
.adjust-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.adjust-target-info {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 10px 14px;
  border-radius: 8px;
  background: var(--color-bg-elevated);
  font-size: 13px;
  color: var(--color-text-secondary);
}

.adjust-target-chapters {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 4px;
}

.adjust-chapter-tag {
  padding: 2px 8px;
  border-radius: 12px;
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  font-size: 12px;
  font-weight: 500;
}

.adjust-hint {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.5;
}
</style>
