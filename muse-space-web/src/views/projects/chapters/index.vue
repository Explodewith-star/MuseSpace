<script setup lang="ts">
import { useRouter, useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { initChaptersState } from './hooks'

const router = useRouter()
const route = useRoute()

const {
  chapters,
  loading,
  drawerOpen,
  createForm,
  createLoading,
  openCreate,
  submitCreate,
  deleteTarget,
  deleteLoading,
  openDelete,
  cancelDelete,
  confirmDelete,
} = initChaptersState()

const STATUS_VARIANTS: Record<number, string> = { 0: 'muted', 1: 'accent', 2: 'primary', 3: 'success' }
const STATUS_LABELS: Record<number, string> = { 0: '计划中', 1: '草稿中', 2: '修改中', 3: '已定稿' }

function goDetail(chapterId: string) {
  router.push(`/projects/${route.params.id}/chapters/${chapterId}`)
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">章节管理</h2>
      <AppButton @click="openCreate">
        <i class="i-lucide-plus" />
        添加章节
      </AppButton>
    </div>

    <!-- 骨架屏 -->
    <div v-if="loading" class="chapter-list">
      <div v-for="i in 4" :key="i" class="chapter-row skeleton-row">
        <AppSkeleton width="60px" height="14px" />
        <AppSkeleton width="200px" height="14px" />
        <AppSkeleton width="40%" height="14px" />
      </div>
    </div>

    <!-- 空状态 -->
    <AppEmpty
      v-else-if="!chapters.length"
      icon="i-lucide-book-text"
      title="还没有章节"
      description="添加第一个章节，开始规划你的故事结构"
    >
      <template #action>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          添加章节
        </AppButton>
      </template>
    </AppEmpty>

    <!-- 章节列表 -->
    <div v-else class="chapter-list">
      <div
        v-for="chapter in chapters"
        :key="chapter.id"
        class="chapter-row"
        @click="goDetail(chapter.id)"
      >
        <span class="chapter-num">第 {{ chapter.number }} 章</span>
        <span class="chapter-title">{{ chapter.title || '未命名' }}</span>
        <span class="chapter-summary">{{ chapter.summary || '—' }}</span>
        <AppBadge :variant="(STATUS_VARIANTS[chapter.status] as any)" size="sm">
          {{ STATUS_LABELS[chapter.status] }}
        </AppBadge>
        <button class="row-delete-btn" title="删除章节" @click.stop="openDelete(chapter)">
          <i class="i-lucide-trash-2" />
        </button>
      </div>
    </div>

    <!-- 添加章节抽屉 -->
    <AppDrawer v-model="drawerOpen" title="添加章节">
      <div class="form-fields">
        <AppInput
          v-model="createForm.number"
          label="章节编号 *"
          placeholder="如：1"
          type="number"
        />
        <AppInput v-model="createForm.title" label="章节标题" placeholder="如：序章·暗涌" />
        <AppTextarea
          v-model="createForm.summary"
          label="章节摘要"
          placeholder="简述本章发生的关键事件..."
          :rows="4"
        />
        <AppTextarea
          v-model="createForm.goal"
          label="章节目标"
          placeholder="本章希望达成的叙事目的..."
          :rows="3"
        />
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="drawerOpen = false">取消</AppButton>
        <AppButton
          :loading="createLoading"
          :disabled="!createForm.number"
          @click="submitCreate"
        >
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 删除确认 -->
    <AppConfirm
      :model-value="!!deleteTarget"
      title="删除章节"
      :message="`确定删除第 ${deleteTarget?.number} 章《${deleteTarget?.title ?? '未命名'}》吗？`"
      variant="danger"
      confirm-text="删除"
      :loading="deleteLoading"
      @update:model-value="cancelDelete"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped>
.page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.page__title {
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.chapter-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.chapter-row {
  display: grid;
  grid-template-columns: 80px 180px 1fr auto 40px;
  align-items: center;
  gap: 16px;
  padding: 12px 16px;
  border-radius: 8px;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  cursor: pointer;
  transition: border-color 0.15s;
}

.chapter-row:hover {
  border-color: var(--color-primary);
}

.chapter-num {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-primary);
  white-space: nowrap;
}

.chapter-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.chapter-summary {
  font-size: 13px;
  color: var(--color-text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.skeleton-row {
  height: 48px;
  background-color: var(--color-bg-surface);
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
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 15px;
  opacity: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.chapter-row:hover .row-delete-btn {
  opacity: 1;
}

.row-delete-btn:hover {
  background-color: color-mix(in srgb, var(--color-danger) 12%, transparent);
  color: var(--color-danger);
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
</style>

