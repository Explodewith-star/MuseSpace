<script setup lang="ts">
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { useRoute, useRouter } from 'vue-router'
import { initSuggestionsState } from './hooks'
import {
  CATEGORY_LABELS,
  CATEGORY_ICONS,
  STATUS_LABELS,
  STATUS_VARIANTS,
  SEVERITY_LABELS,
  SEVERITY_VARIANTS,
  parseContentJson,
  canAccept,
  canApply,
  isOutline,
  parseOutlineChapters,
} from './utils'
import type { AgentSuggestionResponse } from '@/types/models'

const route = useRoute()
const router = useRouter()
const projectId = route.params.id as string

const {
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
} = initSuggestionsState()

function goOutlineDetail(s: AgentSuggestionResponse) {
  router.push(`/projects/${projectId}/suggestions/outline/${s.id}`)
}

const categoryOptions = [
  { value: '', label: '全部类型' },
  { value: 'Consistency', label: '世界观一致性' },
  { value: 'Character', label: '角色一致性' },
  { value: 'Outline', label: '大纲规划' },
]

const checkTypeOptions = [
  { value: 'consistency', label: '世界观一致性检查', icon: 'i-lucide-shield-alert' },
  { value: 'character', label: '角色一致性检查', icon: 'i-lucide-user-check' },
] as const

const statusOptions = [
  { value: '', label: '全部状态' },
  { value: 'Pending', label: '待处理' },
  { value: 'Accepted', label: '已接受' },
  { value: 'Applied', label: '已应用' },
  { value: 'Ignored', label: '已忽略' },
]

function getCategoryLabel(category: string): string {
  return CATEGORY_LABELS[category] ?? category
}

function getCategoryIcon(category: string): string {
  return CATEGORY_ICONS[category] ?? 'i-lucide-bot'
}

