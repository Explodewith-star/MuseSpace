<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { getNovels, uploadNovel, deleteNovel, getNovelStatus } from '@/api/novels'
import type { NovelResponse } from '@/types/models'

const route = useRoute()
const projectId = computed(() => route.params.id as string)

const novels = ref<NovelResponse[]>([])
const loading = ref(false)
const uploading = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)

const deleteTarget = ref<NovelResponse | null>(null)
const deleteLoading = ref(false)
const deleteDialogOpen = ref(false)

const PROCESSING_STATUSES = new Set(['Pending', 'Processing', 'Chunking', 'Embedding'])
const TERMINAL_STATUSES = new Set(['Indexed', 'Done', 'Failed'])

const STATUS_LABEL: Record<string, string> = {
  Pending: '待处理',
  Processing: '处理中',
  Chunking: '切片中',
  Embedding: '向量化',
  Indexed: '已完成',
  Done: '已完成',
  Failed: '失败',
}

const STATUS_VARIANT: Record<string, string> = {
  Pending: 'muted',
  Processing: 'accent',
  Chunking: 'accent',
  Embedding: 'accent',
  Indexed: 'success',
  Done: 'success',
  Failed: 'danger',
}

// 轮询：为每个处理中的 novel 维护一个定时器
const pollingTimers = new Map<string, ReturnType<typeof setInterval>>()

function startPolling(novelId: string) {
  if (pollingTimers.has(novelId)) return
  const timer = setInterval(async () => {
    try {
      const updated = await getNovelStatus(projectId.value, novelId)
      const idx = novels.value.findIndex((n) => n.id === novelId)
      if (idx !== -1) novels.value.splice(idx, 1, updated)
      if (TERMINAL_STATUSES.has(updated.status)) {
        stopPolling(novelId)
      }
    } catch {
      stopPolling(novelId)
    }
  }, 2000)
  pollingTimers.set(novelId, timer)
}

function stopPolling(novelId: string) {
  const timer = pollingTimers.get(novelId)
  if (timer) {
    clearInterval(timer)
    pollingTimers.delete(novelId)
  }
}

function stopAllPolling() {
  pollingTimers.forEach((_, id) => stopPolling(id))
}

async function fetchNovels() {
  loading.value = true
  try {
    const data = await getNovels(projectId.value)
    novels.value = data
    // 对所有处理中的 novel 启动轮询
    data.forEach((n) => {
      if (PROCESSING_STATUSES.has(n.status)) startPolling(n.id)
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
  uploading.value = true
  try {
    const novel = await uploadNovel(projectId.value, file)
    novels.value.unshift(novel)
    // 上传成功后立即开始轮询这条新记录
    startPolling(novel.id)
  } finally {
    uploading.value = false
    input.value = ''
  }
}

function openDelete(novel: NovelResponse) {
  deleteTarget.value = novel
  deleteDialogOpen.value = true
}

function cancelDelete() {
  deleteDialogOpen.value = false
  deleteTarget.value = null
}

async function confirmDelete() {
  if (!deleteTarget.value) return
  deleteLoading.value = true
  try {
    stopPolling(deleteTarget.value.id)
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

onMounted(fetchNovels)
onUnmounted(stopAllPolling)
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
      v-else-if="!novels.length"
      icon="i-lucide-book-open"
      title="还没有导入原著"
      description="点击右上角「导入原著」按钮，上传 TXT 格式文件，AI 将自动提取参考片段"
    />

    <!-- 原著列表 -->
    <div v-else class="novel-list">
      <div v-for="novel in novels" :key="novel.id" class="novel-row">
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
      :message="deleteTarget ? `确定要删除「${deleteTarget.title}」吗？相关切片和向量数据也将被清除。` : ''"
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
</style>
