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
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppModal from '@/components/base/AppModal.vue'
import AgentLauncher from '@/components/base/AgentLauncher.vue'
import {
  getPlotThreads,
  createPlotThread,
  updatePlotThread,
  deletePlotThread,
  type PlotThreadResponse,
  type PlotThreadStatus,
  type UpsertPlotThreadRequest,
} from '@/api/plotThreads'
import { useToast } from '@/composables/useToast'

const route = useRoute()
const projectId = computed(() => route.params.id as string)
const toast = useToast()

const threads = ref<PlotThreadResponse[]>([])
const loading = ref(false)

const STATUS_COLUMNS: { key: PlotThreadStatus; title: string; icon: string }[] = [
  { key: 'Introduced', title: '已埋伏', icon: 'i-lucide-sprout' },
  { key: 'Active', title: '推进中', icon: 'i-lucide-flame' },
  { key: 'PaidOff', title: '已回收', icon: 'i-lucide-check-circle-2' },
  { key: 'Abandoned', title: '已放弃', icon: 'i-lucide-archive' },
]

function listByStatus(s: PlotThreadStatus) {
  return threads.value.filter((t) => t.status === s)
}

async function refresh() {
  loading.value = true
  try {
    threads.value = await getPlotThreads(projectId.value)
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
  form.value = { title: '', description: '', importance: 'Medium', status: 'Introduced' }
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
    tags: t.tags,
  })
  toast.success('状态已更新')
  refresh()
}

async function removeOne(t: PlotThreadResponse) {
  if (!confirm(`确定删除线索"${t.title}"？`)) return
  await deletePlotThread(projectId.value, t.id)
  refresh()
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">伏笔追踪</h2>
      <AppButton @click="openCreate">
        <i class="i-lucide-plus" />
        新增线索
      </AppButton>
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

    <div class="kanban">
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
          >
            <div class="card-head">
              <span class="card-title">{{ t.title }}</span>
              <span v-if="t.importance" class="card-importance" :class="`imp-${t.importance.toLowerCase()}`">
                {{ t.importance }}
              </span>
            </div>
            <p v-if="t.description" class="card-desc">{{ t.description }}</p>
            <div class="card-actions">
              <select
                class="status-select"
                :value="t.status"
                @change="changeStatus(t, ($event.target as HTMLSelectElement).value as PlotThreadStatus)"
              >
                <option v-for="c in STATUS_COLUMNS" :key="c.key" :value="c.key">{{ c.title }}</option>
              </select>
              <AppButton variant="ghost" size="sm" @click="openEdit(t)">编辑</AppButton>
              <AppButton variant="ghost" size="sm" @click="removeOne(t)">
                <i class="i-lucide-trash-2" />
              </AppButton>
            </div>
          </AppCard>
        </div>
      </div>
    </div>

    <AppModal v-model="modalOpen" :title="editing ? '编辑线索' : '新增线索'">
      <div class="form-fields">
        <AppInput v-model="form.title" label="标题 *" />
        <AppTextarea v-model="form.description" label="描述" :rows="4" />
        <div class="form-row">
          <label class="field">
            <span class="field-label">重要度</span>
            <select v-model="form.importance" class="field-select">
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </label>
          <label class="field">
            <span class="field-label">状态</span>
            <select v-model="form.status" class="field-select">
              <option v-for="c in STATUS_COLUMNS" :key="c.key" :value="c.key">{{ c.title }}</option>
            </select>
          </label>
        </div>
        <AppInput v-model="form.tags" label="标签（逗号分隔）" />
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

.status-select,
.field-select {
  padding: 4px 8px;
  border: 1px solid var(--color-border);
  border-radius: 4px;
  font-size: 12px;
  background: var(--color-bg);
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
</style>
