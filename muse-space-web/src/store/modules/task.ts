import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getActiveTasks, getRecentTasks } from '@/api/tasks'
import type { BackgroundTaskResponse } from '@/api/tasks'
import type { TaskProgressPayload } from '@/composables/useTaskProgress'

export const useTaskStore = defineStore('task', () => {
  const tasks = ref<BackgroundTaskResponse[]>([])
  const panelOpen = ref(false)

  const activeTasks = computed(() =>
    tasks.value.filter((t) => t.status === 'Running' || t.status === 'Pending'),
  )
  const activeCount = computed(() => activeTasks.value.length)

  async function loadActive() {
    try {
      tasks.value = await getActiveTasks()
    } catch {
      // silent
    }
  }

  async function loadRecent(limit = 30) {
    try {
      tasks.value = await getRecentTasks(limit)
    } catch {
      // silent
    }
  }

  /** 由 SignalR 事件驱动更新 */
  function applyEvent(payload: TaskProgressPayload) {
    const idx = tasks.value.findIndex((t) => t.id === payload.id)
    const updated: BackgroundTaskResponse = {
      id: payload.id,
      taskType: payload.taskType,
      status: payload.status,
      progress: payload.progress,
      title: payload.title,
      statusMessage: payload.statusMessage,
      errorMessage: payload.errorMessage,
      createdAt: payload.createdAt,
      updatedAt: payload.updatedAt,
    }
    if (idx >= 0) {
      tasks.value[idx] = updated
    } else {
      tasks.value.unshift(updated)
    }
  }

  function togglePanel() {
    panelOpen.value = !panelOpen.value
  }

  return { tasks, activeTasks, activeCount, panelOpen, loadActive, loadRecent, applyEvent, togglePanel }
})