function getContent(s: AgentSuggestionResponse) {
  return parseContentJson(s.contentJson)
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <div class="header-left">
        <h2 class="page__title">建议中心</h2>
        <AppBadge v-if="pendingCount > 0" variant="accent">{{ pendingCount }} 待处理</AppBadge>
      </div>
      <div class="header-actions">
        <AppButton variant="ghost" size="sm" @click="loadSuggestions">
          <i class="i-lucide-refresh-cw" />
          刷新
        </AppButton>
        <AppButton size="sm" @click="checkModalOpen = true">
          <i class="i-lucide-shield-check" />
          触发一致性检查
        </AppButton>
      </div>
    </div>

    <!-- 筛选栏 -->
    <div class="filter-bar">
      <div class="filter-tabs">
        <button
          v-for="opt in categoryOptions"
          :key="opt.value"
          :class="['filter-tab', { active: filterCategory === opt.value }]"
          @click="filterCategory = opt.value"
        >
          {{ opt.label }}
        </button>
      </div>
      <div class="filter-tabs">
        <button
          v-for="opt in statusOptions"
          :key="opt.value"
          :class="['filter-tab', { active: filterStatus === opt.value }]"
          @click="filterStatus = (opt.value as any)"
        >
          {{ opt.label }}
        </button>
      </div>
    </div>

    <!-- 批量操作栏 -->
    <div v-if="selectedIds.size > 0" class="batch-bar">
      <span class="batch-info">已选 {{ selectedIds.size }} 条</span>
      <AppButton variant="ghost" size="sm" @click="batchAccept">
        <i class="i-lucide-check" />
        批量接受
      </AppButton>
      <AppButton variant="ghost" size="sm" @click="batchIgnore">
        <i class="i-lucide-x" />
        批量忽略
      </AppButton>
    </div>

    <!-- 骨架屏 -->
    <div v-if="loading" class="suggestion-list">
      <div v-for="i in 3" :key="i" class="suggestion-card skeleton-card">
        <AppSkeleton width="60%" height="16px" />
        <AppSkeleton width="100%" height="12px" style="margin-top:8px" />
        <AppSkeleton width="80%" height="12px" style="margin-top:6px" />
      </div>
    </div>

    <!-- 空状态 -->
    <AppEmpty
      v-else-if="!filteredSuggestions.length"
      icon="i-lucide-inbox"
      title="暂无建议"
      description="触发一致性检查或等待 Agent 运行后，建议将出现在这里"
    >
      <template #action>
        <AppButton @click="checkModalOpen = true">
          <i class="i-lucide-shield-check" />
          触发一致性检查
        </AppButton>
      </template>
    </AppEmpty>

    <!-- 建议列表 -->
    <div v-else class="suggestion-list">
      <!-- 全选行 -->
      <div class="select-all-row">
        <label class="checkbox-label">
          <input
            type="checkbox"
            :checked="allFilteredSelected"
            @change="toggleSelectAll"
          />
          全选当前筛选结果
        </label>
      </div>

      <div
        v-for="s in filteredSuggestions"
        :key="s.id"
        :class="['suggestion-card', { selected: selectedIds.has(s.id) }]"
      >
        <div class="card-top">
          <div class="card-left">
            <input
              type="checkbox"
              :checked="selectedIds.has(s.id)"
              class="card-checkbox"
              @change="toggleSelect(s.id)"
            />
            <i :class="['card-icon', getCategoryIcon(s.category)]" />
            <span class="card-title">{{ s.title }}</span>
          </div>
          <div class="card-badges">
            <AppBadge size="sm" variant="default">{{ getCategoryLabel(s.category) }}</AppBadge>
            <AppBadge
              v-if="getContent(s).severity"
              size="sm"
              :variant="SEVERITY_VARIANTS[(getContent(s).severity as string)] ?? 'default'"
            >
              {{ SEVERITY_LABELS[(getContent(s).severity as string)] ?? getContent(s).severity }}级
            </AppBadge>
            <AppBadge size="sm" :variant="STATUS_VARIANTS[s.status]">
              {{ STATUS_LABELS[s.status] }}
            </AppBadge>
          </div>
        </div>

        <!-- ─── 大纲类卡片内容 ─── -->
        <template v-if="isOutline(s)">
          <div class="outline-preview">
            <div
              v-for="(ch, i) in parseOutlineChapters(s.contentJson).slice(0, 3)"
              :key="i"
              class="outline-chapter-row"
            >
              <span class="outline-ch-num">第{{ ch.number }}章</span>
              <span class="outline-ch-title">{{ ch.title }}</span>
              <span class="outline-ch-goal">{{ ch.goal }}</span>
            </div>
            <div
              v-if="parseOutlineChapters(s.contentJson).length > 3"
              class="outline-more"
            >
              ... 共 {{ parseOutlineChapters(s.contentJson).length }} 章
            </div>
          </div>

          <div class="card-actions">
            <AppButton size="sm" @click="goOutlineDetail(s)">
              <i class="i-lucide-list-tree" />
              查看完整大纲
            </AppButton>
            <AppButton
              v-if="s.status === 'Pending'"
              variant="ghost"
              size="sm"
              :loading="actionLoadingIds.has(s.id)"
              @click="ignore(s.id)"
            >
              <i class="i-lucide-x" />
              忽略
            </AppButton>
            <span v-if="s.status !== 'Pending'" class="resolved-time">
              {{ s.resolvedAt ? new Date(s.resolvedAt).toLocaleString('zh-CN') : '' }}
            </span>
          </div>
        </template>

        <!-- ─── 一致性/角色类卡片内容 ─── -->
        <template v-else>
          <!-- 内容详情（Consistency 类型） -->
          <div v-if="getContent(s).conflictSnippet" class="conflict-snippet">
            <i class="i-lucide-quote" />
            <span>{{ getContent(s).conflictSnippet }}</span>
          </div>

          <div v-if="getContent(s).explanation" class="card-section">
            <span class="section-label">冲突说明</span>
            <p class="section-text">{{ getContent(s).explanation }}</p>
          </div>

          <div v-if="getContent(s).suggestion" class="card-section">
            <span class="section-label">修改建议</span>
            <p class="section-text suggestion-text">{{ getContent(s).suggestion }}</p>
          </div>

          <!-- 涉及规则（世界观冲突） -->
          <div v-if="getContent(s).ruleName" class="card-rule">
            <i class="i-lucide-bookmark" />
            <span>涉及规则：{{ getContent(s).ruleName }}</span>
          </div>

          <!-- 涉及角色 + 冲突类型（角色冲突） -->
          <div v-if="getContent(s).characterName" class="card-rule">
            <i class="i-lucide-user" />
            <span>涉及角色：{{ getContent(s).characterName }}</span>
            <span v-if="getContent(s).conflictType" class="conflict-type-tag">
              {{ getContent(s).conflictType }}
            </span>
          </div>

          <!-- 操作按钮 -->
          <div class="card-actions">
            <template v-if="canAccept(s)">
              <AppButton
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="accept(s.id)"
              >
                <i class="i-lucide-check" />
                接受
              </AppButton>
              <AppButton
                variant="ghost"
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="ignore(s.id)"
              >
                <i class="i-lucide-x" />
                忽略
              </AppButton>
            </template>
            <template v-else-if="canApply(s)">
              <AppButton
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="apply(s.id)"
              >
                <i class="i-lucide-zap" />
                应用到正式内容
              </AppButton>
            </template>
            <span v-else class="resolved-time">
              {{ s.resolvedAt ? new Date(s.resolvedAt).toLocaleString('zh-CN') : '' }}
            </span>
          </div>
        </template>
      </div>
    </div>

    <!-- 触发一致性检查弹窗 -->
    <AppModal v-model="checkModalOpen" title="触发一致性检查" width="600px">
      <div class="check-modal-body">
        <!-- 检查类型选择 -->
        <div class="check-type-row">
          <button
            v-for="opt in checkTypeOptions"
            :key="opt.value"
            :class="['check-type-btn', { active: checkType === opt.value }]"
            @click="checkType = opt.value"
          >
            <i :class="opt.icon" />
            {{ opt.label }}
          </button>
        </div>
        <p class="check-hint">
          <template v-if="checkType === 'consistency'">
            粘贴草稿文本，Agent 将根据已有<strong>世界观规则</strong>分析冲突并生成建议。
          </template>
          <template v-else>
            粘贴草稿文本，Agent 将根据已有<strong>角色卡设定</strong>分析行为、性格、状态的冲突。
          </template>
          结果将异步写入建议列表，稍后刷新即可查看。
        </p>
        <AppTextarea
          v-model="checkDraftText"
          placeholder="粘贴草稿内容..."
          :rows="10"
        />
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="checkModalOpen = false">取消</AppButton>
        <AppButton :loading="checkLoading" @click="submitConsistencyCheck">
          <i class="i-lucide-send" />
          提交检查
        </AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.page {
  padding: 24px;
  max-width: 900px;
  margin: 0 auto;
}

.page__header {
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

.page__title {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 8px;
}

/* 筛选栏 */
.filter-bar {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 16px;
}

.filter-tabs {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.filter-tab {
  padding: 4px 12px;
  border-radius: 99px;
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-muted);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s;
}

.filter-tab:hover {
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.filter-tab.active {
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  border-color: var(--color-primary);
  color: var(--color-primary);
  font-weight: 500;
}

/* 批量操作 */
.batch-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-primary) 20%, transparent);
  border-radius: 8px;
  margin-bottom: 12px;
}

