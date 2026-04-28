<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { getSuggestionById, importOutline, ignoreSuggestion } from '@/api/suggestions'
import { parseOutlineChapters } from './utils'
import { STATUS_LABELS, STATUS_VARIANTS } from './utils'
import type { AgentSuggestionResponse, OutlineChapterItem } from '@/types/models'
import { useToast } from '@/composables/useToast'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const projectId = route.params.id as string
const suggestionId = route.params.suggestionId as string

// ── 加载建议详情 ──────────────────────────────────────────────
const suggestion = ref<AgentSuggestionResponse | null>(null)
const loading = ref(true)

async function loadSuggestion() {
  loading.value = true
  try {
    suggestion.value = await getSuggestionById(projectId, suggestionId)
    // 初始化可编辑列表
    chapters.value = parseOutlineChapters(suggestion.value.contentJson).map((ch) => ({ ...ch }))
  } catch {
    toast.error('加载失败')
  } finally {
    loading.value = false
  }
}

// ── 可编辑章节列表 ────────────────────────────────────────────
const chapters = ref<OutlineChapterItem[]>([])

function removeChapter(index: number) {
  chapters.value.splice(index, 1)
}

function moveUp(index: number) {
  if (index === 0) return
  const arr = chapters.value
  ;[arr[index - 1], arr[index]] = [arr[index], arr[index - 1]]
}

function moveDown(index: number) {
  if (index === chapters.value.length - 1) return
  const arr = chapters.value
  ;[arr[index], arr[index + 1]] = [arr[index + 1], arr[index]]
}

function addChapter() {
  const maxNum = chapters.value.reduce((m, c) => Math.max(m, c.number), 0)
  chapters.value.push({ number: maxNum + 1, title: '', goal: '', summary: '' })
}

// ── 重新排序编号（按当前顺序）──────────────────────────────────
function reorder() {
  chapters.value.forEach((ch, i) => {
    ch.number = i + 1
  })
}

// ── 计算是否可操作 ────────────────────────────────────────────
const canOperate = computed(() => {
  const s = suggestion.value?.status
  return s === 'Pending' || s === 'Accepted'
})

// ── 导入章节 ────────────────────────────────────────────────
const importing = ref(false)

async function submitImport() {
  if (!chapters.value.length) return
  importing.value = true
  try {
    const count = await importOutline(projectId, {
      chapters: chapters.value.map((ch) => ({
        number: ch.number,
        title: ch.title,
        goal: ch.goal || undefined,
        summary: ch.summary || undefined,
      })),
    })
    toast.success(`已导入 ${count} 个章节`)
    // 忽略原建议（已手动导入）
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

onMounted(loadSuggestion)
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
        <template v-if="canOperate">
          <AppButton variant="ghost" size="sm" @click="reorder">
            <i class="i-lucide-list-ordered" />
            重新编号
          </AppButton>
          <AppButton size="sm" variant="ghost" @click="addChapter">
            <i class="i-lucide-plus" />
            添加章节
          </AppButton>
          <AppButton
            size="sm"
            :loading="importing"
            :disabled="!chapters.length"
            @click="submitImport"
          >
            <i class="i-lucide-download" />
            导入到章节（{{ chapters.length }}）
          </AppButton>
        </template>
        <span v-else class="resolved-hint">该建议已处理，仅供查阅</span>
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

    <!-- 章节编辑列表 -->
    <div v-else class="chapter-editor">
      <div
        v-for="(ch, i) in chapters"
        :key="i"
        class="chapter-card"
      >
        <!-- 卡片头部 -->
        <div class="card-header">
          <div class="card-num-wrap">
            <span class="card-seq">{{ i + 1 }}</span>
            <input
              v-model.number="ch.number"
              class="num-input"
              type="number"
              title="章节编号"
              :disabled="!canOperate"
            />
          </div>
          <input
            v-model="ch.title"
            class="title-input"
            placeholder="章节标题"
            :disabled="!canOperate"
          />
          <div v-if="canOperate" class="card-actions">
            <button class="icon-btn" title="上移" :disabled="i === 0" @click="moveUp(i)">
              <i class="i-lucide-chevron-up" />
            </button>
            <button
              class="icon-btn"
              title="下移"
              :disabled="i === chapters.length - 1"
              @click="moveDown(i)"
            >
              <i class="i-lucide-chevron-down" />
            </button>
            <button class="icon-btn danger" title="删除" @click="removeChapter(i)">
              <i class="i-lucide-trash-2" />
            </button>
          </div>
        </div>

        <!-- 目标 -->
        <div class="card-field">
          <label class="field-label">章节目标</label>
          <input
            v-model="ch.goal"
            class="field-input"
            placeholder="本章希望达成的叙事目标..."
            :disabled="!canOperate"
          />
        </div>

        <!-- 摘要 -->
        <div class="card-field">
          <label class="field-label">章节摘要</label>
          <textarea
            v-model="ch.summary"
            class="field-textarea"
            placeholder="本章主要情节概述..."
            rows="3"
            :disabled="!canOperate"
          />
        </div>
      </div>

      <div v-if="!chapters.length" class="empty-hint">
        <i class="i-lucide-inbox" />
        <span>暂无章节数据</span>
      </div>
    </div>

    <!-- 底部固定操作栏 -->
    <div v-if="!loading && canOperate && chapters.length" class="bottom-bar">
      <span class="bottom-hint">共 {{ chapters.length }} 章，可在上方直接编辑后导入</span>
      <AppButton
        :loading="importing"
        @click="submitImport"
      >
        <i class="i-lucide-download" />
        确认导入 {{ chapters.length }} 章到项目
      </AppButton>
    </div>
  </div>
</template>

<style scoped>
.page {
  padding: 24px;
  max-width: 860px;
  margin: 0 auto;
  padding-bottom: 88px; /* 留出底部栏空间 */
}

.page-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 24px;
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
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: var(--color-primary);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.72rem;
  font-weight: 700;
  flex-shrink: 0;
}

.num-input {
  width: 52px;
  padding: 3px 6px;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  background: var(--color-bg-input);
  color: var(--color-text-primary);
  font-size: 0.82rem;
  text-align: center;
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
.num-input:disabled,
.field-input:disabled,
.field-textarea:disabled {
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
</style>
