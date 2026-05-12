<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import AppBadge from '@/components/base/AppBadge.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppCheckbox from '@/components/base/AppCheckbox.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppFilterChip from '@/components/base/AppFilterChip.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppSelect from '@/components/base/AppSelect.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import {
  getCanonFacts,
  createCanonFact,
  updateCanonFact,
  patchCanonFact,
  deleteCanonFact,
  type CanonFactResponse,
  type UpsertCanonFactRequest,
} from '@/api/canonFacts'
import { useToast } from '@/composables/useToast'

const route = useRoute()
const projectId = computed(() => route.params.id as string)
const toast = useToast()

const facts = ref<CanonFactResponse[]>([])
const loading = ref(false)

const FACT_TYPE_OPTIONS = [
  { value: '', label: '全部类型' },
  { value: 'Relationship', label: '关系' },
  { value: 'Identity', label: '身份' },
  { value: 'LifeStatus', label: '生死' },
  { value: 'WorldState', label: '世界状态' },
  { value: 'UniqueEvent', label: '不可重复事件' },
]

const pageSizeOptions = [
  { value: '20', label: '每页 20 条' },
  { value: '50', label: '每页 50 条' },
  { value: '100', label: '每页 100 条' },
]

const filterType = ref('')
const onlyActive = ref(true)
const onlyLocked = ref(false)
const searchQuery = ref('')
const currentPage = ref(1)
const pageSize = ref('20')

const modalOpen = ref(false)
const editing = ref<CanonFactResponse | null>(null)
const selectedFact = ref<CanonFactResponse | null>(null)
const detailOpen = ref(false)
const form = ref<UpsertCanonFactRequest>({
  factType: 'Relationship',
  factKey: '',
  factValue: '',
})

const typeCounts = computed(() => {
  return facts.value.reduce<Record<string, number>>((map, fact) => {
    map[fact.factType] = (map[fact.factType] ?? 0) + 1
    return map
  }, {})
})

const lockedCount = computed(() => facts.value.filter((fact) => fact.isLocked).length)
const invalidatedCount = computed(
  () => facts.value.filter((fact) => !!fact.invalidatedByChapterId).length,
)
const activeCount = computed(() => facts.value.length - invalidatedCount.value)

const filtered = computed(() => {
  let list = facts.value
  if (filterType.value) list = list.filter((f) => f.factType === filterType.value)
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.trim().toLowerCase()
    list = list.filter(
      (f) =>
        f.factKey.toLowerCase().includes(q) ||
        f.factValue.toLowerCase().includes(q) ||
        (f.notes ?? '').toLowerCase().includes(q) ||
        factSubject(f).toLowerCase().includes(q),
    )
  }
  return list
})

const pageSizeNumber = computed(() => Number(pageSize.value))
const totalPages = computed(() => Math.max(1, Math.ceil(filtered.value.length / pageSizeNumber.value)))
const pagedFacts = computed(() => {
  const start = (currentPage.value - 1) * pageSizeNumber.value
  return filtered.value.slice(start, start + pageSizeNumber.value)
})
const pageStart = computed(() => (filtered.value.length ? (currentPage.value - 1) * pageSizeNumber.value + 1 : 0))
const pageEnd = computed(() => Math.min(currentPage.value * pageSizeNumber.value, filtered.value.length))

watch([filterType, searchQuery, pageSize], () => {
  currentPage.value = 1
})

watch(totalPages, (value) => {
  if (currentPage.value > value) currentPage.value = value
})

