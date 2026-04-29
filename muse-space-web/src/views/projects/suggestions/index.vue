<script setup lang="ts">
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { ref } from 'vue'
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
  parseOutlineVolumes,
  isExtractedAsset,
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
} = initSuggestionsState()

// ── 大纲删除确认弹窗 ──────────────────────────────────────────
type OutlineDeleteScope = 'single' | 'batch' | 'all'
const outlineDeleteModalOpen = ref(false)
const outlineDeleteScope = ref<OutlineDeleteScope>('single')
const outlineDeleteTargetId = ref<string>('')
const outlineDeleteHasChapters = ref(false)

function openOutlineDeleteConfirm(scope: OutlineDeleteScope, id?: string, hasChapters = false) {
  outlineDeleteScope.value = scope
  outlineDeleteTargetId.value = id ?? ''
  outlineDeleteHasChapters.value = hasChapters
  outlineDeleteModalOpen.value = true
}

async function confirmOutlineDelete() {
  outlineDeleteModalOpen.value = false
  if (outlineDeleteScope.value === 'single') {
    await deleteOutline(outlineDeleteTargetId.value)
  } else if (outlineDeleteScope.value === 'batch') {
    await batchDeleteOutlines()
  } else {
    await deleteAllOutlines()
  }
}

function goOutlineDetail(s: AgentSuggestionResponse) {
  router.push(`/projects/${projectId}/suggestions/outline/${s.id}`)
}

function goAssetDetail(s: AgentSuggestionResponse) {
  router.push(`/projects/${projectId}/suggestions/asset/${s.id}`)
}

