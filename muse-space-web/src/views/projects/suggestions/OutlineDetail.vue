<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import { getStoryOutlines } from '@/api/outlines'
import {
  getSuggestionById,
  importOutline,
  ignoreSuggestion,
  regenerateOutlineVolume,
  updateSuggestionContent,
} from '@/api/suggestions'
import { parseOutlineVolumes, STATUS_LABELS, STATUS_VARIANTS } from './utils'
import type {
  AgentSuggestionResponse,
  OutlineChapterItem,
  OutlineVolumeItem,
  StoryOutlineResponse,
} from '@/types/models'
import { useToast } from '@/composables/useToast'
import { useAgentProgress } from '@/composables/useAgentProgress'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const projectId = route.params.id as string
const suggestionId = route.params.suggestionId as string

// ── SignalR 进度订阅（监听单卷重做完成自动刷新） ─────────────
const agentProgress = useAgentProgress()

// ── 加载建议详情 ──────────────────────────────────────────────
const suggestion = ref<AgentSuggestionResponse | null>(null)
const loading = ref(true)
const volumes = ref<OutlineVolumeItem[]>([])
const activeVolumeIndex = ref(0)
const outlines = ref<StoryOutlineResponse[]>([])
const selectedOutlineId = ref('')
const selectedOutline = computed(() =>
  outlines.value.find((o) => o.id === selectedOutlineId.value) ?? null,
)

async function loadSuggestion() {
  loading.value = true
  try {
    suggestion.value = await getSuggestionById(projectId, suggestionId)
    outlines.value = await getStoryOutlines(projectId).catch(() => [])
    selectedOutlineId.value = suggestion.value.targetEntityId
      ?? outlines.value.find((o) => o.isDefault)?.id
      ?? outlines.value[0]?.id
      ?? ''
    volumes.value = parseOutlineVolumes(suggestion.value.contentJson).map((v) => ({
      ...v,
      chapters: (v.chapters ?? []).map((c) => ({ ...c })),
    }))
    if (volumes.value.length === 0) {
      volumes.value = [{ number: 1, title: '空大纲', theme: '', chapters: [] }]
    }
    if (activeVolumeIndex.value >= volumes.value.length) activeVolumeIndex.value = 0
  } catch {
    toast.error('加载失败')
  } finally {
    loading.value = false
  }
}

const activeVolume = computed(() => volumes.value[activeVolumeIndex.value])
const totalChapters = computed(() => volumes.value.reduce((s, v) => s + v.chapters.length, 0))

// ── 章节编辑 ──────────────────────────────────────────────────
function removeChapter(index: number) {
  activeVolume.value?.chapters.splice(index, 1)
}

function moveUp(index: number) {
  if (!activeVolume.value || index === 0) return
  const arr = activeVolume.value.chapters
  ;[arr[index - 1], arr[index]] = [arr[index], arr[index - 1]]
}

function moveDown(index: number) {
  if (!activeVolume.value || index === activeVolume.value.chapters.length - 1) return
  const arr = activeVolume.value.chapters
  ;[arr[index], arr[index + 1]] = [arr[index + 1], arr[index]]
}

function addChapter() {
  if (!activeVolume.value) return
  const allChapters: OutlineChapterItem[] = volumes.value.flatMap((v) => v.chapters)
  const maxNum = allChapters.reduce((m, c) => Math.max(m, c.number), 0)
  activeVolume.value.chapters.push({
    number: maxNum + 1,
    title: '',
    goal: '',
    summary: '',
  })
}

function reorderAll() {
  let n = 1
  for (const v of volumes.value)
    for (const ch of v.chapters) ch.number = n++
}

// ── 卷操作 ────────────────────────────────────────────────────
function selectVolume(i: number) {
  activeVolumeIndex.value = i
}

// ── 单卷重做弹窗 ──────────────────────────────────────────────
const regenModalOpen = ref(false)
const regenInstruction = ref('')
const regenLoading = ref(false)

function openRegenModal() {
  regenInstruction.value = ''
  regenModalOpen.value = true
}

async function submitRegen() {
  if (!activeVolume.value) return
  regenLoading.value = true
  try {
    await regenerateOutlineVolume(projectId, suggestionId, activeVolume.value.number, {
      extraInstruction: regenInstruction.value.trim() || undefined,
    })
    toast.success('重做任务已提交，完成后将自动刷新')
    regenModalOpen.value = false
  } catch {
    // handled
  } finally {
    regenLoading.value = false
  }
}