.batch-info {
  font-size: 13px;
  color: var(--color-primary);
  margin-right: 4px;
}

/* 列表 */
.suggestion-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.select-all-row {
  padding: 4px 0;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-muted);
  cursor: pointer;
  user-select: none;
}

/* 建议卡片 */
.suggestion-card {
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 16px;
  background: var(--color-bg-surface);
  transition: border-color 0.15s;
}

.suggestion-card:hover {
  border-color: var(--color-border-strong);
}

.suggestion-card.selected {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 4%, var(--color-bg-surface));
}

.skeleton-card {
  padding: 16px;
}

.card-top {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
}

.card-left {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.card-checkbox {
  flex-shrink: 0;
  cursor: pointer;
}

.card-icon {
  font-size: 16px;
  color: var(--color-text-muted);
  flex-shrink: 0;
}

.card-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
  line-height: 1.4;
}

.card-badges {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
  flex-wrap: wrap;
  justify-content: flex-end;
}

/* 冲突片段引用 */
.conflict-snippet {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  padding: 10px 12px;
  background: color-mix(in srgb, var(--color-danger) 6%, transparent);
  border-left: 3px solid var(--color-danger);
  border-radius: 0 6px 6px 0;
  margin-bottom: 10px;
  font-size: 13px;
  color: var(--color-text-secondary);
  font-style: italic;
}

.conflict-snippet i {
  color: var(--color-danger);
  flex-shrink: 0;
  margin-top: 2px;
}

/* 详情段落 */
.card-section {
  margin-bottom: 8px;
}

.section-label {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--color-text-muted);
  display: block;
  margin-bottom: 4px;
}

.section-text {
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.6;
  margin: 0;
}

.suggestion-text {
  color: var(--color-text-primary);
}

/* 涉及规则 */
.card-rule {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin-bottom: 12px;
}

.card-rule i {
  font-size: 13px;
}

/* 操作按钮区 */
.card-actions {
  display: flex;
  gap: 8px;
  align-items: center;
  padding-top: 12px;
  border-top: 1px solid var(--color-border);
}

.resolved-time {
  font-size: 12px;
  color: var(--color-text-muted);
}

/* 检查弹窗 */
.check-modal-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.check-type-row {
  display: flex;
  gap: 8px;
}

.check-type-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
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

.check-type-btn:hover {
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.check-type-btn.active {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  border-color: var(--color-primary);
  color: var(--color-primary);
  font-weight: 500;
}

.check-hint {
  font-size: 13px;
  color: var(--color-text-muted);
  line-height: 1.6;
  margin: 0;
}

/* 冲突类型标签 */
.conflict-type-tag {
  display: inline-flex;
  align-items: center;
  padding: 1px 8px;
  border-radius: 99px;
  font-size: 11px;
  font-weight: 500;
  background: color-mix(in srgb, var(--color-accent) 12%, transparent);
  color: var(--color-accent);
  margin-left: 6px;
}

/* ── 大纲预览 ── */
.outline-preview {
  margin: 8px 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.outline-chapter-row {
  display: flex;
  gap: 8px;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}
.outline-ch-num {
  flex-shrink: 0;
  font-weight: 600;
  color: var(--color-text-primary);
}
.outline-ch-title {
  font-weight: 500;
}
.outline-ch-goal {
  opacity: 0.7;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.outline-more {
  font-size: 0.8rem;
  color: var(--color-text-tertiary);
  padding-top: 2px;
}
</style>
