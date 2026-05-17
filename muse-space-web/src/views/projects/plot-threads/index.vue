<script setup lang="ts">
/**
 * D4-C 伏笔追踪：四列状态看板。
 * 列：Introduced（已埋伏）/ Active（推进中）/ PaidOff（已回收）/ Abandoned（已放弃）。
 * 支持：手动新增、编辑、删除；点击列内卡片可改状态；顶部 AgentLauncher 触发 plot-thread-scan。
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/base/AppCard.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppCheckbox from '@/components/base/AppCheckbox.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppSelect from '@/components/base/AppSelect.vue'
import AgentLauncher from '@/components/base/AgentLauncher.vue'
import {
  getPlotThreads,
  createPlotThread,
  updatePlotThread,
  deletePlotThread,
  type PlotThreadResponse,
  type PlotThreadStatus,
  type PlotThreadVisibility,
  type UpsertPlotThreadRequest,
} from '@/api/plotThreads'
import { getChapters } from '@/api/chapters'
import { useToast } from '@/composables/useToast'

const route = useRoute()
const projectId = computed(() => route.params.id as string)
const toast = useToast()

const threads = ref<PlotThreadResponse[]>([])
const loading = ref(false)
const maxChapterNumber = ref(0)
const selectedIds = ref(new Set<string>())
const batchDeleting = ref(false)

// ── 视图切换 & 搜索 ──
const viewMode = ref<'kanban' | 'table'>('kanban')
const searchQuery = ref('')

const VISIBILITY_OPTIONS: { value: PlotThreadVisibility; label: string; icon: string; tip: string }[] = [
  { value: 'ThisOutline', label: '番外局部', icon: 'i-lucide-box', tip: '仅在埋设它的批次内可见' },
  { value: 'Chain', label: '同链追踪', icon: 'i-lucide-link', tip: '在同一故事链的所有批次内可见（默认）' },
  { value: 'Project', label: '全书谜题', icon: 'i-lucide-globe', tip: '在整个项目内跨故事线可见' },
]

const STATUS_COLUMNS: { key: PlotThreadStatus; title: string; icon: string }[] = [
  { key: 'Introduced', title: '已埋伏', icon: 'i-lucide-sprout' },
  { key: 'Active', title: '推进中', icon: 'i-lucide-flame' },
  { key: 'PaidOff', title: '已回收', icon: 'i-lucide-check-circle-2' },
  { key: 'Abandoned', title: '已放弃', icon: 'i-lucide-archive' },
]

const filteredThreads = computed(() => {
  if (!searchQuery.value.trim()) return threads.value
  const q = searchQuery.value.trim().toLowerCase()
  return threads.value.filter(
    (t) =>
      t.title.toLowerCase().includes(q) ||
      (t.description ?? '').toLowerCase().includes(q) ||
      (t.tags ?? '').toLowerCase().includes(q),
  )
})

function listByStatus(s: PlotThreadStatus) {
  return filteredThreads.value.filter((t) => t.status === s)
}

// ── D4-1 过期提醒：状态为 Introduced/Active，且预期回收章号已被当前最新章号越过 ──
function isStale(t: PlotThreadResponse): boolean {
  if (t.status !== 'Introduced' && t.status !== 'Active') return false
  if (!t.expectedResolveByChapterNumber) return false
  return maxChapterNumber.value > t.expectedResolveByChapterNumber
}

const staleThreads = computed(() => threads.value.filter(isStale))

async function refresh() {
  loading.value = true
  try {
    const [list, chapters] = await Promise.all([
      getPlotThreads(projectId.value),
      getChapters(projectId.value).catch(() => []),
    ])
    threads.value = list
    maxChapterNumber.value = chapters.length === 0 ? 0 : Math.max(...chapters.map((c) => c.number))
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

// ── 编辑模态 ────────────────────────────────────────────────
const modalOpen = ref(false)
const editing = ref<PlotThreadResponse | null>(null)
const form = ref<UpsertPlotThreadRequest>({ title: '' })

function openCreate() {
  editing.value = null
  form.value = { title: '', description: '', importance: 'Medium', status: 'Introduced', visibility: 'Chain' }
  modalOpen.value = true
}

function openEdit(t: PlotThreadResponse) {
  editing.value = t
  form.value = {
    title: t.title,
    description: t.description,
    importance: t.importance ?? 'Medium',
    status: t.status,
    tags: t.tags,
    expectedResolveByChapterNumber: t.expectedResolveByChapterNumber,
    plantedInChapterId: t.plantedInChapterId,
    resolvedInChapterId: t.resolvedInChapterId,
    relatedCharacterIds: t.relatedCharacterIds,
    visibility: t.visibility ?? 'Chain',
  }
  modalOpen.value = true
}

async function save() {
  if (!form.value.title.trim()) {
    toast.error('标题不能为空')
    return
  }
  if (editing.value) {
    await updatePlotThread(projectId.value, editing.value.id, form.value)
  } else {
    await createPlotThread(projectId.value, form.value)
  }
  modalOpen.value = false
  toast.success('已保存')
  refresh()
}

async function changeStatus(t: PlotThreadResponse, s: PlotThreadStatus) {
  if (t.status === s) return
  await updatePlotThread(projectId.value, t.id, {
    title: t.title,
    description: t.description,
    importance: t.importance,
    status: s,
    plantedInChapterId: t.plantedInChapterId,
    resolvedInChapterId: t.resolvedInChapterId,
    relatedCharacterIds: t.relatedCharacterIds,
    expectedResolveByChapterNumber: t.expectedResolveByChapterNumber,
    tags: t.tags,
    visibility: t.visibility,
  })
  toast.success('状态已更新')
  refresh()
}

async function removeOne(t: PlotThreadResponse) {
  if (!confirm(`确定删除线索"${t.title}"？`)) return
  await deletePlotThread(projectId.value, t.id)
  refresh()
}

const allSelected = computed(
  () =>
    filteredThreads.value.length > 0 &&
    filteredThreads.value.every((t) => selectedIds.value.has(t.id)),
)

function toggleSelect(id: string) {
  if (selectedIds.value.has(id)) {
    selectedIds.value.delete(id)
  } else {
    selectedIds.value.add(id)
  }
}

function toggleSelectAll() {
  if (allSelected.value) {
    filteredThreads.value.forEach((t) => selectedIds.value.delete(t.id))
  } else {
    filteredThreads.value.forEach((t) => selectedIds.value.add(t.id))
  }
}

async function batchDelete() {
  if (!confirm(`确定删除选中的 ${selectedIds.value.size} 条线索？`)) return
  batchDeleting.value = true
  try {
    await Promise.all([...selectedIds.value].map((id) => deletePlotThread(projectId.value, id)))
    selectedIds.value.clear()
    toast.success('批量删除成功')
    refresh()
  } finally {
    batchDeleting.value = false
  }
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">伏笔追踪</h2>
      <div class="header-actions">
        <div class="search-box">
          <i class="i-lucide-search search-icon" />
          <input
            v-model="searchQuery"
            class="search-input"
            type="text"
            placeholder="搜索伏笔…"
          />
        </div>
        <div class="view-toggle">
          <button
            class="view-toggle-btn"
            :class="{ active: viewMode === 'kanban' }"
            title="看板视图"
            @click="viewMode = 'kanban'"
          >
            <i class="i-lucide-kanban" />
          </button>
          <button
            class="view-toggle-btn"
            :class="{ active: viewMode === 'table' }"
            title="表格视图"
            @click="viewMode = 'table'"
          >
            <i class="i-lucide-table-2" />
          </button>
        </div>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          新增线索
        </AppButton>
      </div>
    </div>

    <AgentLauncher
      class="agent-launcher-block"
      :project-id="projectId"
      title="伏笔扫描 Agent"
      description="扫描已生成草稿，自动发现新埋伏 / 标记已回收的线索。结果直接写入下方看板。"
      :default-agent-type="'plot-thread-scan'"
      placeholder="可选：补充关注点，例如「重点关注主线伏笔」"
      suggestion-category="PlotThread"
      :presets="[{ label: '扫描全部草稿', agentType: 'plot-thread-scan', icon: 'i-lucide-search' }]"
      @done="refresh()"
    />

    <!-- D4-1 过期提醒 banner -->
    <div v-if="staleThreads.length > 0" class="stale-banner">
      <i class="i-lucide-alert-triangle" />
      <div class="stale-banner__text">
        <strong>{{ staleThreads.length }}</strong> 条伏笔预期回收章号已超过当前最新章号
        <span class="stale-banner__hint">（当前最新：第 {{ maxChapterNumber }} 章）</span>
      </div>
    </div>

    <div v-if="selectedIds.size > 0" class="batch-bar">
      <span class="batch-info">已选 {{ selectedIds.size }} 条</span>
      <template v-if="batchDeleting">
        <span class="batch-processing">
          <i class="i-lucide-loader-circle batch-spinner" />
          删除中，请稍候…
        </span>
      </template>
      <template v-else>
        <AppButton size="sm" variant="ghost" :disabled="batchDeleting" @click="batchDelete">
          <i class="i-lucide-trash-2" />
          批量删除
        </AppButton>
        <AppButton size="sm" variant="ghost" @click="selectedIds.clear()">取消选择</AppButton>
      </template>
    </div>

    <!-- ═══ 看板视图 ═══ -->
    <div v-if="viewMode === 'kanban'" class="kanban">
      <div v-for="col in STATUS_COLUMNS" :key="col.key" class="kanban-col">
        <div class="kanban-head">
          <i :class="col.icon" />
          <span>{{ col.title }}</span>
          <span class="count">{{ listByStatus(col.key).length }}</span>
        </div>
        <div class="kanban-body">
          <div v-if="loading" class="loading">加载中...</div>
          <div v-else-if="listByStatus(col.key).length === 0" class="empty">暂无</div>
          <AppCard
            v-for="t in listByStatus(col.key)"
            :key="t.id"
            class="thread-card"
            :class="{ 'thread-card--stale': isStale(t), 'thread-card--selected': selectedIds.has(t.id) }"
          >
            <div class="card-head">
              <AppCheckbox :checked="selectedIds.has(t.id)" @change="toggleSelect(t.id)" />
              <span class="card-title">{{ t.title }}</span>
              <span v-if="t.importance" class="card-importance" :class="`imp-${t.importance.toLowerCase()}`">
                {{ t.importance }}
              </span>
              <span v-if="t.visibility && t.visibility !== 'Chain'" class="card-visibility" :class="`vis-${t.visibility.toLowerCase()}`" :title="VISIBILITY_OPTIONS.find(v => v.value === t.visibility)?.tip">
                <i :class="VISIBILITY_OPTIONS.find(v => v.value === t.visibility)?.icon" />
                {{ VISIBILITY_OPTIONS.find(v => v.value === t.visibility)?.label }}
              </span>
              <span v-if="isStale(t)" class="card-stale" title="预期回收章号已超过当前最新章号">
                <i class="i-lucide-alert-triangle" /> 过期
              </span>
            </div>
            <p v-if="t.description" class="card-desc">{{ t.description }}</p>
            <div v-if="t.expectedResolveByChapterNumber" class="card-meta">
              预期回收于第 {{ t.expectedResolveByChapterNumber }} 章
            </div>
            <div class="card-actions">
              <AppSelect
                :model-value="t.status"
                :options="STATUS_COLUMNS.map(c => ({ value: c.key, label: c.title }))"
                :searchable="false"
                class="inline-status-select"
                @update:model-value="changeStatus(t, $event as PlotThreadStatus)"
              />
              <AppButton variant="ghost" size="sm" @click="openEdit(t)">编辑</AppButton>
              <AppButton variant="ghost" size="sm" @click="removeOne(t)">
                <i class="i-lucide-trash-2" />
              </AppButton>
            </div>
          </AppCard>
        </div>
      </div>
    </div>

    <!-- ═══ 表格视图 ═══ -->
    <AppCard v-else class="table-card">
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="filteredThreads.length === 0" class="empty">
        {{ searchQuery.trim() ? '无匹配结果' : '暂无伏笔线索' }}
      </div>
      <table v-else class="thread-table">
        <thead>
          <tr>
            <th class="col-check">
              <AppCheckbox :checked="allSelected" @change="toggleSelectAll" />
            </th>
            <th>标题</th>
            <th>描述</th>
            <th>重要度</th>
            <th>状态</th>
            <th>可见性</th>
            <th>预期回收章</th>
            <th>标签</th>
            <th class="col-actions">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="t in filteredThreads"
            :key="t.id"
            :class="{ 'row-stale': isStale(t), 'row-selected': selectedIds.has(t.id) }"
          >
            <td class="col-check" @click.stop>
              <AppCheckbox :checked="selectedIds.has(t.id)" @change="toggleSelect(t.id)" />
            </td>
            <td class="cell-title">{{ t.title }}</td>
            <td class="cell-desc">{{ t.description ?? '—' }}</td>
            <td>
              <span v-if="t.importance" class="card-importance" :class="`imp-${t.importance.toLowerCase()}`">
                {{ t.importance }}
              </span>
            </td>
            <td>
              <AppSelect
                :model-value="t.status"
                :options="STATUS_COLUMNS.map(c => ({ value: c.key, label: c.title }))"
                :searchable="false"
                class="inline-status-select"
                @update:model-value="changeStatus(t, $event as PlotThreadStatus)"
              />
            </td>
            <td>
              <span v-if="t.visibility" class="card-visibility" :class="`vis-${t.visibility.toLowerCase()}`" :title="VISIBILITY_OPTIONS.find(v => v.value === t.visibility)?.tip">
                {{ VISIBILITY_OPTIONS.find(v => v.value === t.visibility)?.label }}
              </span>
            </td>
            <td>{{ t.expectedResolveByChapterNumber ?? '—' }}</td>
            <td class="cell-tags">{{ t.tags ?? '—' }}</td>
            <td class="col-actions">
              <AppButton variant="ghost" size="sm" @click="openEdit(t)">编辑</AppButton>
              <AppButton variant="ghost" size="sm" @click="removeOne(t)">
                <i class="i-lucide-trash-2" />
              </AppButton>
            </td>
          </tr>
        </tbody>
      </table>
    </AppCard>

    <AppModal v-model="modalOpen" :title="editing ? '编辑线索' : '新增线索'">
      <div class="form-fields">
        <AppInput v-model="form.title" label="标题 *" />
        <AppTextarea v-model="form.description" label="描述" :rows="4" />
        <div class="form-row">
          <AppSelect
            v-model="form.importance"
            label="重要度"
            :searchable="false"
            :options="[
              { value: 'High', label: 'High' },
              { value: 'Medium', label: 'Medium' },
              { value: 'Low', label: 'Low' },
            ]"
          />
          <AppSelect
            v-model="form.status"
            label="状态"
            :searchable="false"
            :options="STATUS_COLUMNS.map(c => ({ value: c.key, label: c.title }))"
          />
        </div>
        <AppSelect
          v-model="form.visibility"
          label="可见性范围"
          :searchable="false"
          :options="VISIBILITY_OPTIONS.map(v => ({ value: v.value, label: v.label + ' — ' + v.tip }))"
        />
        <AppInput v-model="form.tags" label="标签（逗号分隔）" />
        <label class="field">
          <span class="field-label">预期回收章号（可选）</span>
          <input
            v-model.number="form.expectedResolveByChapterNumber"
            type="number"
            min="1"
            class="field-select"
            placeholder="例如 80，超过则提示过期"
          />
        </label>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">取消</AppButton>
        <AppButton @click="save">保存</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<style scoped>
.page {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.page__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.page__title {
  font-size: 22px;
  font-weight: 600;
  margin: 0;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 8px;
  font-size: 14px;
  color: var(--color-text-muted, #999);
  pointer-events: none;
}

.search-input {
  padding: 5px 10px 5px 28px;
  border: 1px solid var(--color-border, #ddd);
  border-radius: 6px;
  font-size: 13px;
  width: 200px;
  outline: none;
  transition: border-color 0.15s;
  background: var(--color-bg, #fff);
}

.search-input:focus {
  border-color: var(--color-primary, #7c3aed);
}

.view-toggle {
  display: flex;
  border: 1px solid var(--color-border, #ddd);
  border-radius: 6px;
  overflow: hidden;
}

.view-toggle-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 30px;
  border: none;
  background: var(--color-bg, #fff);
  cursor: pointer;
  color: var(--color-text-muted, #999);
  font-size: 15px;
  transition: background-color 0.15s, color 0.15s;
}

.view-toggle-btn + .view-toggle-btn {
  border-left: 1px solid var(--color-border, #ddd);
}

.view-toggle-btn.active {
  background: var(--color-primary, #7c3aed);
  color: #fff;
}

.view-toggle-btn:not(.active):hover {
  background: var(--color-bg-muted, #f5f5f5);
}

.kanban {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.kanban-col {
  background: var(--color-bg-elevated);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  display: flex;
  flex-direction: column;
  min-height: 320px;
}

.kanban-head {
  padding: 10px 14px;
  border-bottom: 1px solid var(--color-border);
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
  font-size: 14px;
}

.count {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-text-secondary);
  background: var(--color-bg-muted);
  border-radius: 999px;
  padding: 2px 8px;
}

.kanban-body {
  padding: 10px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  flex: 1;
}

.loading,
.empty {
  text-align: center;
  color: var(--color-text-secondary);
  padding: 16px;
  font-size: 13px;
}

.thread-card {
  padding: 10px 12px;
}

.thread-card--stale {
  border-color: #f59e0b;
  background: #fffbeb;
}

.card-stale {
  font-size: 11px;
  color: #b45309;
  background: #fef3c7;
  border-radius: 4px;
  padding: 1px 6px;
  display: inline-flex;
  align-items: center;
  gap: 2px;
}

.card-meta {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-top: 4px;
}

.stale-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border: 1px solid #f59e0b;
  background: #fffbeb;
  color: #92400e;
  border-radius: 8px;
  font-size: 13px;
}

.stale-banner__hint {
  color: #b45309;
  margin-left: 4px;
}

.card-head {
  display: flex;
  align-items: center;
  gap: 6px;
}

.card-title {
  font-weight: 600;
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.card-importance {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
}

.card-visibility {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
}

.vis-thisoutline {
  background: rgba(99, 102, 241, 0.12);
  color: rgb(67, 56, 202);
}

.vis-project {
  background: rgba(16, 185, 129, 0.12);
  color: rgb(4, 120, 87);
}

.imp-high {
  background: rgba(220, 38, 38, 0.15);
  color: rgb(185, 28, 28);
}

.imp-medium {
  background: rgba(245, 158, 11, 0.15);
  color: rgb(180, 83, 9);
}

.imp-low {
  background: var(--color-bg-muted);
  color: var(--color-text-secondary);
}

.card-desc {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin: 6px 0;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-actions {
  display: flex;
  gap: 6px;
  align-items: center;
}

.inline-status-select {
  min-width: 100px;
  max-width: 140px;
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-width: 480px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field-label {
  font-size: 12px;
  color: var(--color-text-secondary);
}

/* ── 表格视图 ── */
.table-card {
  overflow: auto;
}

.thread-table {
  width: 100%;
  border-collapse: collapse;
}

.thread-table th,
.thread-table td {
  padding: 8px 10px;
  border-bottom: 1px solid var(--color-border, #eee);
  text-align: left;
  font-size: 13px;
}

.thread-table th {
  background: var(--surface-2, #f9f9fb);
  font-weight: 600;
  white-space: nowrap;
}

.cell-title {
  font-weight: 500;
  max-width: 200px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cell-desc {
  max-width: 280px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--color-text-secondary, #888);
}

.cell-tags {
  max-width: 160px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--color-text-secondary, #888);
}

.col-actions {
  display: flex;
  gap: 4px;
  justify-content: flex-end;
}

.row-stale {
  background: #fffbeb;
}

.row-selected {
  background: color-mix(in srgb, var(--color-primary) 5%, transparent);
}

.col-check {
  width: 32px;
  text-align: center;
  padding: 0 4px;
}

.batch-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 12px;
  background: color-mix(in srgb, var(--color-primary) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-primary) 20%, transparent);
  border-radius: 8px;
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

@keyframes spin {
  to { transform: rotate(360deg); }
}

.batch-spinner {
  animation: spin 1s linear infinite;
}

.thread-card--selected {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 5%, transparent);
}
</style>