const categoryOptions = [
  { value: '', label: '全部类型' },
  { value: 'Consistency', label: '世界观一致性' },
  { value: 'Character', label: '角色' },
  { value: 'WorldRule', label: '世界观规则' },
  { value: 'StyleProfile', label: '文风画像' },
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
        <!-- 大纲页：全部删除 -->
        <AppButton
          v-if="filterCategory === 'Outline' && filteredSuggestions.length > 0"
          variant="ghost"
          size="sm"
          :disabled="batchLoading"
          @click="openOutlineDeleteConfirm('all')"
        >
          <i class="i-lucide-trash-2" />
          全部删除
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
          @click="filterStatus = opt.value as any"
        >
          {{ opt.label }}
        </button>
      </div>
    </div>

    <!-- 批量操作栏 -->
    <div v-if="selectedIds.size > 0" class="batch-bar">
      <span class="batch-info">已选 {{ selectedIds.size }} 条</span>
      <template v-if="batchLoading">
        <span class="batch-processing">
          <i class="i-lucide-loader-circle batch-spinner" />
          处理中，请稍候…
        </span>
      </template>
      <template v-else>
        <!-- 大纲页：批量删除（取代普通批量操作）-->
        <template v-if="filterCategory === 'Outline'">
          <AppButton
            size="sm"
            variant="ghost"
            :disabled="batchLoading"
            @click="openOutlineDeleteConfirm('batch')"
          >
            <i class="i-lucide-trash-2" />
            批量删除
          </AppButton>
        </template>
        <!-- 非大纲页：普通批量操作 -->
        <template v-else>
          <!-- 批量应用：待处理 → 已应用；已忽略 → 重新应用到已应用 -->
          <AppButton v-if="hasPendingSelected || hasIgnoredSelected" size="sm" :loading="batchLoading" :disabled="batchLoading" @click="batchApply">
            <i class="i-lucide-zap" />
            批量应用
          </AppButton>
          <!-- 批量忽略：待处理 / 已应用 → 已忽略 -->
          <AppButton v-if="hasPendingSelected || hasAppliedSelected" variant="ghost" size="sm" :loading="batchLoading" :disabled="batchLoading" @click="batchIgnore">
            <i class="i-lucide-x" />
            批量忽略
          </AppButton>
          <!-- 批量删除：仅已忽略 -->
          <AppButton v-if="hasIgnoredSelected" variant="ghost" size="sm" :loading="batchLoading" :disabled="batchLoading" @click="batchDelete">
            <i class="i-lucide-trash-2" />
            批量删除
          </AppButton>
        </template>
      </template>
    </div>

    <!-- 骨架屏 -->
    <div v-if="loading" class="suggestion-list">
      <div v-for="i in 3" :key="i" class="suggestion-card skeleton-card">
        <AppSkeleton width="60%" height="16px" />
        <AppSkeleton width="100%" height="12px" style="margin-top: 8px" />
        <AppSkeleton width="80%" height="12px" style="margin-top: 6px" />
      </div>
    </div>

    <!-- 空状态：根据筛选分类给出差异化引导 -->
    <div v-else-if="!filteredSuggestions.length" class="empty-wrapper">
      <!-- 大纲专属空状态 -->
      <template v-if="filterCategory === 'Outline'">
        <AppEmpty
          icon="i-lucide-list-tree"
          title="还没有大纲草案"
          description="前往章节页，点击「AI 规划大纲」，让 AI 按分卷结构规划整部故事框架"
        >
          <template #action>
            <AppButton @click="$router.push(`/projects/${projectId}/chapters`)">
              <i class="i-lucide-arrow-right" />
              前往章节页
            </AppButton>
          </template>
        </AppEmpty>
      </template>

      <!-- 角色/世界观/文风专属空状态 -->
      <template
        v-else-if="
          filterCategory === 'Character' ||
          filterCategory === 'WorldRule' ||
          filterCategory === 'StyleProfile'
        "
      >
        <AppEmpty
          icon="i-lucide-box"
          title="还没有提取到资产"
          description="先在「原著导入」页面上传 TXT 原著，点击「提取资产」即可自动识别角色、世界观与文风"
        >
          <template #action>
            <AppButton @click="$router.push(`/projects/${projectId}/novels`)">
              <i class="i-lucide-arrow-right" />
              前往原著导入
            </AppButton>
          </template>
        </AppEmpty>
      </template>

      <!-- 一致性检查专属空状态 -->
      <template v-else-if="filterCategory === 'Consistency'">
        <AppEmpty
          icon="i-lucide-shield-check"
          title="还没有一致性审查结果"
          description="草稿生成后会自动触发文风审查；也可手动点击触发，检查已有内容与角色/世界观的一致性"
        >
          <template #action>
            <AppButton @click="checkModalOpen = true">
              <i class="i-lucide-shield-check" />
              立即触发检查
            </AppButton>
          </template>
        </AppEmpty>
      </template>

      <!-- 默认空状态 -->
      <template v-else>
        <AppEmpty
          icon="i-lucide-inbox"
          title="暂无建议"
          description="触发一致性检查或等待 AI 任务完成后，建议将出现在这里"
        >
          <template #action>
            <AppButton @click="checkModalOpen = true">
              <i class="i-lucide-shield-check" />
              触发一致性检查
            </AppButton>
          </template>
        </AppEmpty>
      </template>
    </div>

    <!-- 建议列表 -->
    <div v-else class="suggestion-list">
      <!-- 全选行 -->
      <div class="select-all-row">
        <label class="checkbox-label">
          <input type="checkbox" :checked="allFilteredSelected" @change="toggleSelectAll" />
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
              :variant="SEVERITY_VARIANTS[getContent(s).severity as string] ?? 'default'"
            >
              {{ SEVERITY_LABELS[getContent(s).severity as string] ?? getContent(s).severity }}级
            </AppBadge>
            <AppBadge size="sm" :variant="STATUS_VARIANTS[s.status]">
              {{ STATUS_LABELS[s.status] }}
            </AppBadge>
          </div>
        </div>

        <!-- ─── 大纲类卡片内容（分卷预览）─── -->
        <template v-if="isOutline(s)">
          <div class="outline-preview">
            <div
              v-for="(vol, vi) in parseOutlineVolumes(s.contentJson).slice(0, 4)"
              :key="vi"
              class="outline-volume-row"
            >
              <span class="outline-vol-num">卷{{ vol.number }}</span>
              <span class="outline-vol-title">{{ vol.title }}</span>
              <span class="outline-vol-meta">{{ vol.chapters.length }}章</span>
            </div>
            <div v-if="parseOutlineVolumes(s.contentJson).length > 4" class="outline-more">
              ... 共 {{ parseOutlineVolumes(s.contentJson).length }} 卷 /
              {{ parseOutlineChapters(s.contentJson).length }} 章
            </div>
            <div v-else class="outline-more">
              共 {{ parseOutlineChapters(s.contentJson).length }} 章
            </div>
          </div>

          <div class="card-actions">
            <AppButton size="sm" @click="goOutlineDetail(s)">
              <i class="i-lucide-list-tree" />
              查看 / 编辑大纲
            </AppButton>
            <!-- 任意状态都可删除，Applied 状态会联动删除关联章节 -->
            <AppButton
              variant="ghost"
              size="sm"
              :disabled="batchLoading"
              @click="openOutlineDeleteConfirm('single', s.id, s.status === 'Applied')"
            >
              <i class="i-lucide-trash-2" />
              删除
            </AppButton>
          </div>
        </template>

        <!-- ─── 资产提取类卡片（角色/世界观/文风） ─── -->
        <template v-else-if="isExtractedAsset(s)">
          <div class="asset-preview">
            <!-- 角色候选 -->
            <template v-if="s.category === 'Character'">
              <div v-if="getContent(s).role" class="asset-field">
                <span class="field-label">身份</span>
                <span>{{ getContent(s).role }}</span>
              </div>
              <div v-if="getContent(s).personalitySummary" class="asset-field">
                <span class="field-label">性格</span>
                <span>{{ getContent(s).personalitySummary }}</span>
              </div>
              <div v-if="getContent(s).motivation" class="asset-field">
                <span class="field-label">动机</span>
                <span>{{ getContent(s).motivation }}</span>
              </div>
            </template>

            <!-- 世界观规则候选 -->
            <template v-else-if="s.category === 'WorldRule'">
              <div v-if="getContent(s).category" class="asset-field">
                <span class="field-label">类别</span>
                <span>{{ getContent(s).category }}</span>
              </div>
              <div v-if="getContent(s).description" class="asset-field">
                <span class="field-label">描述</span>
                <span>{{ getContent(s).description }}</span>
              </div>
              <div class="asset-field">
                <span class="field-label">约束</span>
                <span
                  >{{ getContent(s).isHardConstraint ? '硬约束' : '软约束' }} · 优先级
                  {{ getContent(s).priority ?? '-' }}</span
                >
              </div>
            </template>

            <!-- 文风画像候选 -->
            <template v-else-if="s.category === 'StyleProfile'">
              <div v-if="getContent(s).tone" class="asset-field">
                <span class="field-label">语调</span>
                <span>{{ getContent(s).tone }}</span>
              </div>
              <div v-if="getContent(s).sentenceLengthPreference" class="asset-field">
                <span class="field-label">句式</span>
                <span>{{ getContent(s).sentenceLengthPreference }}</span>
              </div>
              <div v-if="getContent(s).dialogueRatio" class="asset-field">
                <span class="field-label">对话占比</span>
                <span>{{ getContent(s).dialogueRatio }}</span>
              </div>
              <div v-if="getContent(s).descriptionDensity" class="asset-field">
                <span class="field-label">描写密度</span>
                <span>{{ getContent(s).descriptionDensity }}</span>
              </div>
            </template>
          </div>

          <!-- 操作按钮 -->
          <div class="card-actions">
            <AppButton variant="ghost" size="sm" @click="goAssetDetail(s)">
              <i class="i-lucide-pencil" />
              查看/编辑
            </AppButton>
            <template v-if="s.status === 'Pending'">
              <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="quickApply(s.id)">
                <i class="i-lucide-zap" />
                导入
              </AppButton>
              <AppButton
                variant="ghost"
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="ignore(s.id)"
              >
                <i class="i-lucide-trash-2" />
                忽略
              </AppButton>
            </template>
            <template v-else-if="canApply(s)">
              <!-- Accepted 状态（兼容旧数据） -->
              <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="apply(s.id)">
                <i class="i-lucide-zap" />
                导入到正式资产
              </AppButton>
            </template>
            <template v-else-if="s.status === 'Applied'">
              <AppButton
                variant="ghost"
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="ignore(s.id)"
              >
                <i class="i-lucide-undo-2" />
                撤回
              </AppButton>
            </template>
            <template v-else-if="s.status === 'Ignored'">
              <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="reApply(s.id)">
                <i class="i-lucide-rotate-ccw" />
                重新导入
              </AppButton>
              <AppButton
                variant="ghost"
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="deleteIgnored(s.id)"
              >
                <i class="i-lucide-trash-2" />
                删除
              </AppButton>
            </template>
            <span v-else class="resolved-time">
              {{ s.resolvedAt ? new Date(s.resolvedAt).toLocaleString('zh-CN') : '' }}
            </span>
          </div>
        </template>

        <!-- ─── 一致性类卡片内容 ─── -->
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
              <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="accept(s.id)">
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
              <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="apply(s.id)">
                <i class="i-lucide-zap" />
                应用到正式内容
              </AppButton>
            </template>
            <template v-else-if="s.status === 'Applied'">
              <AppButton
                variant="ghost"
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="ignore(s.id)"
              >
                <i class="i-lucide-undo-2" />
                撤回
              </AppButton>
            </template>
            <template v-else-if="s.status === 'Ignored'">
              <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="reApply(s.id)">
                <i class="i-lucide-rotate-ccw" />
                重新应用
              </AppButton>
              <AppButton
                variant="ghost"
                size="sm"
                :loading="actionLoadingIds.has(s.id)"
                @click="deleteIgnored(s.id)"
              >
                <i class="i-lucide-trash-2" />
                删除
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
        <AppTextarea v-model="checkDraftText" placeholder="粘贴草稿内容..." :rows="10" />
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="checkModalOpen = false">取消</AppButton>
        <AppButton :loading="checkLoading" @click="submitConsistencyCheck">
          <i class="i-lucide-send" />
          提交检查
        </AppButton>
      </template>
    </AppModal>

    <!-- 大纲删除确认弹窗 -->
    <AppModal v-model="outlineDeleteModalOpen" title="确认删除大纲" width="480px">
      <div class="delete-confirm-body">
        <div class="delete-confirm-icon">
          <i class="i-lucide-triangle-alert" />
        </div>
        <template v-if="outlineDeleteScope === 'single'">
          <p class="delete-confirm-title">即将删除这条大纲建议</p>
          <p v-if="outlineDeleteHasChapters" class="delete-confirm-desc">
            ⚠️ 该大纲已被导入为章节，<strong>删除后，由该大纲生成的所有章节（包括章节内的计划、草稿、定稿）将一并永久删除</strong>，且无法恢复。
          </p>
          <p v-else class="delete-confirm-desc">
            删除后该大纲建议记录将被永久移除，无法恢复。
          </p>
        </template>
        <template v-else-if="outlineDeleteScope === 'batch'">
          <p class="delete-confirm-title">即将批量删除选中的大纲建议</p>
          <p class="delete-confirm-desc">
            ⚠️ 其中已被导入为章节的大纲，<strong>对应的所有章节（包括计划、草稿、定稿）将一并永久删除</strong>，且无法恢复。确认后操作不可撤销，请谨慎！
          </p>
        </template>
        <template v-else>
          <p class="delete-confirm-title">即将删除当前列表中所有大纲建议</p>
          <p class="delete-confirm-desc">
            ⚠️ 所有已被导入为章节的大纲，<strong>对应的全部章节（包括计划、草稿、定稿）都将被永久删除</strong>，操作不可撤销。如果你只是想清理记录但保留章节，请先手动撤回再删除。
          </p>
        </template>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="outlineDeleteModalOpen = false">取消</AppButton>
        <AppButton variant="danger" :loading="batchLoading" @click="confirmOutlineDelete">
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
  max-width: 900px;
  margin: 0 auto;
}

/* 大纲删除确认弹窗 */
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

.batch-processing {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--color-text-muted);
}

.batch-spinner {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* 列表 */
.empty-wrapper {
  margin-top: 8px;
}

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
.outline-chapter-row,
.outline-volume-row {
  display: flex;
  gap: 8px;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}
.outline-ch-num,
.outline-vol-num {
  flex-shrink: 0;
  font-weight: 600;
  color: var(--color-text-primary);
}
.outline-vol-title {
  flex: 1;
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.outline-vol-meta {
  flex-shrink: 0;
  font-size: 0.78rem;
  color: var(--color-text-tertiary);
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

/* ── 资产提取类预览 ── */
.asset-preview {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin: 6px 0;
}

.asset-field {
  display: flex;
  gap: 8px;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.asset-field .field-label {
  flex-shrink: 0;
  font-weight: 600;
  color: var(--color-text-primary);
  min-width: 56px;
}
</style>
