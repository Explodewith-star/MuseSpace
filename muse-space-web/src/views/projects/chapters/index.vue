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
import { createStoryOutline, getStoryOutlines } from '@/api/outlines'
import { getCharacters } from '@/api/characters'
import { getWorldRules } from '@/api/worldRules'
import { exportProjectChapters, type ExportFormat } from '@/api/export'
import {
  batchGenerateDrafts,
  cancelChapterBatchRun,
  getChapterBatchRun,
  listChapterBatchRuns,
  DEFAULT_BATCH_DRAFT_SIZE,
  HARD_MAX_BATCH_DRAFT_SIZE,
  type ChapterBatchDraftRunResponse,
} from '@/api/chapters'
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

const MODE_LABELS: Record<GenerationMode, string> = {
  Original: '原创主线',
  ContinueFromOriginal: '原著续写',
  SideStoryFromOriginal: '支线番外',
  ExpandOrRewrite: '扩写/改写',
}

async function loadOutlines() {
  outlinesLoading.value = true
  try {
    outlines.value = await getStoryOutlines(projectId)
    if (!selectedOutlineId.value || !outlines.value.some((o) => o.id === selectedOutlineId.value)) {
      selectedOutlineId.value = outlines.value.find((o) => o.isDefault)?.id ?? outlines.value[0]?.id ?? ''
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
})

function openOutlineModal(mode: GenerationMode = 'Original') {
  Object.assign(outlineForm, {
    name: '',
    mode,
    outlineSummary: '',
    branchTopic: '',
  })
  outlineModalOpen.value = true
}

async function submitOutline() {
  if (!outlineForm.name.trim()) return
  outlineSaving.value = true
  try {
    const outline = await createStoryOutline(projectId, {
      name: outlineForm.name.trim(),
      mode: outlineForm.mode,
      outlineSummary: outlineForm.outlineSummary.trim() || undefined,
      branchTopic: outlineForm.branchTopic.trim() || undefined,
    })
    outlines.value.push(outline)
    selectedOutlineId.value = outline.id
    outlineModalOpen.value = false
    toast.success('大纲已创建')
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
  if (!id) return
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

function openPlanModal() {
  // 根据当前章节数自动选模式
  planForm.mode = chapters.value.length > 0 ? 'continue' : 'new'
  planForm.goal = ''
  planForm.chapterCount = selectedOutline.value?.targetChapterCount
    ? String(selectedOutline.value.targetChapterCount)
    : '10'
  loadContextStats()
  planModalOpen.value = true
}

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
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">章节管理</h2>
      <div class="header-actions">
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
        <AppButton variant="ghost" size="sm" @click="openPlanModal">
          <i class="i-lucide-sparkles" />
          AI 规划大纲
        </AppButton>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          添加章节
        </AppButton>
      </div>
    </div>

    <div class="outline-switcher">
      <div class="outline-tabs" v-if="outlines.length">
        <button
          v-for="outline in outlines"
          :key="outline.id"
          type="button"
          :class="['outline-tab', { active: outline.id === selectedOutlineId }]"
          @click="selectOutline(outline.id)"
        >
          <span class="outline-tab__name">{{ outline.name }}</span>
          <span class="outline-tab__meta">
            {{ MODE_LABELS[outline.mode] }} · {{ outline.chapterCount }} 章
          </span>
        </button>
      </div>
      <div v-else-if="outlinesLoading" class="outline-loading">正在加载大纲...</div>
      <AppButton variant="ghost" size="sm" @click="openOutlineModal()">
        <i class="i-lucide-folder-plus" />
        新建大纲
      </AppButton>
    </div>

    <div v-if="selectedOutline" class="outline-current">
      <i class="i-lucide-git-branch" />
      <span>{{ selectedOutline.name }}</span>
      <strong>{{ MODE_LABELS[selectedOutline.mode] }}</strong>
      <span v-if="selectedOutline.outlineSummary">{{ selectedOutline.outlineSummary }}</span>
      <span v-else-if="selectedOutline.branchTopic">{{ selectedOutline.branchTopic }}</span>
    </div>

    <!-- Agent 进度条 -->
    <div v-if="outlineStage && outlineStage.stage !== 'done'" class="agent-progress-bar">
      <div class="progress-indicator" :class="outlineStage.stage">
        <i v-if="outlineStage.stage === 'failed'" class="i-lucide-alert-circle" />
        <i v-else class="i-lucide-loader-2 spin" />
        <span>{{ stageLabels[outlineStage.stage] ?? outlineStage.stage }}</span>
        <span v-if="outlineStage.error" class="progress-error">{{ outlineStage.error }}</span>
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
      <AppButton size="sm" @click="router.push(`/projects/${projectId}/outline`)">
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
      title="还没有章节"
      description="手动添加章节，或让 AI 帮你规划大纲"
    >
      <template #action>
        <div class="empty-actions">
          <AppButton variant="ghost" @click="openPlanModal">
            <i class="i-lucide-sparkles" />
            AI 规划大纲
          </AppButton>
          <AppButton @click="openCreate">
            <i class="i-lucide-plus" />
            手动添加
          </AppButton>
        </div>
      </template>
    </AppEmpty>

    <!-- 章节列表 -->
    <div v-else class="chapter-list">
      <div
        v-for="chapter in chapters"
        :key="chapter.id"
        class="chapter-row"
        @click="goDetail(chapter.id)"
      >
        <span class="chapter-num">第 {{ chapter.number }} 章</span>
        <span class="chapter-title">{{ chapter.title || '未命名' }}</span>
        <span class="chapter-summary">{{ chapter.summary || '—' }}</span>
        <AppBadge :variant="STATUS_VARIANTS[chapter.status] as any" size="sm">
          {{ STATUS_LABELS[chapter.status] }}
        </AppBadge>
        <button class="row-delete-btn" title="删除章节" @click.stop="openDelete(chapter)">
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

    <!-- AI 大纲规划弹窗 -->
    <AppModal v-model="planModalOpen" title="AI 规划大纲" width="560px">
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

        <p v-if="chapters.length" class="plan-hint">
          当前大纲已有 <strong>{{ chapters.length }}</strong> 章，新规划会按该大纲继续编号。
        </p>

        <!-- 上下文提示 -->
        <div class="context-info">
          <span><i class="i-lucide-users" /> {{ characterCount }} 位角色</span>
          <span><i class="i-lucide-globe" /> {{ worldRuleCount }} 条世界观规则</span>
          <span v-if="!characterCount && !worldRuleCount" class="context-warn">
            建议先添加角色和世界观规则，效果更好
          </span>
        </div>

        <AppTextarea
          v-model="planForm.goal"
          label="故事目标 *"
          placeholder="描述你希望的故事发展方向，如：少年从山村出发，经历三国争霸，最终统一天下..."
          :rows="4"
        />

        <AppInput
          v-model="planForm.chapterCount"
          label="预计章节数"
          type="number"
          placeholder="10"
        />
        <p class="plan-count-tip">
          <i class="i-lucide-lightbulb" />
          建议设置 <strong>15～25 章</strong>；少于 10 章大纲较粗，超过 30 章 AI 容易生成重复内容
        </p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="planModalOpen = false">取消</AppButton>
        <AppButton :loading="planLoading" :disabled="!planForm.goal.trim()" @click="submitPlan">
          <i class="i-lucide-sparkles" />
          开始规划
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

    <AppModal v-model="outlineModalOpen" title="新建故事大纲" width="540px">
      <div class="outline-form">
        <div class="outline-mode-grid">
          <button
            v-for="mode in (['Original', 'ContinueFromOriginal', 'SideStoryFromOriginal', 'ExpandOrRewrite'] as GenerationMode[])"
            :key="mode"
            type="button"
            :class="['outline-mode-option', { active: outlineForm.mode === mode }]"
            @click="outlineForm.mode = mode"
          >
            {{ MODE_LABELS[mode] }}
          </button>
        </div>
        <AppInput v-model="outlineForm.name" label="大纲名称 *" placeholder="如：原创主线 / 番外：旧城雨夜" />
        <AppTextarea
          v-model="outlineForm.outlineSummary"
          label="大纲说明"
          placeholder="这条故事线的核心方向、阶段目标或边界..."
          :rows="3"
        />
        <AppInput
          v-if="outlineForm.mode === 'SideStoryFromOriginal'"
          v-model="outlineForm.branchTopic"
          label="番外主题"
          placeholder="如：围绕配角少年时期展开十章支线"
        />
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="outlineModalOpen = false">取消</AppButton>
        <AppButton :loading="outlineSaving" :disabled="!outlineForm.name.trim()" @click="submitOutline">
          创建
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.page__title {
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 8px;
}

.outline-switcher {
  display: flex;
  align-items: stretch;
  gap: 10px;
  margin-bottom: 12px;
}

.outline-tabs {
  display: flex;
  flex: 1;
  gap: 8px;
  overflow-x: auto;
  padding-bottom: 2px;
}

.outline-tab {
  min-width: 150px;
  max-width: 220px;
  padding: 9px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  color: var(--color-text-secondary);
  text-align: left;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.outline-tab.active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  color: var(--color-primary);
}

.outline-tab__name {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.outline-tab__meta,
.outline-loading {
  font-size: 12px;
  color: var(--color-text-muted);
}

.outline-current {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  color: var(--color-text-secondary);
  background: var(--color-bg-elevated);
  font-size: 13px;
}

.outline-current strong {
  color: var(--color-primary);
}

.empty-actions {
  display: flex;
  gap: 8px;
}

/* Agent 进度条 */
.agent-progress-bar {
  margin-bottom: 16px;
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

.context-info {
  display: flex;
  gap: 14px;
  font-size: 12px;
  color: var(--color-text-muted);
  padding: 8px 12px;
  background: var(--color-bg-elevated);
  border-radius: 6px;
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
</style>
