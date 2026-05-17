<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, reactive, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppSelect from '@/components/base/AppSelect.vue'
import { getChapters, deleteChapter, batchDeleteChapters } from '@/api/chapters'
import { getStoryOutlines } from '@/api/outlines'
import {
  getSuggestions,
  triggerOutlinePlan,
  ignoreSuggestion,
  deleteSuggestion,
  batchResolveSuggestions,
} from '@/api/suggestions'
import { getCharacters } from '@/api/characters'
import { getWorldRules } from '@/api/worldRules'
import { useToast } from '@/composables/useToast'
import { useAgentProgress } from '@/composables/useAgentProgress'
import { parseOutlineVolumes, parseOutlineChapters } from '../suggestions/utils'
import type { ChapterResponse, AgentSuggestionResponse, GenerationMode, StoryOutlineResponse } from '@/types/models'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const projectId = route.params.id as string
const { latestEvent, joinProject, stop } = useAgentProgress()

// ── 数据 ──────────────────────────────────────────────
const chaptersLoading = ref(true)
const suggestionsLoading = ref(true)
const chapters = ref<ChapterResponse[]>([])
const outlineSuggestions = ref<AgentSuggestionResponse[]>([])
const storyOutlines = ref<StoryOutlineResponse[]>([])
const selectedOutlineId = ref('')
const selectedOutline = computed(() =>
  storyOutlines.value.find((o) => o.id === selectedOutlineId.value) ?? null,
)

function outlineModeToPlanMode(mode?: GenerationMode): 'new' | 'continue' | 'extra' {
  if (mode === 'SideStoryFromOriginal') return 'extra'
  if (mode === 'ContinueFromOriginal' || mode === 'ExpandOrRewrite') return 'continue'
  return chapters.value.length > 0 ? 'continue' : 'new'
}

async function loadStoryOutlines() {
  storyOutlines.value = await getStoryOutlines(projectId)
  if (!selectedOutlineId.value || !storyOutlines.value.some((o) => o.id === selectedOutlineId.value)) {
    selectedOutlineId.value = storyOutlines.value.find((o) => o.isDefault)?.id ?? storyOutlines.value[0]?.id ?? ''
  }
}

async function loadChapters() {
  chaptersLoading.value = true
  try { chapters.value = await getChapters(projectId, selectedOutlineId.value || undefined) } catch { /* */ }
  finally { chaptersLoading.value = false }
}

async function loadSuggestions() {
  suggestionsLoading.value = true
  try {
    const list = await getSuggestions(projectId, { category: 'Outline' })
    outlineSuggestions.value = list.filter((s) =>
      !selectedOutlineId.value || !s.targetEntityId || s.targetEntityId === selectedOutlineId.value)
  } catch { /* */ }
  finally { suggestionsLoading.value = false }
}

async function loadAll() {
  await loadStoryOutlines()
  await Promise.all([loadChapters(), loadSuggestions()])
}

// ── 实际章节按来源分组 ────────────────────────────────
interface ChapterGroup {
  label: string
  badge: string
  chapters: ChapterResponse[]
}

const chapterGroups = computed<ChapterGroup[]>(() => {
  const bySource = new Map<string, ChapterResponse[]>()
  for (const ch of chapters.value) {
    const key = ch.sourceSuggestionId ?? '_manual_'
    if (!bySource.has(key)) bySource.set(key, [])
    bySource.get(key)!.push(ch)
  }
  const groups: ChapterGroup[] = []
  // 按大纲建议分组
  for (const s of outlineSuggestions.value) {
    const chs = bySource.get(s.id) ?? []
    if (s.status === 'Applied' && chs.length > 0) {
      groups.push({
        label: s.title,
        badge: `${chs.length} 章`,
        chapters: chs,
      })
      bySource.delete(s.id)
    }
  }
  // 手动添加 or 来源找不到的
  const manual = bySource.get('_manual_') ?? []
  // 还有 sourceSuggestionId 不在当前建议列表里的
  for (const [key, chs] of bySource) {
    if (key !== '_manual_') manual.push(...chs)
  }
  if (manual.length > 0) {
    manual.sort((a, b) => a.number - b.number)
    groups.push({ label: '手动添加的章节', badge: `${manual.length} 章`, chapters: manual })
  }
  return groups
})

const totalChapters = computed(() => chapters.value.length)

