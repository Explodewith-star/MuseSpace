<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useTaskStore } from '@/store/modules/task'
import { useTaskProgress } from '@/composables/useTaskProgress'
import { useAuthStore } from '@/store/modules/auth'

const taskStore = useTaskStore()
const authStore = useAuthStore()
const { start, joinUser, onTaskEvent } = useTaskProgress()

const taskTypeLabels: Record<string, string> = {
  NovelImport: '原著导入',
  AssetExtraction: '资产提取',
  ChapterDraftGeneration: '草稿生成',
  BatchDraftGeneration: '批量生成草稿',
  OutlinePlanning: 'AI 大纲规划',
  ConsistencyCheck: '一致性检查',
  NovelEndingSummary: '结局分析',
  CharacterExtraction: '角色提取',
}

const statusIcons: Record<string, string> = {
  Running: '⏳',
  Pending: '⏳',
  Completed: '✅',
  Failed: '❌',
}

function formatTime(dateStr: string) {
  const d = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - d.getTime()
  const mins = Math.floor(diffMs / 60000)
  if (mins < 1) return '刚刚'
  if (mins < 60) return `${mins} 分钟前`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs} 小时前`
  return `${Math.floor(hrs / 24)} 天前`
}

onMounted(async () => {
  if (authStore.user?.id) {
    await start()
    await joinUser(authStore.user.id)
    onTaskEvent((payload) => taskStore.applyEvent(payload))
    await taskStore.loadRecent(30)
  }
})

watch(
  () => authStore.user?.id,
  async (uid) => {
    if (uid) {
      await start()
      await joinUser(uid)
      await taskStore.loadRecent(30)
    }
  },
)
</script>

<template>
  <!-- 浮动按钮 -->
  <div class="task-fab" @click="taskStore.togglePanel()">
    <span class="task-fab__icon">⚙</span>
    <span v-if="taskStore.activeCount > 0" class="task-fab__badge">{{ taskStore.activeCount }}</span>
  </div>

  <!-- 侧拉面板 -->
  <Teleport to="body">
    <Transition name="task-slide">
      <div v-if="taskStore.panelOpen" class="task-panel-overlay" @click.self="taskStore.togglePanel()">
        <div class="task-panel">
          <div class="task-panel__header">
            <h3>后台任务</h3>
            <button class="task-panel__close" @click="taskStore.togglePanel()">✕</button>
          </div>

          <div v-if="taskStore.tasks.length === 0" class="task-panel__empty"> 暂无任务 </div>

          <div class="task-panel__list">
            <div
              v-for="task in taskStore.tasks"
              :key="task.id"
              class="task-item"
              :class="`task-item--${task.status.toLowerCase()}`"
            >
              <div class="task-item__head">
                <span class="task-item__icon">{{ statusIcons[task.status] ?? '·' }}</span>
                <span class="task-item__title">{{ task.title }}</span>
                <span class="task-item__type">{{ taskTypeLabels[task.taskType] ?? task.taskType }}</span>
              </div>

              <!-- 进度条 -->
              <div v-if="task.status === 'Running' || task.status === 'Pending'" class="task-item__progress">
                <div class="task-item__bar">
                  <div class="task-item__bar-fill" :style="{ width: task.progress + '%' }"></div>
                </div>
                <span class="task-item__pct">{{ task.progress }}%</span>
              </div>

              <div class="task-item__meta">
                <span v-if="task.statusMessage" class="task-item__msg">{{ task.statusMessage }}</span>
                <span v-if="task.errorMessage" class="task-item__err">{{ task.errorMessage }}</span>
                <span class="task-item__time">{{ formatTime(task.updatedAt) }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.task-fab {
  position: fixed;
  bottom: 24px;
  right: 24px;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  background: var(--color-primary, #6366f1);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  z-index: 1000;
  transition: transform 0.2s;
  user-select: none;
}
.task-fab:hover {
  transform: scale(1.08);
}
.task-fab__icon {
  font-size: 22px;
}
.task-fab__badge {
  position: absolute;
  top: -4px;
  right: -4px;
  min-width: 20px;
  height: 20px;
  border-radius: 10px;
  background: #ef4444;
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 5px;
}

.task-panel-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.3);
  z-index: 1001;
  display: flex;
  justify-content: flex-end;
}
.task-panel {
  width: 380px;
  max-width: 90vw;
  height: 100vh;
  background: var(--color-bg, #fff);
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.task-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-border, #e5e7eb);
}
.task-panel__header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
}
.task-panel__close {
  background: none;
  border: none;
  font-size: 18px;
  cursor: pointer;
  color: var(--color-text-secondary, #6b7280);
  padding: 4px 8px;
  border-radius: 4px;
}
.task-panel__close:hover {
  background: var(--color-bg-hover, #f3f4f6);
}
.task-panel__empty {
  padding: 40px 20px;
  text-align: center;
  color: var(--color-text-secondary, #9ca3af);
}
.task-panel__list {
  flex: 1;
  overflow-y: auto;
  padding: 8px 12px;
}

.task-item {
  padding: 12px;
  border-radius: 8px;
  margin-bottom: 8px;
  background: var(--color-bg-secondary, #f9fafb);
  transition: background 0.2s;
}
.task-item--running,
.task-item--pending {
  background: var(--color-bg-active, #eef2ff);
}
.task-item--failed {
  background: var(--color-bg-error, #fef2f2);
}
.task-item__head {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 6px;
}
.task-item__icon {
  font-size: 16px;
  flex-shrink: 0;
}
.task-item__title {
  font-size: 14px;
  font-weight: 500;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.task-item__type {
  font-size: 11px;
  color: var(--color-text-secondary, #9ca3af);
  flex-shrink: 0;
}

.task-item__progress {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}
.task-item__bar {
  flex: 1;
  height: 6px;
  background: var(--color-border, #e5e7eb);
  border-radius: 3px;
  overflow: hidden;
}
.task-item__bar-fill {
  height: 100%;
  background: var(--color-primary, #6366f1);
  border-radius: 3px;
  transition: width 0.4s ease;
}
.task-item__pct {
  font-size: 12px;
  color: var(--color-text-secondary, #6b7280);
  min-width: 32px;
  text-align: right;
}

.task-item__meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--color-text-secondary, #9ca3af);
}
.task-item__msg {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.task-item__err {
  color: #ef4444;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.task-item__time {
  flex-shrink: 0;
}

/* Slide transition */
.task-slide-enter-active,
.task-slide-leave-active {
  transition: opacity 0.25s ease;
}
.task-slide-enter-active .task-panel,
.task-slide-leave-active .task-panel {
  transition: transform 0.25s ease;
}
.task-slide-enter-from,
.task-slide-leave-to {
  opacity: 0;
}
.task-slide-enter-from .task-panel,
.task-slide-leave-to .task-panel {
  transform: translateX(100%);
}
</style>
