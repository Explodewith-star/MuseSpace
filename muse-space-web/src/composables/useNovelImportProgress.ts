import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'

export type NovelImportStage = 'chunking' | 'embedding' | 'indexed'

export interface ChunkProgressPayload {
  novelId: string
  stage: 'chunking'
  done: number
  total: number
}

export interface EmbedProgressPayload {
  novelId: string
  stage: 'embedding'
  done: number
  total: number
}

export interface ImportDonePayload {
  novelId: string
  stage: 'indexed'
  done: number
  total: number
}

export interface ImportFailedPayload {
  novelId: string
  error: string
}

export interface ImportProgressPayload {
  novelId: string
  stage: NovelImportStage
  done: number
  total: number
}

export function useNovelImportProgress() {
  const isConnected = ref(false)
  const progressEvent = ref<ImportProgressPayload | null>(null)
  const importFailed = ref<ImportFailedPayload | null>(null)
  const joinedNovelIds = new Set<string>()

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_APP_HUB_BASE ?? ''}/hubs/novel-import`)
    .withAutomaticReconnect()
    .build()

  connection.on('ChunkProgress', (payload: ChunkProgressPayload) => {
    progressEvent.value = payload
  })

  connection.on('EmbedProgress', (payload: EmbedProgressPayload) => {
    progressEvent.value = payload
  })

  connection.on('ImportDone', (payload: ImportDonePayload) => {
    progressEvent.value = {
      novelId: payload.novelId,
      stage: 'indexed',
      done: payload.done,
      total: payload.total,
    }
  })

  connection.onreconnected(async () => {
    await Promise.all(Array.from(joinedNovelIds).map((novelId) => connection.invoke('JoinNovelGroup', novelId)))
  })

  connection.on('ImportFailed', (payload: ImportFailedPayload) => {
    importFailed.value = payload
  })

  async function start() {
    if (isConnected.value) return

    try {
      await connection.start()
      isConnected.value = true
    } catch (err) {
      console.error('[SignalR] connection failed:', err)
    }
  }

  async function joinNovel(novelId: string) {
    if (!isConnected.value)
      await start()

    if (!isConnected.value || joinedNovelIds.has(novelId))
      return

    await connection.invoke('JoinNovelGroup', novelId)
    joinedNovelIds.add(novelId)
  }

  async function leaveNovel(novelId: string) {
    if (!isConnected.value || !joinedNovelIds.has(novelId))
      return

    try {
      await connection.invoke('LeaveNovelGroup', novelId)
    } finally {
      joinedNovelIds.delete(novelId)
    }
  }

  async function stop() {
    try {
      await Promise.all(Array.from(joinedNovelIds).map((novelId) => leaveNovel(novelId)))
      await connection.stop()
    } catch {
      // ignore
    }
    isConnected.value = false
  }

  onUnmounted(() => stop())

  return {
    isConnected,
    progressEvent,
    importFailed,
    start,
    stop,
    joinNovel,
    leaveNovel,
  }
}