// ── AI 大纲规划弹窗 ──────────────────────────────────
const planModalOpen = ref(false)
const planForm = reactive({ goal: '', chapterCount: '10', mode: 'new' as 'new' | 'continue' | 'extra' })
const planLoading = ref(false)
const characterCount = ref(0)
const worldRuleCount = ref(0)

async function loadContextStats() {
  try {
    const outlineId = selectedOutlineId.value
    const [chars, rules] = await Promise.all([
      outlineId ? getCharacters(projectId, outlineId) : Promise.resolve([]),
      getWorldRules(projectId),
    ])
    characterCount.value = chars.length
    worldRuleCount.value = rules.length
  } catch { /* */ }
}

function openPlanModal() {
  planForm.mode = outlineModeToPlanMode(selectedOutline.value?.mode)
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
      storyOutlineId: selectedOutlineId.value || undefined,
      goal: planForm.goal,
      chapterCount: Number(planForm.chapterCount),
      mode: planForm.mode,
    })
    planModalOpen.value = false
    toast.success('大纲规划已提交，请稍候...')
  } catch { /* */ }
  finally { planLoading.value = false }
}

// ── 章节状态 ──────────────────────────────────────────
const STATUS_LABELS: Record<number, string> = { 0: '计划中', 1: '草稿中', 2: '修改中', 3: '已定稿' }
const STATUS_VARIANTS: Record<number, string> = { 0: 'muted', 1: 'accent', 2: 'primary', 3: 'success' }
const SUGGESTION_STATUS_LABELS: Record<string, string> = { Pending: '待处理', Accepted: '已接受应用', Applied: '已接受应用', Ignored: '已忽略' }
const SUGGESTION_STATUS_VARIANTS: Record<string, string> = { Pending: 'accent', Accepted: 'success', Applied: 'success', Ignored: 'muted' }

function goOutlineDetail(s: AgentSuggestionResponse) {
  router.push(`/projects/${projectId}/suggestions/outline/${s.id}`)
}

function goChapterDetail(ch: ChapterResponse) {
  router.push(`/projects/${projectId}/chapters/${ch.id}`)
}

// ── 展开/折叠 ────────────────────────────────────────
const expandedGroups = ref<Set<number>>(new Set([0]))

function toggleGroup(index: number) {
  if (expandedGroups.value.has(index)) expandedGroups.value.delete(index)
  else expandedGroups.value.add(index)
}

// ── 大纲删除（单条 / 全部） ─────────────────────────
const deleteLoading = ref(false)
const deleteModalOpen = ref(false)
const deleteScope = ref<'single' | 'all'>('single')
const deleteTargetId = ref('')
const deleteTargetApplied = ref(false)

function openDeleteConfirm(scope: 'single' | 'all', id?: string) {
  deleteScope.value = scope
  deleteTargetId.value = id ?? ''
  if (scope === 'single') {
    const s = outlineSuggestions.value.find(x => x.id === id)
    deleteTargetApplied.value = s?.status === 'Applied'
  } else {
    deleteTargetApplied.value = outlineSuggestions.value.some(s => s.status === 'Applied')
  }
  deleteModalOpen.value = true
}

async function confirmDelete() {
  deleteModalOpen.value = false
  deleteLoading.value = true
  try {
    if (deleteScope.value === 'single') {
      const s = outlineSuggestions.value.find(x => x.id === deleteTargetId.value)
      if (s?.status === 'Applied' || s?.status === 'Pending') {
        await ignoreSuggestion(projectId, deleteTargetId.value)
      }
      await deleteSuggestion(projectId, deleteTargetId.value)
      toast.success('大纲及关联章节已删除')
    } else {
      const allIds = outlineSuggestions.value.map(s => s.id)
      const needIgnore = outlineSuggestions.value
        .filter(s => s.status === 'Applied' || s.status === 'Pending')
        .map(s => s.id)
      if (needIgnore.length) {
        await batchResolveSuggestions(projectId, { ids: needIgnore, action: 'Ignore' })
      }
      await batchResolveSuggestions(projectId, { ids: allIds, action: 'Delete' })
      toast.success(`已删除全部 ${allIds.length} 条大纲及其关联章节`)
    }
    await loadAll()
  } catch { /* */ }
  finally { deleteLoading.value = false }
}

