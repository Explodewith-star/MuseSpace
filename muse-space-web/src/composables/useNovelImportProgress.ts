import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'

export interface ChunkProgressPayload {
  novelId: string
  totalChunks: number
}

export interface EmbedProgressPayload {
  novelId: string
  embedded: number
  total: number
}

export interface ImportDonePayload {
  novelId: string
  totalChunks: number
}

export interface ImportFailedPayload {
  novelId: string
  error: string
}

export function useNovelImportProgress(novelId: string) {
  const isConnected = ref(false)
  const chunkProgress = ref<ChunkProgressPayload | null>(null)
  const embedProgress = ref<EmbedProgressPayload | null>(null)
  const importDone = ref<ImportDonePayload | null>(null)
  const importFailed = ref<ImportFailedPayload | null>(null)

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${import.meta.env.VITE_APP_HUB_BASE ?? ''}/hubs/novel-import`)
    .withAutomaticReconnect()
    .build()

  connection.on('ChunkProgress', (payload: ChunkProgressPayload) => {
    chunkProgress.value = payload
  })

  connection.on('EmbedProgress', (payload: EmbedProgressPayload) => {
    embedProgress.value = payload
  })

  connection.on('ImportDone', (payload: ImportDonePayload) => {
    importDone.value = payload
  })

  connection.on('ImportFailed', (payload: ImportFailedPayload) => {
    importFailed.value = payload
  })

  async function start() {
    try {
      await connection.start()
      isConnected.value = true
      await connection.invoke('JoinNovelGroup', novelId)
    } catch (err) {
      console.error('[SignalR] connection failed:', err)
    }
  }

  async function stop() {
    try {
      await connection.invoke('LeaveNovelGroup', novelId)
      await connection.stop()
    } catch {
      // ignore
    }
    isConnected.value = false
  }

  onUnmounted(() => stop())

  return {
    isConnected,
    chunkProgress,
    embedProgress,
    importDone,
    importFailed,
    start,
    stop,
  }
}