// 监听重做进度
watch(agentProgress.latestEvent, (ev) => {
  if (!ev || ev.taskType !== 'outline-volume-regenerate') return
  if (ev.stage === 'done') {
    toast.success(ev.summary ?? '卷已重做完成')
    void loadSuggestion()
  } else if (ev.stage === 'failed') {
    toast.error(ev.error ?? '重做失败')
  }
})

// ── 是否可操作 ────────────────────────────────────────────────
const canOperate = computed(() => {
  const s = suggestion.value?.status
  return s === 'Pending' || s === 'Accepted'
})

// ── 保存大纲编辑（任意状态均可） ────────────────────────────
const saving = ref(false)

async function saveContent() {
  saving.value = true
  try {
    const contentJson = JSON.stringify({ volumes: volumes.value })
    await updateSuggestionContent(projectId, suggestionId, contentJson)
    toast.success('大纲修改已保存')
  } catch {
    // handled by http interceptor
  } finally {
    saving.value = false
  }
}

// ── 导入章节 ──────────────────────────────────────────────────
const importing = ref(false)

async function submitImport() {
  if (totalChapters.value === 0) return
  importing.value = true
  try {
    const flatChapters = volumes.value.flatMap((v) => v.chapters)
    const count = await importOutline(projectId, {
      storyOutlineId: selectedOutlineId.value || undefined,
      chapters: flatChapters.map((ch) => ({
        number: ch.number,
        title: ch.title,
        goal: ch.goal || undefined,
        summary: ch.summary || undefined,
      })),
    })
    toast.success(`已导入 ${count} 个章节`)
    try {
      await ignoreSuggestion(projectId, suggestionId)
    } catch {
      // non-critical
    }
    router.push(`/projects/${projectId}/chapters`)
  } catch {
    // handled
  } finally {
    importing.value = false
  }
}

onMounted(() => {
  void agentProgress.joinProject(projectId)
  void loadSuggestion()
})
onUnmounted(() => {
  agentProgress.stop()
})
</script>

