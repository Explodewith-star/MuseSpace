<script setup lang="ts">
import { useRouter } from 'vue-router'
import AppLayout from '@/components/layout/AppLayout.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import ProjectCard from './components/ProjectCard.vue'
import { initHomeState } from './hooks'

const router = useRouter()

const {
  projects,
  loading,
  drawerOpen,
  createTrigger,
  createForm,
  createLoading,
  openCreate,
  submitCreate,
  deleteTarget,
  deleteLoading,
  openDelete,
  cancelDelete,
  confirmDelete,
  navigateTo,
} = initHomeState()
</script>

<template>
  <AppLayout>
    <template #header-right>
      <button class="pool-nav-btn" @click="router.push('/character-pool')">
        <i class="i-lucide-users" />
        角色池
      </button>
    </template>
    <div class="home-page">
      <div class="home-page__header">
        <div>
          <h1 class="home-page__title">我的创作项目</h1>
          <p class="home-page__subtitle">管理你的小说世界，开始 AI 辅助创作</p>
        </div>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          新建项目
        </AppButton>
      </div>

      <!-- 骨架屏 -->
      <div v-if="loading" class="project-grid">
        <div v-for="i in 3" :key="i" class="skeleton-card">
          <AppSkeleton width="55%" height="20px" style="margin-bottom: 10px" />
          <AppSkeleton width="100%" height="14px" style="margin-bottom: 6px" />
          <AppSkeleton width="75%" height="14px" style="margin-bottom: 16px" />
          <AppSkeleton width="40%" height="12px" />
        </div>
      </div>

      <!-- 空状态 -->
      <AppEmpty
        v-else-if="!projects.length"
        icon="i-lucide-book-open"
        title="还没有任何项目"
        description="创建你的第一个故事项目，开始构建你的世界"
      >
        <template #action>
          <AppButton @click="openCreate">
            <i class="i-lucide-plus" />
            新建项目
          </AppButton>
        </template>
      </AppEmpty>

      <!-- 项目卡片列表 -->
      <div v-else class="project-grid">
        <ProjectCard
          v-for="project in projects"
          :key="project.id"
          :project="project"
          @click="navigateTo(project.id)"
          @delete="openDelete(project)"
        />
      </div>
    </div>

    <!-- 新建项目抽屉 -->
    <AppDrawer v-model="drawerOpen" title="新建项目" :clear-handler="openCreate" :open-trigger="createTrigger">
      <div class="form-fields">
        <AppInput
          v-model="createForm.name"
          label="项目名称 *"
          placeholder="给你的故事起个名字"
        />
        <AppTextarea
          v-model="createForm.description"
          label="项目简介"
          placeholder="简短描述这个故事..."
          :rows="3"
        />
        <AppInput v-model="createForm.genre" label="类型" placeholder="如：玄幻、都市、科幻" />
        <AppInput
          v-model="createForm.narrativePerspective"
          label="叙事视角"
          placeholder="如：第一人称、第三人称限知"
        />
        <div class="form-field">
          <label class="form-label">项目大类</label>
          <div class="outline-type-options">
            <button
              v-for="t in ['原创主线', '原著续写', '直线番外', '扩写改写']"
              :key="t"
              type="button"
              class="outline-type-btn"
              :class="{ active: createForm.outlineType === t }"
              @click="createForm.outlineType = createForm.outlineType === t ? '' : t"
            >{{ t }}</button>
          </div>
        </div>
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="drawerOpen = false">取消</AppButton>
        <AppButton
          :loading="createLoading"
          :disabled="!createForm.name.trim()"
          @click="submitCreate"
        >
          创建项目
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 删除确认 -->
    <AppConfirm
      :model-value="!!deleteTarget"
      title="删除项目"
      :message="`确定删除项目《${deleteTarget?.name}》吗？\n\n项目内所有章节、角色、世界观规则、原著导入及 AI 建议将被永久删除，不可恢复。`"
      variant="danger"
      confirm-text="删除"
      :loading="deleteLoading"
      @update:model-value="cancelDelete"
      @confirm="confirmDelete"
    />
  </AppLayout>
</template>

<style scoped>
.home-page {
  max-width: 1100px;
  margin: 0 auto;
}

.home-page__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 32px;
}

.home-page__title {
  font-size: 24px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 4px;
}

.home-page__subtitle {
  font-size: 14px;
  color: var(--color-text-muted);
  margin: 0;
}

.project-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.skeleton-card {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 20px;
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.outline-type-options {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.outline-type-btn {
  padding: 4px 14px;
  border-radius: 20px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-surface);
  color: var(--color-text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s;
}

.outline-type-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.outline-type-btn.active {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: #fff;
}

.pool-nav-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s;
  margin-right: 8px;
}

.pool-nav-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}
</style>