async function refresh() {
  loading.value = true
  try {
    facts.value = await getCanonFacts(projectId.value, {
      onlyActive: onlyActive.value && !onlyLocked.value,
      onlyLocked: onlyLocked.value,
    })
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

function factTypeLabel(t: string): string {
  return FACT_TYPE_OPTIONS.find((o) => o.value === t)?.label ?? t
}

function factSubject(f: CanonFactResponse): string {
  const labels: Record<string, string> = {
    classroomDoorOpenedByEntity: '教室门被异常打开',
    entityBroadcast: '异常广播',
    redLight: '暗红灯光',
    abnormalDarkness: '异常黑暗',
    phoneSignalLoss: '手机信号中断',
    unnaturalDarkness: '非自然天黑',
    facelessEntity: '无脸校服人影',
    redDressFacelessGhost: '红裙无脸鬼',
  }
  const [, raw = f.factKey] = f.factKey.split(':')
  if (labels[raw]) return labels[raw]
  return raw.replace(/([a-z])([A-Z])/g, '$1 $2').replaceAll('-', ' - ')
}

function factValueLabel(value: string): string {
  const labels: Record<string, string> = {
    Alive: '存活',
    Dead: '死亡',
    Missing: '失踪',
    Civilian: '普通人',
    Awakened: '觉醒/具备能力',
    Companion: '同伴',
    Awareness_Shared: '共同察觉异常',
    Happened: '已发生',
    Manifested: '已显现',
  }
  return labels[value] ?? value
}

function confidenceLabel(confidence: number): string {
  return `${Math.round(confidence * 100)}%`
}

function toggleOnlyActive() {
  onlyActive.value = !onlyActive.value
  void refresh()
}

function toggleOnlyLocked() {
  onlyLocked.value = !onlyLocked.value
  void refresh()
}

function goPrevPage() {
  if (currentPage.value > 1) currentPage.value -= 1
}

function goNextPage() {
  if (currentPage.value < totalPages.value) currentPage.value += 1
}

function openCreate() {
  editing.value = null
  form.value = {
    factType: 'Relationship',
    factKey: '',
    factValue: '',
    confidence: 1,
    isLocked: true,
    notes: '',
  }
  modalOpen.value = true
}

function openEdit(f: CanonFactResponse) {
  editing.value = f
  form.value = {
    factType: f.factType,
    subjectId: f.subjectId,
    objectId: f.objectId,
    factKey: f.factKey,
    factValue: f.factValue,
    sourceChapterId: f.sourceChapterId,
    confidence: f.confidence,
    isLocked: f.isLocked,
    invalidatedByChapterId: f.invalidatedByChapterId,
    notes: f.notes,
  }
  modalOpen.value = true
}

function openDetail(f: CanonFactResponse) {
  selectedFact.value = f
  detailOpen.value = true
}

async function save() {
  if (!form.value.factType || !form.value.factKey.trim() || !form.value.factValue.trim()) {
    toast.error('类型 / Key / Value 不能为空')
    return
  }
  if (editing.value) {
    await updateCanonFact(projectId.value, editing.value.id, form.value)
  } else {
    await createCanonFact(projectId.value, form.value)
  }
  modalOpen.value = false
  toast.success('已保存')
  await refresh()
}

async function toggleLock(f: CanonFactResponse) {
  await patchCanonFact(projectId.value, f.id, { isLocked: !f.isLocked })
  toast.success(f.isLocked ? '已解锁' : '已锁定')
  await refresh()
}

async function removeOne(f: CanonFactResponse) {
  if (!confirm(`确定删除事实「${f.factKey} = ${f.factValue}」？`)) return
  await deleteCanonFact(projectId.value, f.id)
  if (selectedFact.value?.id === f.id) detailOpen.value = false
  await refresh()
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <div>
        <h2 class="page__title">剧情记忆</h2>
        <p class="page__subtitle">Canon 事实账本：记录会影响后续创作的一次性事件、人物状态与世界变化。</p>
      </div>
      <AppButton @click="openCreate">
        <i class="i-lucide-plus" />
        新增事实
      </AppButton>
    </div>

    <section class="memory-summary" aria-label="剧情记忆统计">
      <div class="summary-card">
        <span class="summary-card__value">{{ facts.length }}</span>
        <span class="summary-card__label">事实总数</span>
      </div>
      <div class="summary-card">
        <span class="summary-card__value">{{ activeCount }}</span>
        <span class="summary-card__label">当前生效</span>
      </div>
      <div class="summary-card">
        <span class="summary-card__value">{{ lockedCount }}</span>
        <span class="summary-card__label">已锁定</span>
      </div>
      <div class="summary-card">
        <span class="summary-card__value">{{ invalidatedCount }}</span>
        <span class="summary-card__label">已失效</span>
      </div>
    </section>

    <section class="toolbar">
      <div class="toolbar__main">
        <div class="search-box">
          <i class="i-lucide-search search-icon" />
          <input
            v-model="searchQuery"
            class="search-input"
            type="text"
            name="canon-fact-search"
            placeholder="搜索人物、事实、来源…"
          />
        </div>
        <AppSelect
          v-model="filterType"
          :options="FACT_TYPE_OPTIONS"
          :searchable="false"
          placeholder="全部类型"
          class="type-select"
        />
        <AppSelect
          v-model="pageSize"
          :options="pageSizeOptions"
          :searchable="false"
          class="size-select"
        />
      </div>
      <div class="toolbar__chips">
        <AppFilterChip :active="onlyActive" :count="activeCount" icon="i-lucide-circle-check" @click="toggleOnlyActive">
          仅看生效
        </AppFilterChip>
        <AppFilterChip :active="onlyLocked" :count="lockedCount" icon="i-lucide-lock" @click="toggleOnlyLocked">
          仅看锁定
        </AppFilterChip>
      </div>
    </section>

    <section class="type-strip" aria-label="事实类型筛选">
      <AppFilterChip
        v-for="opt in FACT_TYPE_OPTIONS"
        :key="opt.value"
        :active="filterType === opt.value"
        :count="opt.value ? typeCounts[opt.value] ?? 0 : facts.length"
        @click="filterType = opt.value"
      >
        {{ opt.label }}
      </AppFilterChip>
    </section>

    <section class="table-shell">
      <div class="table-shell__top">
        <div>
          <span class="table-title">事实列表</span>
          <span class="table-count">显示 {{ pageStart }}-{{ pageEnd }} / {{ filtered.length }} 条</span>
        </div>
        <span class="table-hint">点击任意行查看完整出处与内部 Key</span>
      </div>

      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="filtered.length === 0" class="empty">
        没有符合条件的事实。可以调整筛选，或等待 AI 从章节中抽取新的剧情记忆。
      </div>
      <div v-else class="fact-table-wrap">
        <table class="fact-table">
          <thead>
            <tr>
              <th class="col-type">类型</th>
              <th>对象</th>
              <th>事实</th>
              <th class="col-confidence">置信度</th>
              <th class="col-status">状态</th>
              <th>来源 / 说明</th>
              <th class="col-actions">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="f in pagedFacts"
              :key="f.id"
              :class="{ 'row-invalidated': !!f.invalidatedByChapterId }"
              @click="openDetail(f)"
            >
              <td class="col-type">
                <AppBadge variant="primary">{{ factTypeLabel(f.factType) }}</AppBadge>
              </td>
              <td class="cell-subject">{{ factSubject(f) }}</td>
              <td class="cell-value">{{ factValueLabel(f.factValue) }}</td>
              <td class="col-confidence">{{ confidenceLabel(f.confidence) }}</td>
              <td class="col-status">
                <AppBadge v-if="f.invalidatedByChapterId" variant="muted">已失效</AppBadge>
                <AppBadge v-else-if="f.isLocked" variant="danger">
                  <i class="i-lucide-lock" /> 锁定
                </AppBadge>
                <AppBadge v-else variant="accent">
                  <i class="i-lucide-unlock" /> 未锁
                </AppBadge>
              </td>
              <td class="cell-notes" :title="f.notes ?? ''">{{ f.notes ?? '暂无来源说明' }}</td>
              <td class="col-actions" @click.stop>
                <AppButton variant="ghost" size="sm" @click="toggleLock(f)">
                  {{ f.isLocked ? '解锁' : '锁定' }}
                </AppButton>
                <AppButton variant="ghost" size="sm" @click="openEdit(f)">编辑</AppButton>
                <AppButton variant="ghost" size="sm" @click="removeOne(f)" aria-label="删除事实">
                  <i class="i-lucide-trash-2" />
                </AppButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="pagination">
        <span>第 {{ currentPage }} / {{ totalPages }} 页</span>
        <div class="pagination__actions">
          <AppButton variant="ghost" size="sm" :disabled="currentPage <= 1" @click="goPrevPage">
            上一页
          </AppButton>
          <AppButton variant="ghost" size="sm" :disabled="currentPage >= totalPages" @click="goNextPage">
            下一页
          </AppButton>
        </div>
      </div>
    </section>

    <AppDrawer v-model="detailOpen" title="事实详情" width="520px">
      <div v-if="selectedFact" class="detail">
        <div class="detail__head">
          <AppBadge variant="primary">{{ factTypeLabel(selectedFact.factType) }}</AppBadge>
          <AppBadge v-if="selectedFact.isLocked" variant="danger">锁定</AppBadge>
          <AppBadge v-else variant="accent">未锁</AppBadge>
        </div>

        <div class="detail-block">
          <span class="detail-label">对象</span>
          <p class="detail-value">{{ factSubject(selectedFact) }}</p>
        </div>
        <div class="detail-block">
          <span class="detail-label">事实值</span>
          <p class="detail-value">{{ factValueLabel(selectedFact.factValue) }}</p>
        </div>
        <div class="detail-block">
          <span class="detail-label">出处 / 说明</span>
          <p class="detail-copy">{{ selectedFact.notes ?? '暂无来源说明' }}</p>
        </div>
        <div class="detail-grid">
          <div>
            <span class="detail-label">置信度</span>
            <p class="detail-value">{{ confidenceLabel(selectedFact.confidence) }}</p>
          </div>
          <div>
            <span class="detail-label">状态</span>
            <p class="detail-value">{{ selectedFact.invalidatedByChapterId ? '已失效' : '当前生效' }}</p>
          </div>
        </div>
        <div class="detail-block detail-block--technical">
          <span class="detail-label">内部 Key</span>
          <code>{{ selectedFact.factKey }}</code>
        </div>
        <div class="detail-block detail-block--technical">
          <span class="detail-label">内部 Value</span>
          <code>{{ selectedFact.factValue }}</code>
        </div>
      </div>
      <template #footer>
        <AppButton v-if="selectedFact" variant="ghost" @click="toggleLock(selectedFact)">
          {{ selectedFact.isLocked ? '解锁' : '锁定' }}
        </AppButton>
        <AppButton v-if="selectedFact" @click="openEdit(selectedFact)">编辑</AppButton>
      </template>
    </AppDrawer>

    <AppModal v-model="modalOpen" :title="editing ? '编辑事实' : '新增事实'">
      <div class="form-fields">
        <AppSelect
          v-model="form.factType"
          label="类型 *"
          :options="FACT_TYPE_OPTIONS.filter((o) => o.value)"
        />
        <AppInput
          v-model="form.factKey"
          label="FactKey *"
          placeholder="如 Relationship:A-B、Identity:A、UniqueEvent:proposal:A-B"
        />
        <AppInput
          v-model="form.factValue"
          label="FactValue *"
          placeholder="如 Engaged / Exposed / Alive / Happened"
        />
        <label class="field">
          置信度 (0-1)
          <input
            v-model.number="form.confidence"
            type="number"
            step="0.05"
            min="0"
            max="1"
            class="plain-input"
          />
        </label>
        <AppCheckbox v-model="form.isLocked" label="锁定为核心事实" hint="锁定后默认不会被后续抽取自动推翻" />
        <AppTextarea v-model="form.notes" label="备注 / 出处" :rows="3" />
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
  max-width: 1160px;
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
  font-size: 13px;
  color: var(--color-text-muted);
  line-height: 1.6;
}

.memory-summary {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
  margin-bottom: 14px;
}

.summary-card {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 12px 14px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
}

.summary-card__value {
  font-size: 20px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.summary-card__label {
  font-size: 12px;
  color: var(--color-text-muted);
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  margin-bottom: 10px;
}

.toolbar__main,
.toolbar__chips {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.search-box {
  position: relative;
  width: 280px;
  flex-shrink: 0;
}

.search-icon {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--color-text-muted);
  font-size: 14px;
  pointer-events: none;
}

.search-input {
  width: 100%;
  height: 34px;
  padding: 0 12px 0 32px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: 13px;
  outline: none;
  transition:
    border-color 0.15s,
    box-shadow 0.15s;
}

.search-input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary) 14%, transparent);
}

