<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { triggerAgentTask, type AgentType } from '@/api/agentTasks'
import { getNovels, uploadNovel, deleteNovel, getNovelStatus } from '@/api/novels'
import { useNovelImportProgress } from '@/composables/useNovelImportProgress'
import { useAgentProgress, type AgentProgressAsset } from '@/composables/useAgentProgress'
import type { NovelResponse } from '@/types/models'

// crypto.randomUUID() 在 HTTP（非安全上下文）下不可用，用兼容方案
function generateId(): string {
  return crypto.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2, 11)}`
}

type ImportTaskStatus = 'uploading' | 'pending' | 'chunking' | 'embedding' | 'done' | 'failed'

interface ActiveImportTask {
  localId: string
  novelId?: string
  title: string
  fileName: string
  fileSize: number
  status: ImportTaskStatus
  uploadPercent: number | null
  uploadedBytes: number
  totalBytes: number
  totalChunks: number
  progressDone: number
  progressTotal: number
  errorMessage?: string
}

const route = useRoute()
const router = useRouter()
const projectId = computed(() => route.params.id as string)

const novels = ref<NovelResponse[]>([])
const loading = ref(false)
const uploading = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)
const activeImportTasks = ref<ActiveImportTask[]>([])
const importProgress = useNovelImportProgress()

// ── 资产提取进度 ─────────────────────────────────────────────
type AssetExtractStage = 'started' | 'generating' | 'done' | 'failed' | null
const assetExtractStage = ref<AssetExtractStage>(null)
const assetExtractSummary = ref('')
const assetExtractNovelId = ref<string | null>(null)
const assetExtractAssets = ref<AgentProgressAsset[]>([])
const retryingAgentTypes = ref<AgentType[]>([])
const agentProgress = useAgentProgress()

const completedAssetItems = computed(() =>
  assetExtractAssets.value.filter((asset) => asset.status === 'succeeded'),
)

const failedAssetItems = computed(() =>
  assetExtractAssets.value.filter((asset) => asset.status === 'failed' && asset.retryAgentType),
)

watch(
  () => agentProgress.latestEvent.value,
  (evt) => {
    if (!evt || evt.taskType !== 'asset-extract') return

    assetExtractStage.value = evt.stage as AssetExtractStage
    assetExtractNovelId.value = evt.novelId ?? assetExtractNovelId.value
    assetExtractAssets.value = evt.assets ?? []

    if (evt.stage === 'started') {
      assetExtractSummary.value = '正在启动角色/世界观/文风提取...'
      retryingAgentTypes.value = []
      return
    }

    if (evt.stage === 'generating') {
      assetExtractSummary.value = evt.summary ?? 'AI 正在分析原著，提取角色、世界观规则和文风画像...'
      retryingAgentTypes.value = []
      return
    }

    if (evt.stage === 'done') {
      assetExtractSummary.value = evt.summary ?? '资产提取完成'
      retryingAgentTypes.value = []
    }

    if (evt.stage === 'failed') {
      assetExtractSummary.value = evt.error ?? '提取失败'
      retryingAgentTypes.value = []
    }
  },
)

const deleteTarget = ref<NovelResponse | null>(null)
const deleteLoading = ref(false)
const deleteDialogOpen = ref(false)

const PROCESSING_STATUSES = new Set(['Pending', 'Chunking', 'Embedding'])

const STATUS_LABEL: Record<string, string> = {
  Pending: '待处理',
  Chunking: '切片中',
  Embedding: '向量化',
  Indexed: '已完成',
  Failed: '失败',
}

const STATUS_VARIANT: Record<string, string> = {
  Pending: 'muted',
  Chunking: 'accent',
  Embedding: 'accent',
  Indexed: 'success',
  Failed: 'danger',
}

const visibleNovels = computed(() =>
  novels.value.filter((novel) => !activeImportTasks.value.some((task) => task.novelId === novel.id)),
)

const importPollingTimers = new Map<string, ReturnType<typeof setInterval>>()

function createImportTask(file: File): ActiveImportTask {
  return {
    localId: generateId(),
    title: file.name.replace(/\.[^.]+$/, '') || file.name,
    fileName: file.name,
    fileSize: file.size,
    status: 'uploading',
    uploadPercent: 0,
    uploadedBytes: 0,
    totalBytes: file.size,
    totalChunks: 0,
    progressDone: 0,
    progressTotal: 0,
  }
}

function findImportTask(localId: string) {
  return activeImportTasks.value.find((task) => task.localId === localId)
}

function updateImportTask(localId: string, updater: (task: ActiveImportTask) => void) {
  const task = findImportTask(localId)
  if (!task) return
  updater(task)
}

function removeImportTask(localId: string) {
  activeImportTasks.value = activeImportTasks.value.filter((task) => task.localId !== localId)
}

function findImportTaskByNovelId(novelId: string) {
  return activeImportTasks.value.find((task) => task.novelId === novelId)
}

function ensureImportTaskForNovel(novel: NovelResponse) {
  const existingTask = findImportTaskByNovelId(novel.id)
  if (existingTask) {
    syncImportTaskWithNovel(existingTask.localId, novel)
    return existingTask
  }

  const task: ActiveImportTask = {
    localId: generateId(),
    novelId: novel.id,
    title: novel.title,
    fileName: novel.fileName,
    fileSize: novel.fileSize,
    status: normalizeNovelStatus(novel.status),
    uploadPercent: 100,
    uploadedBytes: novel.fileSize,
    totalBytes: novel.fileSize,
    totalChunks: novel.totalChunks,
    progressDone: novel.progressDone,
    progressTotal: novel.progressTotal,
    errorMessage: novel.lastError ?? undefined,
  }

  activeImportTasks.value.unshift(task)
  return task
}

function upsertNovel(novel: NovelResponse) {
  const index = novels.value.findIndex((item) => item.id === novel.id)
  if (index === -1) {
    novels.value.unshift(novel)
    return
  }

  novels.value.splice(index, 1, novel)
}

function normalizeNovelStatus(status: NovelResponse['status']): ImportTaskStatus {
  switch (status) {
    case 'Chunking':
      return 'chunking'
    case 'Embedding':
      return 'embedding'
    case 'Indexed':
      return 'done'
    case 'Failed':
      return 'failed'
    case 'Pending':
    default:
      return 'pending'
  }
}

function getImportTaskLabel(task: ActiveImportTask): string {
  switch (task.status) {
    case 'uploading':
      return '上传中'
    case 'pending':
      return '待处理'
    case 'chunking':
      return '切片中'
    case 'embedding':
      return '向量化中'
    case 'done':
      return '已完成'
    case 'failed':
      return '失败'
  }
}

function getImportTaskVariant(task: ActiveImportTask): string {
  switch (task.status) {
    case 'done':
      return 'success'
    case 'failed':
      return 'danger'
    case 'pending':
      return 'muted'
    default:
      return 'accent'
  }
}

function getImportTaskPercent(task: ActiveImportTask): number | null {
  if (task.status === 'uploading') return task.uploadPercent ?? 0
  if ((task.status === 'chunking' || task.status === 'embedding') && task.progressTotal > 0)
    return Math.min(100, Math.round((task.progressDone / task.progressTotal) * 100))
  if (task.status === 'done') return 100
  return null
}

function getImportTaskProgressText(task: ActiveImportTask): string {
  switch (task.status) {
    case 'uploading':
      return task.uploadPercent === null
        ? `已上传 ${formatFileSize(task.uploadedBytes)}`
        : `已上传 ${task.uploadPercent}%`
    case 'pending':
      return '上传完成，等待服务器处理'
    case 'chunking':
      return task.progressTotal > 0
        ? `切片进度 ${task.progressDone} / ${task.progressTotal}`
        : '文件已入库，正在切片'
    case 'embedding':
      return task.progressTotal > 0
        ? `向量化进度 ${task.progressDone} / ${task.progressTotal}`
        : '正在生成向量索引'
    case 'done':
      return task.totalChunks > 0 ? `共 ${task.totalChunks} 段，导入完成` : '导入完成'
    case 'failed':
      return task.errorMessage ?? '导入失败，请稍后重试'
  }
}

async function beginImportTracking(localId: string, novelId: string) {
  await importProgress.joinNovel(novelId)
  startImportPolling(localId, novelId)
}

function stopImportPolling(localId: string) {
  const timer = importPollingTimers.get(localId)
  if (!timer) return

  clearInterval(timer)
  importPollingTimers.delete(localId)
}

function stopAllImportPolling() {
  importPollingTimers.forEach((_, localId) => stopImportPolling(localId))
}

function finalizeImportTask(localId: string, novel: NovelResponse) {
  stopImportPolling(localId)
  upsertNovel(novel)
  removeImportTask(localId)
}

function syncImportTaskWithNovel(localId: string, novel: NovelResponse) {
  updateImportTask(localId, (task) => {
    task.novelId = novel.id
    task.title = novel.title
    task.status = normalizeNovelStatus(novel.status)
    task.uploadPercent = 100
    task.uploadedBytes = task.totalBytes
    task.totalChunks = novel.totalChunks
    task.progressDone = novel.progressDone
    task.progressTotal = novel.progressTotal
    task.errorMessage = novel.lastError ?? (novel.status === 'Failed' ? '服务器处理失败，请查看后端日志。' : undefined)
  })

  if (novel.status === 'Indexed') {
    finalizeImportTask(localId, novel)
    return
  }

  if (novel.status === 'Failed') {
    stopImportPolling(localId)
  }
}

function startImportPolling(localId: string, novelId: string) {
  if (importPollingTimers.has(localId)) return

  const timer = setInterval(async () => {
    try {
      const updated = await getNovelStatus(projectId.value, novelId)
      syncImportTaskWithNovel(localId, updated)
    } catch {
      updateImportTask(localId, (task) => {
        task.status = 'failed'
        task.errorMessage = '获取导入状态失败，请稍后刷新重试。'
      })
      stopImportPolling(localId)
    }
  }, 2000)

  importPollingTimers.set(localId, timer)
}

async function fetchNovels() {
  loading.value = true
  try {
    const data = await getNovels(projectId.value)
    novels.value = data
    // 对所有处理中的 novel 启动轮询
    data.forEach((n) => {
      if (PROCESSING_STATUSES.has(n.status)) {
        const task = ensureImportTaskForNovel(n)
        void beginImportTracking(task.localId, n.id)
      }
    })
  } finally {
    loading.value = false
  }
}

function triggerFileInput() {
  fileInputRef.value?.click()
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  const task = createImportTask(file)
  activeImportTasks.value.unshift(task)
  uploading.value = true

  try {
    const novel = await uploadNovel(projectId.value, file, undefined, (progress) => {
      const totalBytes = progress.total ?? file.size
      const uploadPercent = totalBytes > 0
        ? Math.min(100, Math.round((progress.loaded / totalBytes) * 100))
        : null

      updateImportTask(task.localId, (current) => {
        current.uploadedBytes = progress.loaded
        current.totalBytes = totalBytes
        current.uploadPercent = uploadPercent
      })
    })

    syncImportTaskWithNovel(task.localId, novel)

    if (novel.status !== 'Indexed' && novel.status !== 'Failed') {
      await beginImportTracking(task.localId, novel.id)
    }
  } catch (error) {
    updateImportTask(task.localId, (current) => {
      current.status = 'failed'
      current.errorMessage = error instanceof Error ? error.message : '上传失败，请稍后重试。'
    })
  } finally {
    uploading.value = false
    input.value = ''
  }
}

function openDelete(novel: NovelResponse) {
  deleteTarget.value = novel
  deleteDialogOpen.value = true
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleteLoading.value = true
  try {
    const task = findImportTaskByNovelId(deleteTarget.value.id)
    if (task) {
      stopImportPolling(task.localId)
      removeImportTask(task.localId)
    }

    await deleteNovel(projectId.value, deleteTarget.value.id)
    novels.value = novels.value.filter((n) => n.id !== deleteTarget.value!.id)
    deleteDialogOpen.value = false
    deleteTarget.value = null
  } finally {
    deleteLoading.value = false
  }
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function isRetrying(agentType: string | undefined): boolean {
  if (!agentType) return false
  return retryingAgentTypes.value.includes(agentType as AgentType)
}

async function retryFailedAsset(agentType: string | undefined) {
  if (!agentType || isRetrying(agentType)) return

  retryingAgentTypes.value = [...retryingAgentTypes.value, agentType as AgentType]
  try {
    await triggerAgentTask(projectId.value, {
      agentType: agentType as AgentType,
      novelId: assetExtractNovelId.value ?? undefined,
    })
  } finally {
    retryingAgentTypes.value = retryingAgentTypes.value.filter((item) => item !== agentType)
  }
}

function closeAssetExtractBanner() {
  assetExtractStage.value = null
  assetExtractSummary.value = ''
  assetExtractAssets.value = []
  assetExtractNovelId.value = null
  retryingAgentTypes.value = []
}

onMounted(() => {
  fetchNovels()
  void agentProgress.joinProject(projectId.value)
})

watch(
  () => importProgress.progressEvent.value,
  (payload) => {
    if (!payload) return

    const task = findImportTaskByNovelId(payload.novelId)
    if (!task) return

    updateImportTask(task.localId, (current) => {
      current.status = payload.stage === 'indexed' ? 'done' : payload.stage
      current.progressDone = payload.done
      current.progressTotal = payload.total
    })
  },
)

watch(
  () => importProgress.importFailed.value,
  (payload) => {
    if (!payload) return

    const task = findImportTaskByNovelId(payload.novelId)
    if (!task) return

    updateImportTask(task.localId, (current) => {
      current.status = 'failed'
      current.errorMessage = payload.error
    })
  },
)

onUnmounted(() => {
  stopAllImportPolling()
  void importProgress.stop()
  agentProgress.stop()
})
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">原著导入</h2>
      <AppButton :loading="uploading" @click="triggerFileInput">
        <i class="i-lucide-upload" />
        导入原著
      </AppButton>
      <!-- 隐藏文件选择框，只接受 .txt -->
      <input
        ref="fileInputRef"
        type="file"
        accept=".txt"
        class="hidden-input"
        @change="onFileSelected"
      />
    </div>

    <p class="page__desc">
      导入原著 TXT 文件后，系统将自动切片并生成向量索引。草稿生成时会自动检索相关段落作为参考。
    </p>

    <!-- 资产提取进度 banner -->
    <div
      v-if="assetExtractStage && assetExtractStage !== null"
      :class="['extract-banner', `extract-banner--${assetExtractStage}`]"
    >
      <i
        :class="[
          assetExtractStage === 'done' ? 'i-lucide-check-circle' :
          assetExtractStage === 'failed' ? 'i-lucide-alert-circle' :
          'i-lucide-loader-2 banner-spin'
        ]"
      />
      <div class="banner-content">
        <span class="banner-text">
          <template v-if="assetExtractStage === 'started'">正在启动角色/世界观/文风提取...</template>
          <template v-else-if="assetExtractStage === 'generating'">AI 正在分析原著，提取角色、世界观规则和文风画像...</template>
          <template v-else>{{ assetExtractSummary }}</template>
        </span>

        <div v-if="assetExtractAssets.length" class="banner-assets">
          <div
            v-for="asset in assetExtractAssets"
            :key="`${asset.assetType}-${asset.status}`"
            :class="['banner-asset', `banner-asset--${asset.status}`]"
          >
            <div class="banner-asset__meta">
              <span class="banner-asset__label">{{ asset.label }}</span>
              <span class="banner-asset__message">{{ asset.message ?? `${asset.label}${asset.status === 'failed' ? '提取失败' : '提取完成'}` }}</span>
            </div>
            <AppButton
              v-if="asset.status === 'failed' && asset.retryAgentType"
              variant="secondary"
              size="sm"
              :loading="isRetrying(asset.retryAgentType)"
              @click="retryFailedAsset(asset.retryAgentType)"
            >
              重试{{ asset.label }}
            </AppButton>
          </div>
        </div>
      </div>
      <button
        v-if="assetExtractStage === 'done' || completedAssetItems.length > 0 || failedAssetItems.length > 0"
        class="banner-action"
        @click="router.push(`/projects/${projectId}/suggestions`)"
      >
        前往建议中心 →
      </button>
      <button
        class="banner-close"
        @click="closeAssetExtractBanner"
      >
        <i class="i-lucide-x" />
      </button>
    </div>

    <div v-if="activeImportTasks.length" class="novel-list novel-list--active">
      <div v-for="task in activeImportTasks" :key="task.localId" class="novel-row novel-row--active">
        <i class="i-lucide-file-up row-icon" />
        <div class="novel-info">
          <span class="novel-title">{{ task.title }}</span>
          <span class="novel-meta">
            {{ formatFileSize(task.fileSize) }}
            <template v-if="task.totalChunks > 0"> · {{ task.totalChunks }} 段</template>
          </span>
          <div class="novel-progress">
            <div class="novel-progress__track">
              <div
                :class="[
                  'novel-progress__fill',
                  { 'novel-progress__fill--indeterminate': getImportTaskPercent(task) === null },
                ]"
                :style="getImportTaskPercent(task) !== null ? { width: `${getImportTaskPercent(task)}%` } : undefined"
              />
            </div>
            <span class="novel-progress__text">{{ getImportTaskProgressText(task) }}</span>
          </div>
          <span v-if="task.errorMessage && task.status === 'failed'" class="novel-error">
            {{ task.errorMessage }}
          </span>
        </div>
        <AppBadge :variant="(getImportTaskVariant(task) as any)" size="sm">
          <i v-if="task.status !== 'done' && task.status !== 'failed'" class="i-lucide-loader-2 badge-spin" />
          {{ getImportTaskLabel(task) }}
        </AppBadge>
      </div>
    </div>

    <!-- 骨架屏 -->
    <div v-if="loading" class="novel-list">
      <div v-for="i in 3" :key="i" class="novel-row skeleton-row">
        <AppSkeleton width="200px" height="14px" />
        <AppSkeleton width="80px" height="14px" />
        <AppSkeleton width="60px" height="14px" />
      </div>
    </div>

    <!-- 空状态 -->
    <AppEmpty
      v-else-if="!visibleNovels.length && !activeImportTasks.length"
      icon="i-lucide-book-open"
      title="还没有导入原著"
      description="上传 TXT 格式原著后，AI 将自动切片并向量化，随后可一键提取角色、世界观与文风档案，为 AI 写作提供参考素材"
    >
      <template #action>
        <AppButton @click="fileInputRef?.click()">
          <i class="i-lucide-upload" />
          导入原著
        </AppButton>
      </template>
    </AppEmpty>

    <!-- 原著列表 -->
    <div v-else class="novel-list">
      <div v-for="novel in visibleNovels" :key="novel.id" class="novel-row">
        <i class="i-lucide-file-text row-icon" />
        <div class="novel-info">
          <span class="novel-title">{{ novel.title }}</span>
          <span class="novel-meta">
            {{ formatFileSize(novel.fileSize) }}
            <template v-if="novel.totalChunks > 0"> · {{ novel.totalChunks }} 段</template>
          </span>
        </div>
        <AppBadge :variant="(STATUS_VARIANT[novel.status] as any)" size="sm" :class="{ 'badge-pulsing': PROCESSING_STATUSES.has(novel.status) }">
          <i v-if="PROCESSING_STATUSES.has(novel.status)" class="i-lucide-loader-2 badge-spin" />
          {{ STATUS_LABEL[novel.status] ?? novel.status }}
        </AppBadge>
        <button
          class="row-delete-btn"
          title="删除原著"
          @click.stop="openDelete(novel)"
        >
          <i class="i-lucide-trash-2" />
        </button>
      </div>
    </div>

    <!-- 删除确认 -->
    <AppConfirm
      v-model="deleteDialogOpen"
      title="删除原著"
      :message="deleteTarget ? `确定要删除「${deleteTarget.title}」吗？\n\n将同时清除：切片数据、向量索引，以及该原著提取出的未应用建议（已应用的角色/世界观规则不受影响）。` : ''"
      confirm-text="删除"
      variant="danger"
      :loading="deleteLoading"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped>
.page {
  padding: 24px;
  max-width: 900px;
}

.page__header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
}

.page__title {
  flex: 1;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text);
  margin: 0;
}

.page__desc {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0 0 20px;
  line-height: 1.6;
}

.hidden-input {
  display: none;
}

.novel-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.novel-list--active {
  margin-bottom: 12px;
}

.novel-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 14px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-card);
  transition: background-color 0.15s;
}

.novel-row:hover {
  background: var(--color-bg-elevated);
}

.novel-row--active {
  align-items: flex-start;
}

.skeleton-row {
  height: 52px;
  background: var(--color-bg-card);
}

.row-icon {
  font-size: 18px;
  color: var(--color-text-muted);
  flex-shrink: 0;
}

.novel-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.novel-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.novel-meta {
  font-size: 12px;
  color: var(--color-text-muted);
}

.novel-progress {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 6px;
}

.novel-progress__track {
  position: relative;
  flex: 1;
  height: 6px;
  border-radius: 999px;
  overflow: hidden;
  background: var(--color-bg-elevated);
}

.novel-progress__fill {
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--color-primary), var(--color-accent));
  transition: width 0.2s ease;
}

.novel-progress__fill--indeterminate {
  position: absolute;
  width: 36%;
  min-width: 84px;
  animation: progress-indeterminate 1.2s ease-in-out infinite;
}

.novel-progress__text {
  flex-shrink: 0;
  font-size: 12px;
  color: var(--color-text-muted);
}

.novel-error {
  margin-top: 4px;
  font-size: 12px;
  color: var(--color-danger, #ef4444);
}

.row-delete-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: none;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  font-size: 14px;
  flex-shrink: 0;
  opacity: 0.35;
  transition:
    background-color 0.15s,
    color 0.15s,
    opacity 0.15s;
}

.novel-row:hover .row-delete-btn {
  opacity: 1;
}

.row-delete-btn:hover {
  background-color: var(--color-danger-bg, rgba(239, 68, 68, 0.1));
  color: var(--color-danger, #ef4444);
  opacity: 1;
}

.badge-spin {
  display: inline-block;
  margin-right: 3px;
  animation: spin 1.2s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to   { transform: rotate(360deg); }
}

@keyframes progress-indeterminate {
  from {
    transform: translateX(-100%);
  }
  to {
    transform: translateX(280%);
  }
}

/* ── 资产提取 banner ── */
.extract-banner {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 0.875rem;
  margin-bottom: 16px;
  background: var(--color-info-bg, #eff6ff);
  border: 1px solid var(--color-info-border, #bfdbfe);
  color: var(--color-info-text, #1d4ed8);
}
.extract-banner--done {
  background: var(--color-success-bg, #f0fdf4);
  border-color: var(--color-success-border, #bbf7d0);
  color: var(--color-success-text, #15803d);
}
.extract-banner--failed {
  background: var(--color-danger-bg, #fef2f2);
  border-color: var(--color-danger-border, #fecaca);
  color: var(--color-danger-text, #b91c1c);
}

.banner-content {
  flex: 1;
  min-width: 0;
}

.banner-text {
  display: block;
  line-height: 1.4;
}

.banner-assets {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 10px;
}

.banner-asset {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 10px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.45);
}

.banner-asset--failed {
  border: 1px solid rgba(185, 28, 28, 0.16);
}

.banner-asset--succeeded {
  border: 1px solid rgba(21, 128, 61, 0.12);
}

.banner-asset__meta {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.banner-asset__label {
  font-weight: 600;
}

.banner-asset__message {
  font-size: 12px;
  line-height: 1.4;
  opacity: 0.88;
}

.banner-action {
  flex-shrink: 0;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 600;
  color: inherit;
  text-decoration: underline;
  padding: 0;
}
.banner-action:hover {
  opacity: 0.8;
}

.banner-close {
  flex-shrink: 0;
  background: none;
  border: none;
  cursor: pointer;
  color: inherit;
  opacity: 0.5;
  padding: 2px;
  display: flex;
  align-items: center;
}
.banner-close:hover {
  opacity: 1;
}

.banner-spin {
  animation: spin 1s linear infinite;
}
@keyframes spin {
  from { transform: rotate(0deg); }
  to   { transform: rotate(360deg); }
}

@media (max-width: 900px) {
  .banner-asset {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