// ── 章节删除（单个 / 整组 / 全部） ──────────────────
const chDeleteLoading = ref(false)
const chDeleteModalOpen = ref(false)
const chDeleteScope = ref<'single' | 'group' | 'all'>('single')
const chDeleteTargetIds = ref<string[]>([])
const chDeleteLabel = ref('')

function openChDeleteSingle(ch: ChapterResponse) {
  chDeleteScope.value = 'single'
  chDeleteTargetIds.value = [ch.id]
  chDeleteLabel.value = `第 ${ch.number} 章「${ch.title || '未命名'}」`
  chDeleteModalOpen.value = true
}

function openChDeleteGroup(group: ChapterGroup) {
  chDeleteScope.value = 'group'
  chDeleteTargetIds.value = group.chapters.map(c => c.id)
  chDeleteLabel.value = `「${group.label}」（共 ${group.chapters.length} 章）`
  chDeleteModalOpen.value = true
}

function openChDeleteAll() {
  chDeleteScope.value = 'all'
  chDeleteTargetIds.value = chapters.value.map(c => c.id)
  chDeleteLabel.value = `全部 ${chapters.value.length} 章`
  chDeleteModalOpen.value = true
}

async function confirmChDelete() {
  chDeleteModalOpen.value = false
  chDeleteLoading.value = true
  try {
    if (chDeleteTargetIds.value.length === 1) {
      await deleteChapter(projectId, chDeleteTargetIds.value[0])
    } else {
      await batchDeleteChapters(projectId, chDeleteTargetIds.value)
    }
    toast.success(`已删除 ${chDeleteTargetIds.value.length} 章及其关联数据`)
    await loadAll()
  } catch { /* */ }
  finally { chDeleteLoading.value = false }
}

// ── SignalR 监听大纲生成完成 ──────────────────────────
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

watch(outlineStage, (e) => {
  if (e?.stage === 'done') {
    toast.success(e.summary ?? '大纲已生成')
    void loadAll()
  } else if (e?.stage === 'failed') {
    toast.error(e.error ?? '大纲生成失败')
  }
})

watch(selectedOutlineId, () => {
  void Promise.all([loadChapters(), loadSuggestions()])
})

onMounted(() => {
  joinProject(projectId)
  void loadAll()
})
onUnmounted(() => stop())
</script>