.type-select {
  width: 150px;
}

.size-select {
  width: 132px;
}

.type-strip {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 14px;
}

.table-shell {
  overflow: hidden;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
}

.table-shell__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--color-border);
}

.table-title {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.table-count,
.table-hint {
  margin-left: 8px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.fact-table-wrap {
  max-height: 560px;
  overflow: auto;
}

.fact-table {
  width: 100%;
  min-width: 980px;
  border-collapse: separate;
  border-spacing: 0;
}

.fact-table th,
.fact-table td {
  padding: 11px 12px;
  border-bottom: 1px solid color-mix(in srgb, var(--color-border) 72%, transparent);
  text-align: left;
  font-size: 13px;
  vertical-align: middle;
}

.fact-table th {
  position: sticky;
  top: 0;
  z-index: 1;
  background: color-mix(in srgb, var(--color-bg-elevated) 54%, var(--color-bg-surface));
  color: var(--color-text-primary);
  font-weight: 700;
}

.fact-table tbody tr {
  cursor: pointer;
  transition: background-color 0.12s;
}

.fact-table tbody tr:hover {
  background: color-mix(in srgb, var(--color-primary) 5%, transparent);
}

.col-type {
  width: 120px;
}

.col-confidence {
  width: 90px;
}

.col-status {
  width: 104px;
}

.cell-subject {
  max-width: 220px;
  font-weight: 600;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cell-value {
  max-width: 180px;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cell-notes {
  max-width: 360px;
  color: var(--color-text-muted);
  line-height: 1.5;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.col-actions {
  position: sticky;
  right: 0;
  display: flex;
  gap: 4px;
  justify-content: flex-end;
  min-width: 172px;
  background: inherit;
}

th.col-actions {
  background: color-mix(in srgb, var(--color-bg-elevated) 54%, var(--color-bg-surface));
}

.row-invalidated {
  opacity: 0.6;
}

.loading,
.empty {
  padding: 42px 24px;
  text-align: center;
  color: var(--color-text-muted);
  font-size: 13px;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  border-top: 1px solid var(--color-border);
  color: var(--color-text-muted);
  font-size: 12px;
}

.pagination__actions {
  display: flex;
  gap: 6px;
}

.detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.detail__head {
  display: flex;
  gap: 8px;
}

.detail-block,
.detail-grid > div {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.detail-label {
  font-size: 12px;
  color: var(--color-text-muted);
}

.detail-value {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 14px;
  line-height: 1.6;
}

.detail-copy {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 13px;
  line-height: 1.7;
}

.detail-block--technical {
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-bg-elevated) 48%, transparent);
}

code {
  color: var(--color-text-primary);
  font-size: 12px;
  overflow-wrap: anywhere;
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
  color: var(--color-text-primary);
}

.plain-input {
  padding: 7px 10px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: 13px;
}

@media (max-width: 920px) {
  .memory-summary {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .toolbar__main,
  .toolbar__chips {
    flex-wrap: wrap;
  }

  .search-box {
    width: 100%;
  }
}
</style>
