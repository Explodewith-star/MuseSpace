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
const selectedIds = ref(new Set<string>())
const batchDeleting = ref(false)
const guideVisible = ref(true)

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
const onlyActive = ref(false)
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
    Married: '已婚',
    Divorced: '离婚',
    Engaged: '订婚',
    Dating: '恋爱中',
    Blackmailing: '威胁勒索',
    Betrayed: '背叛',
    Ally: '同盟',
    Enemy: '对立',
    True: '是',
    False: '否',
    Active: '生效中',
    Known: '已知',
    Unknown: '未知',
  }
  if (labels[value]) return labels[value]
  // 未映射的英文：拆分 PascalCase 为空格分隔
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
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
  const newLocked = !f.isLocked
  await patchCanonFact(projectId.value, f.id, { isLocked: newLocked })
  toast.success(newLocked ? '已锁定' : '已解锁')
  const idx = facts.value.findIndex((item) => item.id === f.id)
  if (idx !== -1) facts.value[idx] = { ...facts.value[idx], isLocked: newLocked }
}

async function removeOne(f: CanonFactResponse) {
  if (!confirm(`确定删除事实「${f.factKey} = ${f.factValue}」？`)) return
  await deleteCanonFact(projectId.value, f.id)
  if (selectedFact.value?.id === f.id) detailOpen.value = false
  await refresh()
}

const allPageSelected = computed(
  () =>
    pagedFacts.value.length > 0 && pagedFacts.value.every((f) => selectedIds.value.has(f.id)),
)

function toggleSelect(id: string) {
  if (selectedIds.value.has(id)) {
    selectedIds.value.delete(id)
  } else {
    selectedIds.value.add(id)
  }
}

function toggleSelectAllPage() {
  if (allPageSelected.value) {
    pagedFacts.value.forEach((f) => selectedIds.value.delete(f.id))
  } else {
    pagedFacts.value.forEach((f) => selectedIds.value.add(f.id))
  }
}