<template>
  <div class="page">
    <!-- 头部 -->
    <div class="page-header">
      <div class="header-left">
        <h2 class="page-title">故事大纲</h2>
        <AppBadge v-if="totalChapters > 0" variant="default">{{ totalChapters }} 章</AppBadge>
      </div>
      <div class="header-actions">
        <AppSelect
          v-if="storyOutlines.length"
          v-model="selectedOutlineId"
          :options="storyOutlines.map(o => ({ value: o.id, label: o.name }))"
          :searchable="false"
          placeholder="选择大纲"
          class="outline-select"
        />
        <AppButton variant="ghost" size="sm" @click="loadAll">
          <i class="i-lucide-refresh-cw" />
          刷新
        </AppButton>
        <AppButton
          v-if="outlineSuggestions.length > 0"
          variant="ghost"
          size="sm"
          :disabled="deleteLoading"
          @click="openDeleteConfirm('all')"
        >
          <i class="i-lucide-trash-2" />
          全部删除
        </AppButton>
        <AppButton size="sm" @click="openPlanModal">
          <i class="i-lucide-sparkles" />
          AI 规划大纲
        </AppButton>
      </div>
    </div>

    <!-- Agent 进度条 -->
    <div v-if="outlineStage && outlineStage.stage !== 'done'" class="agent-bar">
      <div class="agent-bar-inner" :class="outlineStage.stage">
        <i v-if="outlineStage.stage === 'failed'" class="i-lucide-alert-circle" />
        <i v-else class="i-lucide-loader-2 spin" />
        <span>{{ stageLabels[outlineStage.stage] ?? outlineStage.stage }}</span>
      </div>
    </div>

    <!-- ============ 大纲草案区 ============ -->
    <section class="section">
      <div class="section-header">
        <h3 class="section-title">
          <i class="i-lucide-file-text section-icon" />
          大纲草案
        </h3>
        <span class="section-hint">当前选中「{{ selectedOutline?.name ?? '默认大纲' }}」</span>
      </div>

      <div v-if="suggestionsLoading" class="skeleton-grid">
        <AppSkeleton v-for="i in 2" :key="i" width="100%" height="88px" />
      </div>

      <AppEmpty
        v-else-if="outlineSuggestions.length === 0"
        icon="i-lucide-list-tree"
        title="还没有大纲草案"
        description="点击右上方「AI 规划大纲」，让 AI 按分卷结构规划整部故事框架"
      />

      <div v-else class="draft-list">
        <div
          v-for="s in outlineSuggestions"
          :key="s.id"
          class="draft-card"
          @click="goOutlineDetail(s)"
        >
          <div class="draft-card-top">
            <span class="draft-title">{{ s.title }}</span>
            <AppBadge size="sm" :variant="(SUGGESTION_STATUS_VARIANTS[s.status] as any)">
              {{ SUGGESTION_STATUS_LABELS[s.status] }}
            </AppBadge>
          </div>
          <div class="draft-card-meta">
            <span>{{ parseOutlineVolumes(s.contentJson).length }} 卷</span>
            <span>{{ parseOutlineChapters(s.contentJson).length }} 章</span>
            <span class="draft-time">{{ new Date(s.createdAt).toLocaleString('zh-CN') }}</span>
          </div>
          <div class="draft-card-actions" @click.stop>
            <AppButton size="sm" @click="goOutlineDetail(s)">
              <i class="i-lucide-eye" />
              查看 / 编辑
            </AppButton>
            <AppButton
              variant="ghost"
              size="sm"
              :disabled="deleteLoading"
              @click="openDeleteConfirm('single', s.id)"
            >
              <i class="i-lucide-trash-2" />
              删除
            </AppButton>
          </div>
        </div>
      </div>
    </section>

    <!-- ============ 实际章节结构 ============ -->
    <section class="section">
      <div class="section-header">
        <h3 class="section-title">
          <i class="i-lucide-book-text section-icon" />
          章节结构
        </h3>
        <span class="section-hint">实际写入「{{ selectedOutline?.name ?? '默认大纲' }}」的章节</span>
        <AppButton
          v-if="chapters.length > 0"
          variant="ghost"
          size="sm"
          class="section-action"
          :disabled="chDeleteLoading"
          @click="openChDeleteAll"
        >
          <i class="i-lucide-trash-2" />
          全部删除
        </AppButton>
      </div>

      <div v-if="chaptersLoading" class="skeleton-grid">
        <AppSkeleton v-for="i in 3" :key="i" width="100%" height="40px" />
      </div>

      <AppEmpty
        v-else-if="chapters.length === 0"
        icon="i-lucide-book-text"
        title="还没有章节"
        description="先生成大纲草案，审核后导入为章节；或在章节管理页手动添加"
      >
        <template #action>
          <AppButton variant="ghost" @click="router.push(`/projects/${projectId}/chapters`)">
            <i class="i-lucide-book-text" />
            前往章节管理
          </AppButton>
        </template>
      </AppEmpty>

      <div v-else class="structure">
        <div v-for="(group, gi) in chapterGroups" :key="gi" class="group">
          <div class="group-header-row">
            <button class="group-header" @click="toggleGroup(gi)">
              <i :class="['group-chevron', expandedGroups.has(gi) ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right']" />
              <span class="group-label">{{ group.label }}</span>
              <AppBadge size="sm" variant="default">{{ group.badge }}</AppBadge>
            </button>
            <AppButton
              variant="ghost"
              size="sm"
              class="group-delete-btn"
              :disabled="chDeleteLoading"
              @click.stop="openChDeleteGroup(group)"
            >
              <i class="i-lucide-trash-2" />
            </AppButton>
          </div>
          <div v-if="expandedGroups.has(gi)" class="group-chapters">
            <div
              v-for="ch in group.chapters"
              :key="ch.id"
              class="ch-row"
              @click="goChapterDetail(ch)"
            >
              <span class="ch-num">{{ ch.number }}</span>
              <span class="ch-title">{{ ch.title || '未命名' }}</span>
              <span class="ch-summary">{{ ch.summary || '—' }}</span>
              <AppBadge :variant="(STATUS_VARIANTS[ch.status] as any)" size="sm">
                {{ STATUS_LABELS[ch.status] }}
              </AppBadge>
              <button
                class="ch-delete-btn"
                :disabled="chDeleteLoading"
                @click.stop="openChDeleteSingle(ch)"
              >
                <i class="i-lucide-trash-2" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- AI 大纲规划弹窗 -->
    <AppModal v-model="planModalOpen" title="AI 规划大纲" width="560px">
      <div class="plan-form">
        <div class="plan-mode-row">
          <button :class="['plan-mode-btn', { active: planForm.mode === 'new' }]" @click="planForm.mode = 'new'">
            <i class="i-lucide-file-plus" /> 全新规划
          </button>
          <button :class="['plan-mode-btn', { active: planForm.mode === 'continue' }]" :disabled="!chapters.length" @click="planForm.mode = 'continue'">
            <i class="i-lucide-arrow-right-from-line" /> 续写扩展
          </button>
          <button :class="['plan-mode-btn', { active: planForm.mode === 'extra' }]" :disabled="!chapters.length" @click="planForm.mode = 'extra'">
            <i class="i-lucide-sparkles" /> 番外/支线
          </button>
        </div>
        <p v-if="planForm.mode === 'continue' && chapters.length" class="plan-hint">
          将基于已有 <strong>{{ chapters.length }}</strong> 章续写，新章从第 {{ chapters.length + 1 }} 章开始。
        </p>
        <p v-else-if="planForm.mode === 'extra' && chapters.length" class="plan-hint">
          生成番外/支线卷，不影响主线。
        </p>
        <div class="context-info">
          <span><i class="i-lucide-users" /> {{ characterCount }} 位角色</span>
          <span><i class="i-lucide-globe" /> {{ worldRuleCount }} 条世界观规则</span>
        </div>
        <AppTextarea v-model="planForm.goal" label="故事目标 *" placeholder="描述你希望的故事发展方向..." :rows="4" />
        <AppInput v-model="planForm.chapterCount" label="预计章节数" type="number" placeholder="10" />
        <p class="plan-count-tip"><i class="i-lucide-lightbulb" /> 建议 15～25 章</p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="planModalOpen = false">取消</AppButton>
        <AppButton :loading="planLoading" :disabled="!planForm.goal.trim()" @click="submitPlan">
          <i class="i-lucide-sparkles" /> 开始规划
        </AppButton>
      </template>
    </AppModal>

    <!-- 大纲删除确认弹窗 -->
    <AppModal v-model="deleteModalOpen" title="确认删除大纲" width="480px">
      <div class="delete-confirm-body">
        <div class="delete-confirm-icon">
          <i class="i-lucide-triangle-alert" />
        </div>
        <template v-if="deleteScope === 'single'">
          <p class="delete-confirm-title">即将删除这条大纲</p>
          <p v-if="deleteTargetApplied" class="delete-confirm-desc">
            ⚠️ 该大纲已被导入为章节，<strong>删除后由其生成的所有章节（包括计划、草稿、定稿）将一并永久删除</strong>，且无法恢复。
          </p>
          <p v-else class="delete-confirm-desc">
            删除后该大纲记录将被永久移除，无法恢复。
          </p>
        </template>
        <template v-else>
          <p class="delete-confirm-title">即将删除所有大纲（共 {{ outlineSuggestions.length }} 条）</p>
          <p class="delete-confirm-desc">
            ⚠️ 其中已导入为章节的大纲，<strong>对应的全部章节（包括计划、草稿、定稿）都将被永久删除</strong>，操作不可撤销。
          </p>
        </template>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="deleteModalOpen = false">取消</AppButton>
        <AppButton variant="danger" :loading="deleteLoading" @click="confirmDelete">
          <i class="i-lucide-trash-2" />
          确认删除
        </AppButton>
      </template>
    </AppModal>

    <!-- 章节删除确认弹窗 -->
    <AppModal v-model="chDeleteModalOpen" title="确认删除章节" width="480px">
      <div class="delete-confirm-body">
        <div class="delete-confirm-icon">
          <i class="i-lucide-triangle-alert" />
        </div>
        <p class="delete-confirm-title">即将删除 {{ chDeleteLabel }}</p>
        <p class="delete-confirm-desc">
          ⚠️ 删除后，章节的<strong>计划参数、草稿、正式定稿、场景</strong>等所有关联数据将一并永久清除，无法恢复。
        </p>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="chDeleteModalOpen = false">取消</AppButton>
        <AppButton variant="danger" :loading="chDeleteLoading" @click="confirmChDelete">
          <i class="i-lucide-trash-2" />
          确认删除
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.page {
  padding: 24px;
  max-width: 960px;
  margin: 0 auto;
}