<template>
  <div class="page">
    <!-- 顶栏 -->
    <div class="page-header">
      <AppButton variant="ghost" size="sm" @click="router.back()">
        <i class="i-lucide-arrow-left" />
        返回
      </AppButton>
      <div class="header-center">
        <h2 class="page-title">大纲详情</h2>
        <template v-if="suggestion">
          <AppBadge size="sm" variant="default">大纲规划</AppBadge>
          <AppBadge size="sm" :variant="STATUS_VARIANTS[suggestion.status]">
            {{ STATUS_LABELS[suggestion.status] }}
          </AppBadge>
        </template>
      </div>
      <div class="header-actions">
        <select v-if="outlines.length" v-model="selectedOutlineId" class="outline-select">
          <option v-for="outline in outlines" :key="outline.id" :value="outline.id">
            {{ outline.name }}
          </option>
        </select>
        <!-- 始终可以重排编号 -->
        <AppButton variant="ghost" size="sm" @click="reorderAll">
          <i class="i-lucide-list-ordered" />
          重排编号
        </AppButton>
        <!-- Pending / Accepted：可导入 -->
        <AppButton
          v-if="canOperate"
          size="sm"
          :loading="importing"
          :disabled="!totalChapters"
          @click="submitImport"
        >
          <i class="i-lucide-download" />
          导入到章节（共 {{ totalChapters }} 章）
        </AppButton>
        <!-- Applied / Ignored：保存修改 -->
        <AppButton
          v-else-if="suggestion"
          variant="ghost"
          size="sm"
          :loading="saving"
          @click="saveContent"
        >
          <i class="i-lucide-save" />
          保存修改
        </AppButton>
      </div>
    </div>

    <!-- 加载骨架屏 -->
    <div v-if="loading" class="skeleton-list">
      <div v-for="i in 5" :key="i" class="skeleton-row">
        <AppSkeleton width="60px" height="14px" />
        <AppSkeleton width="160px" height="14px" />
        <AppSkeleton width="40%" height="14px" />
      </div>
    </div>

    <!-- 主体：左卷导航 + 右章节编辑 -->
    <div v-else class="outline-layout">
      <!-- 左：卷导航 -->
      <aside class="vol-nav">
        <div class="vol-nav-header">
          <span>分卷（{{ volumes.length }}）</span>
        </div>
        <div class="vol-list">
          <button
            v-for="(v, i) in volumes"
            :key="i"
            :class="['vol-item', { active: i === activeVolumeIndex }]"
            @click="selectVolume(i)"
          >
            <div class="vol-item-top">
              <span class="vol-num">卷{{ v.number }}</span>
              <span class="vol-count">{{ v.chapters.length }}章</span>
            </div>
            <div class="vol-title">{{ v.title || '未命名卷' }}</div>
            <div v-if="v.theme" class="vol-theme">{{ v.theme }}</div>
          </button>
        </div>
      </aside>

      <!-- 右：当前卷章节编辑 -->
      <main class="vol-main">
        <div v-if="activeVolume" class="vol-main-header">
          <div class="vol-main-title-row">
            <input
              v-model="activeVolume.title"
              class="vol-title-input"
              placeholder="卷标题"
            />
            <AppButton
              v-if="canOperate"
              variant="ghost"
              size="sm"
              @click="openRegenModal"
            >
              <i class="i-lucide-rotate-ccw" />
              重做本卷
            </AppButton>
            <AppButton
              variant="ghost"
              size="sm"
              @click="addChapter"
            >
              <i class="i-lucide-plus" />
              添加章节
            </AppButton>
          </div>
          <input
            v-model="activeVolume.theme"
            class="vol-theme-input"
            placeholder="卷主题（一句话描述本卷核心冲突）"
          />
        </div>

        <div v-if="activeVolume && activeVolume.chapters.length" class="chapter-editor">
          <div
            v-for="(ch, i) in activeVolume.chapters"
            :key="i"
            class="chapter-card"
          >
            <div class="card-header">
              <div class="card-num-wrap">
                <span class="card-seq">{{ ch.number }}</span>
              </div>
              <input
                v-model="ch.title"
                class="title-input"
                placeholder="章节标题"
              />
              <div class="card-actions">
                <button class="icon-btn" title="上移" :disabled="i === 0" @click="moveUp(i)">
                  <i class="i-lucide-chevron-up" />
                </button>
                <button
                  class="icon-btn"
                  title="下移"
                  :disabled="i === activeVolume.chapters.length - 1"
                  @click="moveDown(i)"
                >
                  <i class="i-lucide-chevron-down" />
                </button>
                <button class="icon-btn danger" title="删除" @click="removeChapter(i)">
                  <i class="i-lucide-trash-2" />
                </button>
              </div>
            </div>

            <div class="card-field">
              <label class="field-label">章节目标</label>
              <input
                v-model="ch.goal"
                class="field-input"
                placeholder="本章希望达成的叙事目标..."
              />
            </div>

            <div class="card-field">
              <label class="field-label">章节摘要</label>
              <textarea
                v-model="ch.summary"
                class="field-textarea"
                placeholder="本章主要情节概述..."
                rows="3"
              />
            </div>
          </div>
        </div>

        <div v-else class="empty-hint">
          <i class="i-lucide-inbox" />
          <span>本卷暂无章节，可点击右上方"添加章节"</span>
        </div>
      </main>
    </div>

    <!-- 底部固定操作栏 -->
    <div v-if="!loading && totalChapters" class="bottom-bar">
      <span class="bottom-hint">
        <template v-if="canOperate">共 {{ volumes.length }} 卷 / {{ totalChapters }} 章，将导入到「{{ selectedOutline?.name ?? '默认大纲' }}」</template>
        <template v-else>共 {{ volumes.length }} 卷 / {{ totalChapters }} 章 · 已应用/忽略状态，修改只更新大纲记录</template>
      </span>
      <AppButton v-if="canOperate" :loading="importing" @click="submitImport">
        <i class="i-lucide-download" />
        确认导入 {{ totalChapters }} 章到项目
      </AppButton>
      <AppButton v-else variant="ghost" :loading="saving" @click="saveContent">
        <i class="i-lucide-save" />
        保存修改
      </AppButton>
    </div>

    <!-- 重做卷弹窗 -->
    <AppModal v-model="regenModalOpen" title="重做本卷" width="540px">
      <div class="regen-body">
        <p class="regen-hint">
          将基于其它卷的概览，为
          <strong>卷{{ activeVolume?.number }}《{{ activeVolume?.title }}》</strong>
          重新规划章节。其它卷不会受影响。
        </p>
        <AppTextarea
          v-model="regenInstruction"
          placeholder="可选：附加要求，例如「加强卷末高潮」「主角与反派一次正面交锋」..."
          :rows="4"
        />
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="regenModalOpen = false">取消</AppButton>
        <AppButton :loading="regenLoading" @click="submitRegen">
          <i class="i-lucide-rotate-ccw" />
          提交重做
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.page {
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
  padding-bottom: 88px;
}

.page-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.header-center {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
}

.page-title {
  font-size: 1.15rem;
  font-weight: 600;
  margin: 0;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.outline-select {
  height: 32px;
  min-width: 160px;
  padding: 0 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-input);
  color: var(--color-text-primary);
  font-size: 13px;
}

.resolved-hint {
  font-size: 0.82rem;
  color: var(--color-text-tertiary);
}

