<script setup lang="ts">
/**
 * Module D 正典事实层 / 章节事件面板。
 * 展示当前章节的事件清单，支持手动新增 / 编辑 / 删除 / 切换不可逆标记。
 * 嵌入到章节详情页（chapters/detail/index.vue）。
 */
import { ref, computed, watch, onMounted } from 'vue'
import AppCard from '@/components/base/AppCard.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppModal from '@/components/base/AppModal.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import {
  getChapterEvents,
  createChapterEvent,
  updateChapterEvent,
  deleteChapterEvent,
  type ChapterEventResponse,
  type UpsertChapterEventRequest,
} from '@/api/canonFacts'
import { useToast } from '@/composables/useToast'

const props = defineProps<{
  projectId: string
  chapterId: string
}>()

const toast = useToast()
const events = ref<ChapterEventResponse[]>([])
const loading = ref(false)

async function refresh() {
  if (!props.projectId || !props.chapterId) return
  loading.value = true
  try {
    events.value = await getChapterEvents(props.projectId, props.chapterId)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
watch(() => props.chapterId, refresh)

// ── 编辑模态 ─────────────────────────────────────────────────────────
const modalOpen = ref(false)
const editing = ref<ChapterEventResponse | null>(null)
const form = ref<UpsertChapterEventRequest>({
  eventType: '',
  eventText: '',
  isIrreversible: false,
})

function openCreate() {
  editing.value = null
  form.value = {
    order: events.value.length + 1,
    eventType: '',
    eventText: '',
    importance: 'Medium',
    isIrreversible: false,
  }
  modalOpen.value = true
}

function openEdit(e: ChapterEventResponse) {
  editing.value = e
  form.value = {
    order: e.order,
    eventType: e.eventType,
    eventText: e.eventText,
    location: e.location,
    timePoint: e.timePoint,
    importance: e.importance,
    isIrreversible: e.isIrreversible,
  }
  modalOpen.value = true
}

async function save() {
  if (!form.value.eventType.trim() || !form.value.eventText.trim()) {
    toast.error('类型和正文不能为空')
    return
  }
  if (editing.value) {
    await updateChapterEvent(props.projectId, props.chapterId, editing.value.id, form.value)
  } else {
    await createChapterEvent(props.projectId, props.chapterId, form.value)
  }
  modalOpen.value = false
  toast.success('已保存')
  refresh()
}

async function removeOne(e: ChapterEventResponse) {
  if (!confirm(`删除事件「[${e.eventType}] ${e.eventText.slice(0, 40)}…」？`)) return
  await deleteChapterEvent(props.projectId, props.chapterId, e.id)
  refresh()
}

const sortedEvents = computed(() =>
  [...events.value].sort((a, b) => a.order - b.order),
)
</script>

<template>
  <AppCard class="event-panel">
    <div class="panel-head">
      <div class="panel-title">
        <i class="i-lucide-list-checks" />
        <span>章节事件</span>
        <span class="muted">（Module D 正典事实层 / 时间线）</span>
      </div>
      <AppButton size="sm" @click="openCreate">
        <i class="i-lucide-plus" />
        新增事件
      </AppButton>
    </div>

    <div v-if="loading" class="panel-loading">加载中...</div>
    <div v-else-if="sortedEvents.length === 0" class="panel-empty">
      暂无事件。生成草稿后 AI 会自动抽取，也可点击右上角手动录入。
    </div>
    <ul v-else class="event-list">
      <li
        v-for="e in sortedEvents"
        :key="e.id"
        class="event-item"
        :class="{ 'event-item--irreversible': e.isIrreversible }"
      >
        <div class="event-head">
          <span class="event-order">#{{ e.order }}</span>
          <AppBadge variant="primary">{{ e.eventType }}</AppBadge>
          <AppBadge v-if="e.isIrreversible" variant="danger" title="不可重复事件">
            <i class="i-lucide-lock" /> 不可逆
          </AppBadge>
          <AppBadge v-if="e.importance === 'High'" variant="accent">高</AppBadge>
          <span v-if="e.location" class="event-loc">📍 {{ e.location }}</span>
          <span v-if="e.timePoint" class="event-loc">🕒 {{ e.timePoint }}</span>
          <div class="event-actions">
            <AppButton variant="ghost" size="sm" @click="openEdit(e)">编辑</AppButton>
            <AppButton variant="ghost" size="sm" @click="removeOne(e)">
              <i class="i-lucide-trash-2" />
            </AppButton>
          </div>
        </div>
        <p class="event-text">{{ e.eventText }}</p>
      </li>
    </ul>

    <AppModal v-model="modalOpen" :title="editing ? '编辑事件' : '新增事件'">
      <div class="form-fields">
        <div class="form-row">
          <AppInput v-model="form.eventType" label="事件类型 *" placeholder="如 Proposal" />
          <label class="field">
            顺序
            <input v-model.number="form.order" type="number" min="1" class="plain-input" />
          </label>
        </div>
        <AppTextarea
          v-model="form.eventText"
          label="事件正文 *"
          :rows="3"
          placeholder="一两句客观陈述本章发生的事，避免主观评论"
        />
        <div class="form-row">
          <AppInput v-model="form.location" label="地点" />
          <AppInput v-model="form.timePoint" label="时间点" placeholder="如 中元节当夜" />
        </div>
        <div class="form-row">
          <AppInput v-model="form.importance" label="重要度" placeholder="High / Medium / Low" />
          <label class="checkbox-row">
            <input v-model="form.isIrreversible" type="checkbox" />
            标记为不可重复（如求婚 / 第一次告白）
          </label>
        </div>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="modalOpen = false">取消</AppButton>
        <AppButton @click="save">保存</AppButton>
      </template>
    </AppModal>
  </AppCard>
</template>

<style scoped>
.event-panel {
  margin-top: 16px;
}
.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-color, #eee);
}
.panel-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 14px;
}
.panel-title .muted {
  color: #999;
  font-weight: 400;
  font-size: 12px;
}
.panel-loading,
.panel-empty {
  padding: 24px;
  text-align: center;
  color: #888;
  font-size: 13px;
}
.event-list {
  list-style: none;
  margin: 0;
  padding: 8px 16px 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.event-item {
  border: 1px solid var(--border-color, #eee);
  border-radius: 6px;
  padding: 10px 12px;
  background: var(--surface-1, #fff);
}
.event-item--irreversible {
  border-left: 3px solid #c0392b;
}
.event-head {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  font-size: 12px;
  margin-bottom: 4px;
}
.event-order {
  font-family: monospace;
  color: #888;
}
.event-loc {
  color: #888;
  font-size: 12px;
}
.event-actions {
  margin-left: auto;
  display: flex;
  gap: 4px;
}
.event-text {
  margin: 0;
  font-size: 13px;
  line-height: 1.6;
  color: #333;
}
.form-fields {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.form-row {
  display: flex;
  gap: 12px;
}
.form-row > * {
  flex: 1;
}
.checkbox-row {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  white-space: nowrap;
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