/* 删除确认弹窗 */
.delete-confirm-body {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 8px 0 4px;
  text-align: center;
}
.delete-confirm-icon {
  font-size: 36px;
  color: #f59e0b;
}
.delete-confirm-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}
.delete-confirm-desc {
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.6;
  margin: 0;
  max-width: 380px;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}
.header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}
.page-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}
.header-actions {
  display: flex;
  gap: 8px;
}

.outline-select {
  min-width: 170px;
}

/* Agent bar */
.agent-bar { margin-bottom: 16px; }
.agent-bar-inner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
}
.agent-bar-inner.started,
.agent-bar-inner.generating {
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-primary) 20%, transparent);
  color: var(--color-primary);
}
.agent-bar-inner.failed {
  background: color-mix(in srgb, var(--color-danger) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-danger) 20%, transparent);
  color: var(--color-danger);
}
@keyframes spin { to { transform: rotate(360deg); } }
.spin { animation: spin 1s linear infinite; }

/* Section */
.section {
  margin-bottom: 32px;
}
.section-header {
  display: flex;
  align-items: baseline;
  gap: 10px;
  margin-bottom: 12px;
}
.section-title {
  font-size: 15px;
  font-weight: 600;
  margin: 0;
  color: var(--color-text-primary);
  display: flex;
  align-items: center;
  gap: 6px;
}
.section-icon {
  font-size: 16px;
  color: var(--color-text-muted);
}
.section-hint {
  font-size: 12px;
  color: var(--color-text-muted);
}
.section-action {
  margin-left: auto;
}

