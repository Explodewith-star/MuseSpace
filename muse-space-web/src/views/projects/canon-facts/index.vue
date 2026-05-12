<script setup lang="ts">
/**
 * Module D 正典事实层：Canon Fact 管理页。
 * - 表格展示项目内全部事实，支持按类型筛选 / 仅看锁定 / 仅看活跃。
 * - 每行支持：锁定切换、删除、查看详情。
 * - 顶部新增按钮可手动录入。
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import AppCard from '@/components/base/AppCard.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSelect from '@/components/base/AppSelect.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppBadge from '@/components/base/AppBadge.vue'
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

const filterType = ref('')
const onlyActive = ref(true)
const onlyLocked = ref(false)
const searchQuery = ref('')

const filtered = computed(() => {
  let list = facts.value
  if (filterType.value) list = list.filter((f) => f.factType === filterType.value)
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.trim().toLowerCase()
    list = list.filter(
      (f) =>
        f.factKey.toLowerCase().includes(q) ||
        f.factValue.toLowerCase().includes(q) ||
        (f.notes ?? '').toLowerCase().includes(q),
    )
  }
  return list
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

// ── 编辑模态 ─────────────────────────────────────────────────────────
const modalOpen = ref(false)
const editing = ref<CanonFactResponse | null>(null)
const form = ref<UpsertCanonFactRequest>({
  factType: 'Relationship',
  factKey: '',
  factValue: '',
})

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
  refresh()
}

async function toggleLock(f: CanonFactResponse) {
  await patchCanonFact(projectId.value, f.id, { isLocked: !f.isLocked })
  toast.success(f.isLocked ? '已解锁' : '已锁定')
  refresh()
}

async function removeOne(f: CanonFactResponse) {
  if (!confirm(`确定删除事实「${f.factKey} = ${f.factValue}」？`)) return
  await deleteCanonFact(projectId.value, f.id)
  refresh()
}

function factTypeLabel(t: string): string {
  return FACT_TYPE_OPTIONS.find((o) => o.value === t)?.label ?? t
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">Canon 事实账本</h2>
      <AppButton @click="openCreate">
        <i class="i-lucide-plus" />
        新增事实
      </AppButton>
    </div>

    <AppCard class="filter-bar">
      <div class="filter-row">
        <div class="search-box">
          <i class="i-lucide-search search-icon" />
          <input
            v-model="searchQuery"
            class="search-input"
            type="text"
            placeholder="搜索 Key / Value / 备注…"
          />
        </div>
        <AppSelect
          v-model="filterType"
          label="类型"
          :options="FACT_TYPE_OPTIONS"
          :searchable="false"
          placeholder="全部类型"
          class="filter-type-select"
        />
        <label class="filter-item">
          <input v-model="onlyActive" type="checkbox" @change="refresh" />
          仅显示活跃（未失效）
        </label>
        <label class="filter-item">
          <input v-model="onlyLocked" type="checkbox" @change="refresh" />
          仅显示已锁定
        </label>
        <span v-if="filtered.length !== facts.length" class="filter-count">
          {{ filtered.length }} / {{ facts.length }} 条
        </span>
      </div>
    </AppCard>

    <AppCard class="table-card">
      <div v-if="loading" class="loading">加载中...</div>
      <div v-else-if="filtered.length === 0" class="empty">
        暂无事实记录。可以等待 AI 自动抽取，也可以点击右上角手动录入。
      </div>
      <table v-else class="fact-table">
        <thead>
          <tr>
            <th>类型</th>
            <th>Key</th>
            <th>Value</th>
            <th>置信度</th>
            <th>状态</th>
            <th>来源</th>
            <th class="col-actions">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="f in filtered"
            :key="f.id"
            :class="{ 'row-invalidated': !!f.invalidatedByChapterId }"
          >
            <td>
              <AppBadge variant="primary">{{ factTypeLabel(f.factType) }}</AppBadge>
            </td>
            <td class="cell-key">{{ f.factKey }}</td>
            <td class="cell-value">{{ f.factValue }}</td>
            <td>{{ (f.confidence * 100).toFixed(0) }}%</td>
            <td>
              <AppBadge v-if="f.invalidatedByChapterId" variant="muted">已失效</AppBadge>
              <AppBadge v-else-if="f.isLocked" variant="danger">
                <i class="i-lucide-lock" /> 锁定
              </AppBadge>
              <AppBadge v-else variant="accent">
                <i class="i-lucide-unlock" /> 未锁
              </AppBadge>
            </td>
            <td class="cell-notes" :title="f.notes ?? ''">{{ f.notes ?? '—' }}</td>
            <td class="col-actions">
              <AppButton variant="ghost" size="sm" @click="toggleLock(f)">
                {{ f.isLocked ? '解锁' : '锁定' }}
              </AppButton>
              <AppButton variant="ghost" size="sm" @click="openEdit(f)">编辑</AppButton>
              <AppButton variant="ghost" size="sm" @click="removeOne(f)">
                <i class="i-lucide-trash-2" />
              </AppButton>
            </td>
          </tr>
        </tbody>
      </table>
    </AppCard>

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
        <label class="checkbox-row">
          <input v-model="form.isLocked" type="checkbox" />
          锁定（核心事实，不可被推翻）
        </label>
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
}
.page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}
.page__title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
}
.filter-bar {
  margin-bottom: 16px;
}
.filter-row {
  display: flex;
  gap: 24px;
  align-items: center;
  padding: 12px 16px;
}
.filter-item {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
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
  border: 1px solid var(--border-color, #ddd);
  border-radius: 6px;
  font-size: 13px;
  width: 220px;
  outline: none;
  transition: border-color 0.15s;
}
.search-input:focus {
  border-color: var(--color-primary, #7c3aed);
}
.filter-type-select {
  min-width: 120px;
}
.filter-count {
  font-size: 12px;
  color: var(--color-text-muted, #999);
  margin-left: auto;
}
.fact-table {
  width: 100%;
  border-collapse: collapse;
}
.fact-table th,
.fact-table td {
  padding: 8px 10px;
  border-bottom: 1px solid var(--border-color, #eee);
  text-align: left;
  font-size: 13px;
}
.fact-table th {
  background: var(--surface-2, #f9f9fb);
  font-weight: 600;
}
.cell-key {
  font-family: monospace;
  color: #555;
}
.cell-value {
  font-weight: 500;
}
.cell-notes {
  max-width: 240px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: #888;
}
.col-actions {
  display: flex;
  gap: 4px;
  justify-content: flex-end;
}
.row-invalidated {
  opacity: 0.55;
  text-decoration: line-through;
}
.loading,
.empty {
  padding: 32px;
  text-align: center;
  color: #888;
}
.form-fields {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.checkbox-row {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
}
.plain-input {
  padding: 6px 10px;
  border: 1px solid var(--border-color, #ddd);
  border-radius: 4px;
  font-size: 13px;
}
</style>
