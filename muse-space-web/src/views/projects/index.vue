<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AppLayout from '@/components/layout/AppLayout.vue'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import { initProjectWorkspace } from './hooks'

const route = useRoute()
const { projectStore, goBack } = initProjectWorkspace()

const projectId = computed(() => route.params.id as string)

const navItems = computed(() => [
  { label: '概览', icon: 'i-lucide-layout-dashboard', to: `/projects/${projectId.value}/overview` },
  { label: '创作', icon: 'i-lucide-book-text', to: `/projects/${projectId.value}/chapters` },
  { label: '角色', icon: 'i-lucide-users', to: `/projects/${projectId.value}/characters` },
  { label: '世界观', icon: 'i-lucide-globe', to: `/projects/${projectId.value}/world-rules` },
  { label: '文风', icon: 'i-lucide-pen-line', to: `/projects/${projectId.value}/style-profile` },
  { label: '伏笔追踪', icon: 'i-lucide-spline', to: `/projects/${projectId.value}/plot-threads` },
  { label: 'Canon 事实', icon: 'i-lucide-shield-check', to: `/projects/${projectId.value}/canon-facts` },
  { label: '原著导入', icon: 'i-lucide-book-open', to: `/projects/${projectId.value}/novels` },
  { label: '建议中心', icon: 'i-lucide-inbox', to: `/projects/${projectId.value}/suggestions` },
])
</script>

<template>
  <AppLayout>
    <template #header-left>
      <button class="back-btn" title="返回项目列表" @click="goBack">
        <i class="i-lucide-arrow-left" />
      </button>
      <span class="project-name">{{ projectStore.current?.name ?? '加载中...' }}</span>
    </template>

    <template #sidebar>
      <AppSidebar :items="navItems" />
    </template>

    <router-view :key="route.fullPath" />
  </AppLayout>
</template>

<style scoped>
.back-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 16px;
  transition:
    background-color 0.15s,
    color 0.15s;
  flex-shrink: 0;
}

.back-btn:hover {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.project-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}
</style>

