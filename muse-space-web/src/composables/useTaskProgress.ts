import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'

export interface TaskProgressPayload {
  id: string
  taskType: string
  status: string
  progress: number
  title: string
  statusMessage?: string
  errorMessage?: string
  createdAt: string
  updatedAt: string
}

export function useTaskProgress() {
  const isConnected = ref(false)
  const latestEvent = ref<TaskProgressPayload | null>(null)
  const callbacks = new Set<(payload: TaskProgressPayload) => void>()
  let userId: string | null = null

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_APP_HUB_BASE ?? ''}/hubs/task-progress`)
    .withAutomaticReconnect()
    .build()

  const handleEvent = (payload: TaskProgressPayload) => {
    latestEvent.value = payload
    callbacks.forEach((cb) => cb(payload))
  }

  connection.on('TaskStarted', handleEvent)
  connection.on('TaskProgress', handleEvent)
  connection.on('TaskCompleted', handleEvent)
  connection.on('TaskFailed', handleEvent)

  connection.onreconnected(async () => {
    isConnected.value = true
    if (userId) {
      await connection.invoke('JoinUserGroup', userId)
    }
  })
  connection.onclose(() => {
    isConnected.value = false
  })

  async function start() {
    if (connection.state === signalR.HubConnectionState.Disconnected) {
      await connection.start()
      isConnected.value = true
    }
  }

  async function joinUser(uid: string) {
    userId = uid
    if (connection.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('JoinUserGroup', uid)
    }
  }

  async function leaveUser(uid: string) {
    if (connection.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('LeaveUserGroup', uid)
    }
    userId = null
  }

  async function stop() {
    await connection.stop()
    isConnected.value = false
  }

  function onTaskEvent(cb: (payload: TaskProgressPayload) => void) {
    callbacks.add(cb)
  }

  onUnmounted(() => {
    stop()
  })

  return { isConnected, latestEvent, start, joinUser, leaveUser, stop, onTaskEvent }
}