async function batchDeleteSelected() {
  if (!confirm(`确定删除选中的 ${selectedIds.value.size} 条事实？`)) return
  batchDeleting.value = true
  try {
    await Promise.all([...selectedIds.value].map((id) => deleteCanonFact(projectId.value, id)))
    selectedIds.value.clear()
    toast.success('批量删除成功')
    await refresh()
  } finally {
    batchDeleting.value = false
  }
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <div>
        <h2 class="page__title">剧情记忆</h2>
        <p class="page__subtitle">记录 AI 从章节中提取的关键事实，约束后续创作、防止剧情矛盾。</p>
      </div>
      <AppButton @click="openCreate">
        <i class="i-lucide-plus" />
        新增事实
      </AppButton>
    </div>

    <div v-if="guideVisible" class="guide-panel">
      <div class="guide-panel__grid">
        <div class="guide-item">
          <i class="i-lucide-brain guide-item__icon guide-icon--primary" />
          <div>
            <p class="guide-item__title">什么是剧情记忆？</p>
            <p class="guide-item__body">AI 从每个章节自动抄取的关键事实：人物关系、生死状态、不可重复事件等。这些记忆会约束后续 AI 创作，防止前后矛盾。</p>
          </div>
        </div>
        <div class="guide-item">
          <i class="i-lucide-lock guide-item__icon guide-icon--lock" />
          <div>
            <p class="guide-item__title">锁定 = 剧情铁律</p>
            <p class="guide-item__body">AI 不会自动推翻或修改此事实，适合已确认无误、对剧情起决定性作用的关键事件。</p>
          </div>
        </div>
        <div class="guide-item">
          <i class="i-lucide-pencil guide-item__icon guide-icon--edit" />
          <div>
            <p class="guide-item__title">待确认 = 允许更新</p>
            <p class="guide-item__body">AI 可随新章节自动更新此事实，适合尚不确定的关系或状态。</p>
          </div>
        </div>
        <div class="guide-item">
          <i class="i-lucide-x-circle guide-item__icon guide-icon--invalid" />
          <div>
            <p class="guide-item__title">已失效 = 被推翻</p>
            <p class="guide-item__body">后续章节内容与该事实矛盾时自动标记为失效，不再约束 AI，但保留历史记录。</p>
          </div>
        </div>
      </div>
      <button class="guide-close" title="关闭引导" @click="guideVisible = false">
        <i class="i-lucide-x" />
      </button>
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

    <div v-if="selectedIds.size > 0" class="batch-bar">
      <span class="batch-info">已选 {{ selectedIds.size }} 条</span>
      <template v-if="batchDeleting">
        <span class="batch-processing">
          <i class="i-lucide-loader-circle batch-spinner" />
          删除中，请稍候…
        </span>
      </template>
      <template v-else>
        <AppButton size="sm" variant="ghost" :disabled="batchDeleting" @click="batchDeleteSelected">
          <i class="i-lucide-trash-2" />
          批量删除
        </AppButton>
        <AppButton size="sm" variant="ghost" @click="selectedIds.clear()">取消选择</AppButton>
      </template>
    </div>

    <section class="table-shell">
      <div class="table-shell__top">
        <div>
          <span class="table-title">事实列表</span>
          <span class="table-count">显示 {{ pageStart }}-{{ pageEnd }} / {{ filtered.length }} 条</span>
        </div>
        <span class="table-hint">点击任意行查看详情与备注</span>
      </div>

      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="filtered.length === 0" class="empty">
        没有符合条件的事实。可以调整筛选，或等待 AI 从章节中抽取新的剧情记忆。
      </div>
      <div v-else class="fact-table-wrap">
        <table class="fact-table">
          <thead>
            <tr>
              <th class="col-check">
                <AppCheckbox :checked="allPageSelected" @change="toggleSelectAllPage" />
              </th>
              <th class="col-type">类型</th>
              <th>事实描述</th>
              <th class="col-status">状态</th>
              <th class="col-confidence">可信度</th>
              <th class="col-actions">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="f in pagedFacts"
              :key="f.id"
              :class="{ 'row-invalidated': !!f.invalidatedByChapterId, 'row-selected': selectedIds.has(f.id) }"
              @click="openDetail(f)"
            >
              <td class="col-check" @click.stop>
                <AppCheckbox :checked="selectedIds.has(f.id)" @change="toggleSelect(f.id)" />
              </td>
              <td class="col-type">
                <AppBadge variant="primary">{{ factTypeLabel(f.factType) }}</AppBadge>
              </td>
              <td class="cell-description">
                <p class="cell-desc__primary">{{ f.notes || `${factSubject(f)} → ${factValueLabel(f.factValue)}` }}</p>
                <p v-if="f.notes" class="cell-desc__meta">{{ factSubject(f) }} → {{ factValueLabel(f.factValue) }}</p>
              </td>
              <td class="col-status">
                <AppBadge v-if="f.invalidatedByChapterId" variant="muted">
                  <i class="i-lucide-x-circle" /> 已失效
                </AppBadge>
                <AppBadge v-else-if="f.isLocked" variant="success">
                  <i class="i-lucide-lock-keyhole" /> 已确认
                </AppBadge>
                <AppBadge v-else variant="default">
                  <i class="i-lucide-pencil-line" /> 待确认
                </AppBadge>
              </td>
              <td class="col-confidence">{{ confidenceLabel(f.confidence) }}</td>
              <td class="col-actions" @click.stop>
                <div class="col-actions__inner">
                  <button
                    class="row-action"
                    :class="{ 'row-action--locked': f.isLocked }"
                    :title="f.isLocked ? '已锁定（点击解锁，允许 AI 更新此事实）' : '未锁定（点击锁定，防止 AI 自动修改）'"
                    @click="toggleLock(f)"
                  >
                    <i :class="f.isLocked ? 'i-lucide-lock-keyhole' : 'i-lucide-lock-open'" />
                  </button>
                  <button class="row-action" title="编辑事实" @click="openEdit(f)">
                    <i class="i-lucide-pencil" />
                  </button>
                  <button
                    class="row-action row-action--danger"
                    aria-label="删除事实"
                    title="删除事实"
                    @click="removeOne(f)"
                  >
                    <i class="i-lucide-trash-2" />
                  </button>
                </div>
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
          <AppBadge v-if="selectedFact.invalidatedByChapterId" variant="muted">
            <i class="i-lucide-x-circle" /> 已失效
          </AppBadge>
          <AppBadge v-else-if="selectedFact.isLocked" variant="success">
            <i class="i-lucide-lock-keyhole" /> 已确认（锁定）
          </AppBadge>
          <AppBadge v-else variant="default">
            <i class="i-lucide-pencil-line" /> 待确认
          </AppBadge>
        </div>

        <div class="detail-block">
          <span class="detail-label">事实描述</span>
          <p class="detail-copy">{{ selectedFact.notes || `${factSubject(selectedFact)} → ${factValueLabel(selectedFact.factValue)}` }}</p>
        </div>
        <div class="detail-grid">
          <div>
            <span class="detail-label">涉及对象</span>
            <p class="detail-value">{{ factSubject(selectedFact) }}</p>
          </div>
          <div>
            <span class="detail-label">AI 标注取值</span>
            <p class="detail-value">{{ factValueLabel(selectedFact.factValue) }}</p>
          </div>
        </div>
        <div class="detail-grid">
          <div>
            <span class="detail-label">置信度</span>
            <p class="detail-value">{{ confidenceLabel(selectedFact.confidence) }}</p>
          </div>
          <div>
            <span class="detail-label">状态</span>
            <p class="detail-value">{{ selectedFact.invalidatedByChapterId ? '已失效（被后续章节推翻）' : selectedFact.isLocked ? '已确认（AI 不会修改）' : '待确认（允许 AI 更新）' }}</p>
          </div>
        </div>
        <div class="detail-block detail-block--technical">
          <span class="detail-label">事实标识 <span class="detail-label-sub">（系统内部，供 AI 识别）</span></span>
          <code>{{ selectedFact.factKey }}</code>
        </div>
        <div class="detail-block detail-block--technical">
          <span class="detail-label">事实取值 <span class="detail-label-sub">（对应标识的状态值）</span></span>
          <code>{{ selectedFact.factValue }}</code>
        </div>
      </div>
      <template #footer>
        <AppButton
          v-if="selectedFact"
          variant="ghost"
          :title="selectedFact.isLocked ? '解锁后 AI 可根据新章节更新此事实' : '锁定后 AI 不会自动修改此事实'"
          @click="toggleLock(selectedFact)"
        >
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

.guide-panel {
  position: relative;
  padding: 16px 40px 16px 16px;
  background: color-mix(in srgb, var(--color-primary) 6%, var(--color-bg-surface));
  border: 1px solid color-mix(in srgb, var(--color-primary) 18%, transparent);
  border-radius: 10px;
  margin-bottom: 16px;
}

.guide-panel__grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.guide-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
}