.skeleton-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

/* Draft list */
.draft-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.draft-card {
  padding: 14px 16px;
  border-radius: 10px;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  cursor: pointer;
  transition: border-color 0.15s;
}
.draft-card:hover { border-color: var(--color-primary); }

.draft-card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 6px;
}
.draft-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.draft-card-meta {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin-bottom: 8px;
}
.draft-time {
  margin-left: auto;
}
.draft-card-actions {
  display: flex;
  gap: 6px;
}

/* Chapter structure */
.structure {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.group-header-row {
  display: flex;
  align-items: center;
  gap: 4px;
}
.group-header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  padding: 10px 14px;
  border-radius: 8px;
  background: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
  transition: background-color 0.12s;
  text-align: left;
}
.group-header:hover { background: var(--color-bg-elevated); }
.group-delete-btn {
  opacity: 0;
  transition: opacity 0.15s;
}
.group-header-row:hover .group-delete-btn {
  opacity: 1;
}
.group-chevron {
  font-size: 14px;
  color: var(--color-text-muted);
  flex-shrink: 0;
}
.group-label { flex: 1; }
.group-chapters {
  margin-left: 20px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.ch-row {
  display: grid;
  grid-template-columns: 40px 160px 1fr auto 28px;
  align-items: center;
  gap: 12px;
  padding: 8px 14px;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.12s;
}
.ch-row:hover { background: var(--color-bg-elevated); }
.ch-delete-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  opacity: 0;
  transition: opacity 0.15s, color 0.15s, background-color 0.15s;
  font-size: 14px;
}
.ch-row:hover .ch-delete-btn { opacity: 1; }
.ch-delete-btn:hover {
  background: color-mix(in srgb, var(--color-danger) 10%, transparent);
  color: var(--color-danger);
}
.ch-delete-btn:disabled { opacity: 0.3; cursor: not-allowed; }
.ch-num {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-primary);
  text-align: center;
}
.ch-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ch-summary {
  font-size: 12px;
  color: var(--color-text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Plan modal */
.plan-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.plan-mode-row {
  display: flex;
  gap: 8px;
}
.plan-mode-btn {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
  transition: all 0.15s;
}
.plan-mode-btn:hover { border-color: var(--color-primary); }
.plan-mode-btn.active {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  border-color: var(--color-primary);
  color: var(--color-primary);
}
.plan-mode-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.plan-hint {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin: 0;
}
.context-info {
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: var(--color-text-muted);
}
.context-info i { margin-right: 4px; }
.plan-count-tip {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}
.plan-count-tip i { margin-right: 4px; }
</style>
