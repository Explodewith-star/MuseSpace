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
import { getCharacters } from '@/api/characters'
import { getWorldRules } from '@/api/worldRules'
import { useAgentProgress } from '@/composables/useAgentProgress'
import { useToast } from '@/composables/useToast'

const router = useRouter()
const route = useRoute()
const toast = useToast()
const projectId = route.params.id as string

const {
  chapters,
  loading,
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
} = initChaptersState()

const STATUS_VARIANTS: Record<number, string> = { 0: 'muted', 1: 'accent', 2: 'primary', 3: 'success' }
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
    const [chars, rules] = await Promise.all([
      getCharacters(projectId),
      getWorldRules(projectId),
    ])
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
  planForm.chapterCount = '10'
  loadContextStats()
  planModalOpen.value = true
}

async function submitPlan() {
  if (!planForm.goal.trim()) return
  planLoading.value = true
  try {
    await triggerOutlinePlan(projectId, {
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

// ── SignalR 进度订阅 ──────────────────────────────────────────
const { latestEvent, joinProject, leaveProject } = useAgentProgress()

const outlineStage = computed(() => {
  const e = latestEvent.value
  if (!e || e.taskType !== 'outline') return null
  return e
})

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
    const list = await getSuggestions(projectId, { category: 'Outline', status: 'Pending' })
    pendingOutlineCount.value = list.length
  } catch {
    // ignore
  }
}

onMounted(() => {
  joinProject(projectId)
  refreshPendingOutline()
})
onUnmounted(() => leaveProject(projectId))

// 大纲生成完成后提示跳转
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
      <span>有 <strong>{{ pendingOutlineCount }}</strong> 份待处理大纲草案，前往建议中心查看并导入章节</span>
      <AppButton
        size="sm"
        @click="router.push(`/projects/${projectId}/outline`)"
      >
        前往查看
      </AppButton>
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
        <AppBadge :variant="(STATUS_VARIANTS[chapter.status] as any)" size="sm">
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
        <AppButton
          :loading="createLoading"
          :disabled="!createForm.number"
          @click="submitCreate"
        >
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
        <!-- 模式选择 -->
        <div class="plan-mode-row">
          <button
            :class="['plan-mode-btn', { active: planForm.mode === 'new' }]"
            @click="planForm.mode = 'new'"
          >
            <i class="i-lucide-file-plus" />
            全新规划
          </button>
          <button
            :class="['plan-mode-btn', { active: planForm.mode === 'continue' }]"
            :disabled="!chapters.length"
            @click="planForm.mode = 'continue'"
          >
            <i class="i-lucide-arrow-right-from-line" />
            续写扩展
          </button>
          <button
            :class="['plan-mode-btn', { active: planForm.mode === 'extra' }]"
            :disabled="!chapters.length"
            @click="planForm.mode = 'extra'"
          >
            <i class="i-lucide-sparkles" />
            番外/支线
          </button>
        </div>

        <p v-if="planForm.mode === 'continue' && chapters.length" class="plan-hint">
          将基于已有 <strong>{{ chapters.length }}</strong> 章内容续写，新章节从第 {{ chapters.length + 1 }} 章开始。
        </p>
        <p v-else-if="planForm.mode === 'extra' && chapters.length" class="plan-hint">
          将生成番外/支线卷，不影响主线，章号从第 {{ chapters.length + 1 }} 章开始。
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
        <AppButton
          :loading="planLoading"
          :disabled="!planForm.goal.trim()"
          @click="submitPlan"
        >
          <i class="i-lucide-sparkles" />
          开始规划
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
  to { transform: rotate(360deg); }
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
</style>