.guide-item__icon {
  flex-shrink: 0;
  font-size: 18px;
  margin-top: 1px;
}

.guide-icon--primary {
  color: var(--color-primary);
}

.guide-icon--lock {
  color: var(--color-success);
}

.guide-icon--edit {
  color: var(--color-text-muted);
}

.guide-icon--invalid {
  color: var(--color-text-muted);
}

.guide-item__title {
  margin: 0 0 3px;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.guide-item__body {
  margin: 0;
  font-size: 12px;
  color: var(--color-text-muted);
  line-height: 1.6;
}

.guide-close {
  position: absolute;
  top: 10px;
  right: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  padding: 0;
  border: none;
  background: transparent;
  color: var(--color-text-muted);
  font-size: 15px;
  cursor: pointer;
  border-radius: 4px;
  transition: background-color 0.12s;
}

.guide-close:hover {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  color: var(--color-text-primary);
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
  min-width: 760px;
  border-collapse: separate;
  border-spacing: 0;
}

.fact-table th,
.fact-table td {
  padding: 12px 12px;
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
  width: 110px;
}

.cell-description {
  max-width: 520px;
}

.cell-desc__primary {
  margin: 0;
  color: var(--color-text-primary);
  font-size: 13px;
  line-height: 1.6;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
}

.cell-desc__meta {
  margin: 3px 0 0;
  color: var(--color-text-muted);
  font-size: 12px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.col-actions {
  position: sticky;
  right: 0;
  min-width: 110px;
  background: inherit;
  box-shadow: -6px 0 10px -4px color-mix(in srgb, var(--color-border) 60%, transparent);
}

.col-actions__inner {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 2px;
}

th.col-actions {
  z-index: 2;
  background: color-mix(in srgb, var(--color-bg-elevated) 54%, var(--color-bg-surface));
}

.row-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  padding: 0;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  font-size: 14px;
  flex-shrink: 0;
  transition: background 0.15s, color 0.15s;
}

.row-action:hover {
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.row-action--locked {
  color: var(--color-primary);
}

.row-action--locked:hover {
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  color: var(--color-primary);
}

.row-action--danger:hover {
  background: color-mix(in srgb, var(--color-danger) 12%, transparent);
  color: var(--color-danger);
}

.row-invalidated {
  opacity: 0.6;
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

.detail-label-sub {
  font-size: 11px;
  opacity: 0.75;
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
