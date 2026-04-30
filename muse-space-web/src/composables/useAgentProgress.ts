import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'

export type AgentStage = 'started' | 'generating' | 'done' | 'failed'

export interface AgentProgressPayload {
  projectId: string
  taskType: string
  stage: AgentStage
  summary?: string
  error?: string
}

export function useAgentProgress() {
  const isConnected = ref(false)
  const latestEvent = ref<AgentProgressPayload | null>(null)
  const joinedProjectIds = new Set<string>()
  /** 调用方可注册回调，断线重连后被调用以重新拉取活跃任务恢复进度 UI。 */
  const reconnectedListeners = new Set<(projectIds: string[]) => void>()

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_APP_HUB_BASE ?? ''}/hubs/agent-progress`)
    .withAutomaticReconnect()
    .build()

  connection.on('AgentStarted', (payload: AgentProgressPayload) => {
    latestEvent.value = payload
  })

  connection.on('AgentGenerating', (payload: AgentProgressPayload) => {
    latestEvent.value = payload
  })

  connection.on('AgentDone', (payload: AgentProgressPayload) => {
    latestEvent.value = payload
  })

  connection.on('AgentFailed', (payload: AgentProgressPayload) => {
    latestEvent.value = payload
  })

  connection.onreconnected(async () => {
    const ids = Array.from(joinedProjectIds)
    await Promise.all(ids.map((id) => connection.invoke('JoinProjectGroup', id)))
    reconnectedListeners.forEach((cb) => {
      try {
        cb(ids)
      } catch {
        // ignore listener errors
      }
    })
  })

  async function start() {
    if (isConnected.value) return
    try {
      await connection.start()
      isConnected.value = true
    } catch {
      // will retry via automatic reconnect
    }
  }

  async function joinProject(projectId: string) {
    if (!isConnected.value) await start()
    if (!joinedProjectIds.has(projectId)) {
      joinedProjectIds.add(projectId)
      try {
        await connection.invoke('JoinProjectGroup', projectId)
      } catch {
        // ignore
      }
    }
  }

  async function leaveProject(projectId: string) {
    if (joinedProjectIds.has(projectId)) {
      joinedProjectIds.delete(projectId)
      try {
        await connection.invoke('LeaveProjectGroup', projectId)
      } catch {
        // ignore
      }
    }
  }

  async function stop() {
    reconnectedListeners.clear()
    try {
      await connection.stop()
    } catch {
      // ignore
    }
    isConnected.value = false
  }

  function onReconnected(cb: (projectIds: string[]) => void) {
    reconnectedListeners.add(cb)
    return () => reconnectedListeners.delete(cb)
  }

  onUnmounted(stop)

  return {
    isConnected,
    latestEvent,
    start,
    joinProject,
    leaveProject,
    onReconnected,
    stop,
  }
}
