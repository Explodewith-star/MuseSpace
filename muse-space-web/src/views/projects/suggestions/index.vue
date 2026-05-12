<script setup lang="ts">
import AppBadge from '@/components/base/AppBadge.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppCheckbox from '@/components/base/AppCheckbox.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppFilterChip from '@/components/base/AppFilterChip.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import { computed, ref } from 'vue'
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
import type { AgentSuggestionResponse, SuggestionStatus } from '@/types/models'

const route = useRoute()
const router = useRouter()
const projectId = route.params.id as string

const {
  suggestions,
  filteredSuggestions,
  loading,
  filterCategory,
  filterStatus,
  selectedIds,
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

type OutlineDeleteScope = 'single' | 'batch' | 'all'
type WorkbenchKey = 'pending' | 'assets' | 'consistency' | 'memory' | 'history'

const outlineDeleteModalOpen = ref(false)
const outlineDeleteScope = ref<OutlineDeleteScope>('single')
const outlineDeleteTargetId = ref<string>('')
const outlineDeleteHasChapters = ref(false)
const activeWorkbench = ref<WorkbenchKey>('pending')
const expandedGroups = ref<string[]>([])

const assetCategories = ['Character', 'WorldRule', 'StyleProfile', 'Outline']
const consistencyCategories = [
  'CharacterConsistency',
  'StyleConsistency',
  'WorldRuleConsistency',
  'OutlineConsistency',
  'Consistency',
]
const memoryCategories = ['CanonFact', 'CanonEvent', 'PlotThread', 'ProjectSummary']

const categoryOptions = [
  { value: '', label: '全部类型' },
  { value: 'CharacterConsistency', label: '角色冲突' },
  { value: 'StyleConsistency', label: '文风偏离' },
  { value: 'WorldRuleConsistency', label: '世界观冲突' },
  { value: 'CanonFact', label: '剧情事实' },
  { value: 'CanonEvent', label: '章节事件' },
  { value: 'PlotThread', label: '伏笔追踪' },
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

const scopedSuggestions = computed(() => {
  return filteredSuggestions.value.filter((s) => {
    if (activeWorkbench.value === 'pending') return s.status === 'Pending'
    if (activeWorkbench.value === 'assets') return assetCategories.includes(s.category)
    if (activeWorkbench.value === 'consistency') return consistencyCategories.includes(s.category)
    if (activeWorkbench.value === 'memory') return memoryCategories.includes(s.category)
    return s.status !== 'Pending'
  })
})

const categoryCounts = computed(() => {
  return suggestions.value.reduce<Record<string, number>>((map, s) => {
    map[s.category] = (map[s.category] ?? 0) + 1
    return map
  }, {})
})

const statusCounts = computed(() => {
  return suggestions.value.reduce<Record<string, number>>((map, s) => {
    map[s.status] = (map[s.status] ?? 0) + 1
    return map
  }, {})
})

const workbenchOptions = computed(() => [
  {
    key: 'pending' as const,
    label: '待我处理',
    icon: 'i-lucide-inbox',
    count: statusCounts.value.Pending ?? 0,
  },
  {
    key: 'assets' as const,
    label: '资产入库',
    icon: 'i-lucide-box',
    count: suggestions.value.filter((s) => assetCategories.includes(s.category)).length,
  },
  {
    key: 'consistency' as const,
    label: '一致性审查',
    icon: 'i-lucide-shield-alert',
    count: suggestions.value.filter((s) => consistencyCategories.includes(s.category)).length,
  },
  {
    key: 'memory' as const,
    label: '剧情记忆',
    icon: 'i-lucide-bookmark',
    count: suggestions.value.filter((s) => memoryCategories.includes(s.category)).length,
  },
  {
    key: 'history' as const,
    label: '处理记录',
    icon: 'i-lucide-archive',
    count: suggestions.value.filter((s) => s.status !== 'Pending').length,
  },
])

const groupedSuggestions = computed(() => {
  const groups = [
    {
      key: 'memory',
      title: '剧情记忆与伏笔',
      desc: '会写入剧情事实、章节事件或伏笔看板，影响后续上下文注入。',
      categories: memoryCategories,
    },
    {
      key: 'consistency',
      title: '一致性审查',
      desc: '角色、世界观与文风偏离，通常需要结合当前章节判断。',
      categories: consistencyCategories,
    },
    {
      key: 'assets',
      title: '候选资产',
      desc: '从原著中提取出的角色、世界观规则、文风画像或大纲草案。',
      categories: assetCategories,
    },
    {
      key: 'other',
      title: '其他建议',
      desc: '暂未归入固定工作流的 AI 输出。',
      categories: [] as string[],
    },
  ]
  const known = [...memoryCategories, ...consistencyCategories, ...assetCategories]
  return groups
    .map((group) => ({
      ...group,
      items: scopedSuggestions.value.filter((s) =>
        group.key === 'other' ? !known.includes(s.category) : group.categories.includes(s.category),
      ),
    }))
    .filter((group) => group.items.length > 0)
})

const highRiskCount = computed(() =>
  suggestions.value.filter((s) => {
    const content = parseContentJson(s.contentJson)
    return s.status === 'Pending' && content.severity === 'high'
  }).length,
)

const visibleSelectedCount = computed(
  () => scopedSuggestions.value.filter((s) => selectedIds.value.has(s.id)).length,
)

const allScopedSelected = computed(() => {
  const visible = scopedSuggestions.value
  return visible.length > 0 && visible.every((s) => selectedIds.value.has(s.id))
})

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

function getCategoryLabel(category: string): string {
  return CATEGORY_LABELS[category] ?? category
}

function getCategoryIcon(category: string): string {
  return CATEGORY_ICONS[category] ?? 'i-lucide-bot'
}

function getContent(s: AgentSuggestionResponse) {
  return parseContentJson(s.contentJson)
}

function getPrimaryActionLabel(s: AgentSuggestionResponse): string {
  if (s.category === 'Character') return '加入角色库'
  if (s.category === 'WorldRule') return '加入世界观'
  if (s.category === 'StyleProfile') return '应用文风'
  if (s.category === 'CanonFact') return '记录事实'
  if (s.category === 'CanonEvent') return '记录事件'
  if (s.category === 'PlotThread') return '写入伏笔'
  if (consistencyCategories.includes(s.category)) return '标记已确认'
  return '接受'
}

function getSuggestionSummary(s: AgentSuggestionResponse): string {
  const content = getContent(s)
  if (typeof content.suggestion === 'string') return content.suggestion
  if (typeof content.explanation === 'string') return content.explanation
  if (typeof content.description === 'string') return content.description
  if (typeof content.personalitySummary === 'string') return content.personalitySummary
  if (typeof content.tone === 'string') return content.tone
  if (memoryCategories.includes(s.category)) {
    return '该建议包含结构化结果，接受后会写入对应的剧情记忆或追踪模块。'
  }
  return '打开详情或接受后，可将该 AI 结果并入项目工作流。'
}

function setCategory(value: string) {
  filterCategory.value = value
}

function setStatus(value: string) {
  filterStatus.value = value as SuggestionStatus | ''
}

function toggleScopedSelectAll() {
  if (allScopedSelected.value) {
    scopedSuggestions.value.forEach((s) => selectedIds.value.delete(s.id))
  } else {
    scopedSuggestions.value.forEach((s) => selectedIds.value.add(s.id))
  }
}

function isGroupExpanded(key: string): boolean {
  return expandedGroups.value.includes(key)
}

function visibleGroupItems(group: { key: string; items: AgentSuggestionResponse[] }) {
  return isGroupExpanded(group.key) ? group.items : group.items.slice(0, 8)
}

function toggleGroupExpanded(key: string) {
  if (isGroupExpanded(key)) {
    expandedGroups.value = expandedGroups.value.filter((item) => item !== key)
  } else {
    expandedGroups.value = [...expandedGroups.value, key]
  }
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <div>
        <h2 class="page__title">AI 审核台</h2>
        <p class="page__subtitle">
          汇总 AI 生成的资产、剧情记忆和一致性审查结果。优先处理待确认内容，已应用记录可在这里追溯。
        </p>
      </div>
      <div class="header-actions">
        <AppButton variant="ghost" size="sm" @click="loadSuggestions">
          <i class="i-lucide-refresh-cw" />
          刷新
        </AppButton>
        <AppButton
          v-if="filterCategory === 'Outline' && scopedSuggestions.length > 0"
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

    <section class="review-summary">
      <div class="review-metric review-metric--primary">
        <span class="review-metric__value">{{ pendingCount }}</span>
        <span class="review-metric__label">待处理</span>
      </div>
      <div class="review-metric">
        <span class="review-metric__value">{{ highRiskCount }}</span>
        <span class="review-metric__label">高优先级</span>
      </div>
      <div class="review-metric">
        <span class="review-metric__value">{{ statusCounts.Applied ?? 0 }}</span>
        <span class="review-metric__label">已应用</span>
      </div>
      <div class="review-metric">
        <span class="review-metric__value">{{ suggestions.length }}</span>
        <span class="review-metric__label">全部记录</span>
      </div>
    </section>

    <section class="workbench-tabs" aria-label="审核工作台分类">
      <AppFilterChip
        v-for="opt in workbenchOptions"
        :key="opt.key"
        :active="activeWorkbench === opt.key"
        :count="opt.count"
        :icon="opt.icon"
        @click="activeWorkbench = opt.key"
      >
        {{ opt.label }}
      </AppFilterChip>
    </section>

    <section class="filter-panel">
      <div class="filter-panel__row">
        <span class="filter-panel__label">类型</span>
        <div class="filter-chips">
          <AppFilterChip
            v-for="opt in categoryOptions"
            :key="opt.value"
            :active="filterCategory === opt.value"
            :count="opt.value ? categoryCounts[opt.value] ?? 0 : suggestions.length"
            @click="setCategory(opt.value)"
          >
            {{ opt.label }}
          </AppFilterChip>
        </div>
      </div>
      <div class="filter-panel__row">
        <span class="filter-panel__label">状态</span>
        <div class="filter-chips">
          <AppFilterChip
            v-for="opt in statusOptions"
            :key="opt.value"
            :active="filterStatus === opt.value"
            :count="opt.value ? statusCounts[opt.value] ?? 0 : suggestions.length"
            @click="setStatus(opt.value)"
          >
            {{ opt.label }}
          </AppFilterChip>
        </div>
      </div>
    </section>

    <div v-if="selectedIds.size > 0" class="batch-bar">
      <span class="batch-info">已选 {{ selectedIds.size }} 条，当前视图 {{ visibleSelectedCount }} 条</span>
      <template v-if="batchLoading">
        <span class="batch-processing">
          <i class="i-lucide-loader-circle batch-spinner" />
          处理中，请稍候…
        </span>
      </template>
      <template v-else>
        <template v-if="filterCategory === 'Outline'">
          <AppButton size="sm" variant="ghost" :disabled="batchLoading" @click="openOutlineDeleteConfirm('batch')">
            <i class="i-lucide-trash-2" />
            批量删除
          </AppButton>
        </template>
        <template v-else>
          <AppButton v-if="hasPendingSelected || hasIgnoredSelected" size="sm" :loading="batchLoading" :disabled="batchLoading" @click="batchApply">
            <i class="i-lucide-zap" />
            批量应用
          </AppButton>
          <AppButton v-if="hasPendingSelected || hasAppliedSelected" variant="ghost" size="sm" :loading="batchLoading" :disabled="batchLoading" @click="batchIgnore">
            <i class="i-lucide-x" />
            批量忽略
          </AppButton>
          <AppButton v-if="hasIgnoredSelected" variant="ghost" size="sm" :loading="batchLoading" :disabled="batchLoading" @click="batchDelete">
            <i class="i-lucide-trash-2" />
            批量删除
          </AppButton>
        </template>
      </template>
    </div>

    <div v-if="loading" class="review-board">
      <div class="review-main">
        <div v-for="i in 3" :key="i" class="suggestion-card skeleton-card">
          <AppSkeleton width="60%" height="16px" />
          <AppSkeleton width="100%" height="12px" style="margin-top: 8px" />
          <AppSkeleton width="80%" height="12px" style="margin-top: 6px" />
        </div>
      </div>
    </div>

    <div v-else-if="!scopedSuggestions.length" class="empty-wrapper">
      <AppEmpty
        icon="i-lucide-inbox"
        title="当前视图暂无建议"
        description="可以切换工作台分类或状态筛选，也可以触发一次一致性检查。"
      >
        <template #action>
          <AppButton @click="checkModalOpen = true">
            <i class="i-lucide-shield-check" />
            触发一致性检查
          </AppButton>
        </template>
      </AppEmpty>
    </div>

    <div v-else class="review-board">
      <main class="review-main">
        <div class="select-all-row">
          <AppCheckbox
            :checked="allScopedSelected"
            :label="`全选当前视图 ${scopedSuggestions.length} 条`"
            @change="toggleScopedSelectAll"
          />
        </div>

        <section v-for="group in groupedSuggestions" :key="group.key" class="review-group">
          <div class="review-group__header">
            <div>
              <h3 class="review-group__title">{{ group.title }}</h3>
              <p class="review-group__desc">{{ group.desc }}</p>
            </div>
            <AppBadge variant="muted">{{ group.items.length }} 条</AppBadge>
          </div>

          <div class="suggestion-list">
            <div
              v-for="s in visibleGroupItems(group)"
              :key="s.id"
              :class="['suggestion-card', { selected: selectedIds.has(s.id) }]"
            >
              <div class="card-top">
                <div class="card-left">
                  <AppCheckbox size="sm" :checked="selectedIds.has(s.id)" @change="toggleSelect(s.id)" />
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
                  <div v-else class="outline-more">共 {{ parseOutlineChapters(s.contentJson).length }} 章</div>
                </div>

                <div class="card-actions">
                  <AppButton size="sm" @click="goOutlineDetail(s)">
                    <i class="i-lucide-list-tree" />
                    查看 / 编辑大纲
                  </AppButton>
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

              <template v-else-if="isExtractedAsset(s)">
                <div class="asset-preview">
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
                      <span>
                        {{ getContent(s).isHardConstraint ? '硬约束' : '软约束' }} · 优先级
                        {{ getContent(s).priority ?? '-' }}
                      </span>
                    </div>
                  </template>

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
              </template>

              <template v-else>
                <div v-if="memoryCategories.includes(s.category)" class="memory-preview">
                  <i class="i-lucide-bookmark" />
                  <span>{{ getSuggestionSummary(s) }}</span>
                </div>

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

                <div v-if="getContent(s).ruleName" class="card-rule">
                  <i class="i-lucide-bookmark" />
                  <span>涉及规则：{{ getContent(s).ruleName }}</span>
                </div>

                <div v-if="getContent(s).characterName" class="card-rule">
                  <i class="i-lucide-user" />
                  <span>涉及角色：{{ getContent(s).characterName }}</span>
                  <span v-if="getContent(s).conflictType" class="conflict-type-tag">
                    {{ getContent(s).conflictType }}
                  </span>
                </div>
              </template>

              <div class="card-actions">
                <AppButton
                  v-if="isExtractedAsset(s)"
                  variant="ghost"
                  size="sm"
                  @click="goAssetDetail(s)"
                >
                  <i class="i-lucide-pencil" />
                  查看/编辑
                </AppButton>
                <template v-if="s.status === 'Pending' && isExtractedAsset(s)">
                  <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="quickApply(s.id)">
                    <i class="i-lucide-zap" />
                    {{ getPrimaryActionLabel(s) }}
                  </AppButton>
                  <AppButton variant="ghost" size="sm" :loading="actionLoadingIds.has(s.id)" @click="ignore(s.id)">
                    <i class="i-lucide-trash-2" />
                    忽略
                  </AppButton>
                </template>
                <template v-else-if="canAccept(s)">
                  <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="accept(s.id)">
                    <i class="i-lucide-check" />
                    {{ getPrimaryActionLabel(s) }}
                  </AppButton>
                  <AppButton variant="ghost" size="sm" :loading="actionLoadingIds.has(s.id)" @click="ignore(s.id)">
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
                  <AppButton variant="ghost" size="sm" :loading="actionLoadingIds.has(s.id)" @click="ignore(s.id)">
                    <i class="i-lucide-undo-2" />
                    撤回
                  </AppButton>
                </template>
                <template v-else-if="s.status === 'Ignored'">
                  <AppButton size="sm" :loading="actionLoadingIds.has(s.id)" @click="reApply(s.id)">
                    <i class="i-lucide-rotate-ccw" />
                    重新应用
                  </AppButton>
                  <AppButton variant="ghost" size="sm" :loading="actionLoadingIds.has(s.id)" @click="deleteIgnored(s.id)">
                    <i class="i-lucide-trash-2" />
                    删除
                  </AppButton>
                </template>
                <span v-else class="resolved-time">
                  {{ s.resolvedAt ? new Date(s.resolvedAt).toLocaleString('zh-CN') : '' }}
                </span>
              </div>
            </div>
          </div>
          <div v-if="group.items.length > 8" class="group-more">
            <AppButton variant="ghost" size="sm" @click="toggleGroupExpanded(group.key)">
              {{ isGroupExpanded(group.key) ? '收起本组' : `展开剩余 ${group.items.length - 8} 条` }}
            </AppButton>
          </div>
        </section>
      </main>

      <aside class="review-side">
        <div class="side-panel">
          <h3>处理建议</h3>
          <p>优先确认高严重度的一致性问题；资产类建议建议回到角色、世界观、文风页查看上下文后再处理。</p>
          <div class="side-stat">
            <span>当前视图</span>
            <strong>{{ scopedSuggestions.length }}</strong>
          </div>
          <div class="side-stat">
            <span>已选</span>
            <strong>{{ visibleSelectedCount }}</strong>
          </div>
        </div>
      </aside>
    </div>

    <AppModal v-model="checkModalOpen" title="触发一致性检查" width="600px">
      <div class="check-modal-body">
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

    <AppModal v-model="outlineDeleteModalOpen" title="确认删除大纲" width="480px">
      <div class="delete-confirm-body">
        <div class="delete-confirm-icon">
          <i class="i-lucide-triangle-alert" />
        </div>
        <template v-if="outlineDeleteScope === 'single'">
          <p class="delete-confirm-title">即将删除这条大纲建议</p>
          <p v-if="outlineDeleteHasChapters" class="delete-confirm-desc">
            该大纲已被导入为章节，删除后，由该大纲生成的所有章节（包括章节内的计划、草稿、定稿）将一并永久删除，且无法恢复。
          </p>
          <p v-else class="delete-confirm-desc">删除后该大纲建议记录将被永久移除，无法恢复。</p>
        </template>
        <template v-else-if="outlineDeleteScope === 'batch'">
          <p class="delete-confirm-title">即将批量删除选中的大纲建议</p>
          <p class="delete-confirm-desc">
            其中已被导入为章节的大纲，对应的所有章节（包括计划、草稿、定稿）将一并永久删除，且无法恢复。
          </p>
        </template>
        <template v-else>
          <p class="delete-confirm-title">即将删除当前列表中所有大纲建议</p>
          <p class="delete-confirm-desc">
            所有已被导入为章节的大纲，对应的全部章节（包括计划、草稿、定稿）都将被永久删除。
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
  max-width: 1180px;
  margin: 0 auto;
}

.page__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}

.page__title {
  margin: 0;
  font-size: 22px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.page__subtitle {
  margin: 6px 0 0;
  max-width: 680px;
  font-size: 13px;
  line-height: 1.6;
  color: var(--color-text-muted);
}

.header-actions {
  display: flex;
  gap: 8px;
}

.review-summary {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
  margin-bottom: 14px;
}

.review-metric {
  padding: 12px 14px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
}

.review-metric--primary {
  border-color: color-mix(in srgb, var(--color-primary) 38%, var(--color-border));
  background: color-mix(in srgb, var(--color-primary) 8%, var(--color-bg-surface));
}

.review-metric__value {
  display: block;
  font-size: 20px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.review-metric__label {
  display: block;
  margin-top: 2px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.workbench-tabs,
.filter-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.workbench-tabs {
  margin-bottom: 12px;
}

.filter-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  margin-bottom: 14px;
}

.filter-panel__row {
  display: grid;
  grid-template-columns: 46px minmax(0, 1fr);
  gap: 10px;
  align-items: start;
}

.filter-panel__label {
  padding-top: 6px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.batch-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 12px;
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

.review-board {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 250px;
  gap: 16px;
  align-items: start;
}

.review-main {
  display: flex;
  flex-direction: column;
  gap: 14px;
  min-width: 0;
}

.review-side {
  position: sticky;
  top: 72px;
}

.side-panel {
  padding: 14px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
}

.side-panel h3 {
  margin: 0 0 8px;
  font-size: 14px;
  color: var(--color-text-primary);
}

.side-panel p {
  margin: 0 0 12px;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.6;
}

.side-stat {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 0;
  border-top: 1px solid var(--color-border);
  font-size: 12px;
  color: var(--color-text-muted);
}

.side-stat strong {
  color: var(--color-text-primary);
}

.empty-wrapper {
  margin-top: 16px;
}

.select-all-row {
  padding: 4px 2px;
}

.review-group {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  overflow: hidden;
}

.review-group__header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 16px;
  background: color-mix(in srgb, var(--color-bg-elevated) 46%, var(--color-bg-surface));
  border-bottom: 1px solid var(--color-border);
}

.review-group__title {
  margin: 0;
  font-size: 15px;
  color: var(--color-text-primary);
}

.review-group__desc {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--color-text-muted);
  line-height: 1.5;
}

.suggestion-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
}

.group-more {
  display: flex;
  justify-content: center;
  padding: 0 12px 12px;
}

.suggestion-card {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 14px;
  background: var(--color-bg-surface);
  transition: border-color 0.15s, background-color 0.15s;
}

.suggestion-card:hover {
  border-color: color-mix(in srgb, var(--color-primary) 32%, var(--color-border));
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
  margin-bottom: 10px;
}

.card-left {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.card-icon {
  font-size: 16px;
  color: var(--color-text-muted);
  flex-shrink: 0;
}

.card-title {
  font-size: 14px;
  font-weight: 600;
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

.memory-preview,
.conflict-snippet {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  padding: 10px 12px;
  border-radius: 8px;
  margin-bottom: 10px;
  font-size: 13px;
  line-height: 1.6;
}

.memory-preview {
  background: color-mix(in srgb, var(--color-primary) 6%, transparent);
  color: var(--color-text-secondary);
}

.conflict-snippet {
  background: color-mix(in srgb, var(--color-danger) 6%, transparent);
  border-left: 3px solid var(--color-danger);
  color: var(--color-text-secondary);
  font-style: italic;
}

.card-section {
  margin-bottom: 8px;
}

.section-label {
  font-size: 11px;
  font-weight: 700;
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

.card-rule {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin-bottom: 12px;
}

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

.asset-preview,
.outline-preview {
  display: flex;
  flex-direction: column;
  gap: 5px;
  margin: 6px 0 10px;
}

.asset-field,
.outline-volume-row {
  display: flex;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.field-label,
.outline-vol-num {
  flex-shrink: 0;
  font-weight: 700;
  color: var(--color-text-primary);
  min-width: 56px;
}

.outline-vol-title {
  flex: 1;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.outline-vol-meta,
.outline-more {
  font-size: 12px;
  color: var(--color-text-muted);
}

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
  justify-content: center;
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
}

.check-type-btn:hover,
.check-type-btn.active {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.check-hint {
  font-size: 13px;
  color: var(--color-text-muted);
  line-height: 1.6;
  margin: 0;
}

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

@media (max-width: 980px) {
  .review-summary {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .review-board {
    grid-template-columns: 1fr;
  }

  .review-side {
    position: static;
  }
}
</style>