/* 骨架屏 */
.skeleton-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.skeleton-row {
  display: flex;
  gap: 12px;
  padding: 16px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
}

/* 主体两栏布局 */
.outline-layout {
  display: grid;
  grid-template-columns: 240px 1fr;
  gap: 20px;
  align-items: start;
}

/* 左侧卷导航 */
.vol-nav {
  position: sticky;
  top: 16px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-bg-card);
  overflow: hidden;
}

.vol-nav-header {
  padding: 10px 14px;
  border-bottom: 1px solid var(--color-border);
  font-size: 0.78rem;
  color: var(--color-text-tertiary);
  font-weight: 600;
  letter-spacing: 0.5px;
}

.vol-list {
  display: flex;
  flex-direction: column;
}

.vol-item {
  text-align: left;
  background: none;
  border: none;
  cursor: pointer;
  padding: 10px 14px;
  border-bottom: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  gap: 4px;
  transition: background 0.12s;
}
.vol-item:last-child {
  border-bottom: none;
}
.vol-item:hover {
  background: var(--color-bg-hover);
}
.vol-item.active {
  background: var(--color-primary-soft, rgba(96, 165, 250, 0.12));
  border-left: 3px solid var(--color-primary);
  padding-left: 11px;
}

.vol-item-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.74rem;
  color: var(--color-text-tertiary);
}

.vol-num {
  font-weight: 700;
  color: var(--color-primary);
}

.vol-count {
  font-variant-numeric: tabular-nums;
}

.vol-title {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.vol-theme {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* 右侧主区 */
.vol-main {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.vol-main-header {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 14px 16px;
  border: 1px solid var(--color-border);
  border-radius: 10px;
  background: var(--color-bg-surface);
}

.vol-main-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.vol-title-input {
  flex: 1;
  padding: 6px 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-input);
  color: var(--color-text-primary);
  font-size: 1rem;
  font-weight: 600;
}

.vol-theme-input {
  padding: 6px 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-input);
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

/* 章节卡片 */
.chapter-editor {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.chapter-card {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: var(--color-bg-card);
  transition: border-color 0.15s;
}

.chapter-card:hover {
  border-color: var(--color-primary);
}

.card-header {
  display: flex;
  align-items: center;
  gap: 10px;
}

.card-num-wrap {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}

.card-seq {
  min-width: 28px;
  height: 22px;
  padding: 0 6px;
  border-radius: 11px;
  background: var(--color-primary);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.72rem;
  font-weight: 700;
  flex-shrink: 0;
}

.title-input {
  flex: 1;
  padding: 5px 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-input);
  color: var(--color-text-primary);
  font-size: 0.9rem;
  font-weight: 500;
}

.title-input:disabled,
.field-input:disabled,
.field-textarea:disabled,
.vol-title-input:disabled,
.vol-theme-input:disabled {
  opacity: 0.65;
  cursor: default;
  background: var(--color-bg-muted, var(--color-bg-input));
}

.card-actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.icon-btn {
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px 5px;
  border-radius: 4px;
  color: var(--color-text-tertiary);
  display: flex;
  align-items: center;
}

.icon-btn:hover:not(:disabled) {
  background: var(--color-bg-hover);
  color: var(--color-text-primary);
}

.icon-btn.danger:hover:not(:disabled) {
  color: var(--color-danger);
}

.icon-btn:disabled {
  opacity: 0.3;
  cursor: default;
}

.card-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field-label {
  font-size: 0.75rem;
  color: var(--color-text-tertiary);
  font-weight: 500;
}

.field-input {
  padding: 5px 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-input);
  color: var(--color-text-primary);
  font-size: 0.85rem;
}

.field-textarea {
  padding: 6px 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-input);
  color: var(--color-text-primary);
  font-size: 0.85rem;
  resize: vertical;
  line-height: 1.5;
}

.empty-hint {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 48px;
  color: var(--color-text-tertiary);
  font-size: 0.9rem;
  border: 1px dashed var(--color-border);
  border-radius: 10px;
}

/* 底部固定操作栏 */
.bottom-bar {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 16px;
  padding: 14px 32px;
  background: var(--color-bg-surface);
  border-top: 1px solid var(--color-border);
  z-index: 10;
}

.bottom-hint {
  font-size: 0.82rem;
  color: var(--color-text-tertiary);
}

/* 弹窗 */
.regen-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.regen-hint {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: 0.88rem;
  line-height: 1.6;
}

@media (max-width: 900px) {
  .outline-layout {
    grid-template-columns: 1fr;
  }
  .vol-nav {
    position: static;
  }
}
</style>
