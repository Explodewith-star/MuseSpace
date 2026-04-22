<script setup lang="ts">
import { useRoute } from 'vue-router'
import AppCard from '@/components/base/AppCard.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { useProjectStore } from '@/store/modules/project'

const route = useRoute()
const projectStore = useProjectStore()
const projectId = route.params.id as string

const sections = [
  { label: '章节', desc: '管理故事章节与摘要', icon: 'i-lucide-book-text', to: 'chapters' },
  { label: '角色', desc: '维护角色卡与人物关系', icon: 'i-lucide-users', to: 'characters' },
  { label: '世界观', desc: '定义世界规则与约束', icon: 'i-lucide-globe', to: 'world-rules' },
  { label: '文风配置', desc: '设定语气基调与写作偏好', icon: 'i-lucide-pen-line', to: 'style-profile' },
  { label: '草稿生成', desc: 'AI 辅助场景创作', icon: 'i-lucide-sparkles', to: 'draft' },
]
</script>

<template>
  <div class="overview-page">
    <!-- 项目信息卡 -->
    <AppCard class="info-card">
      <div v-if="projectStore.loading" class="info-skeleton">
        <AppSkeleton width="40%" height="24px" style="margin-bottom: 10px" />
        <AppSkeleton width="80%" height="14px" style="margin-bottom: 6px" />
        <AppSkeleton width="30%" height="14px" />
      </div>
      <div v-else-if="projectStore.current" class="info-body">
        <h2 class="info-name">{{ projectStore.current.name }}</h2>
        <p v-if="projectStore.current.description" class="info-desc">
          {{ projectStore.current.description }}
        </p>
        <div class="info-meta">
          <AppBadge v-if="projectStore.current.genre" variant="primary">
            {{ projectStore.current.genre }}
          </AppBadge>
          <AppBadge v-if="projectStore.current.narrativePerspective" variant="muted">
            {{ projectStore.current.narrativePerspective }}
          </AppBadge>
          <span class="info-date">
            创建于 {{ new Date(projectStore.current.createdAt).toLocaleDateString('zh-CN') }}
          </span>
        </div>
      </div>
    </AppCard>

    <!-- 快捷导航卡片 -->
    <div class="section-grid">
      <div
        v-for="section in sections"
        :key="section.to"
        class="section-card"
        @click="$router.push(`/projects/${projectId}/${section.to}`)"
      >
        <i :class="['section-card__icon', section.icon]" />
        <div>
          <p class="section-card__label">{{ section.label }}</p>
          <p class="section-card__desc">{{ section.desc }}</p>
        </div>
        <i class="i-lucide-chevron-right section-card__arrow" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.overview-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.info-card {
  border-left: 3px solid var(--color-primary);
}

.info-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.info-name {
  font-size: 20px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.info-desc {
  font-size: 14px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.6;
}

.info-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.info-date {
  font-size: 12px;
  color: var(--color-text-muted);
}

.section-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.section-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px 20px;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  cursor: pointer;
  transition:
    border-color 0.15s,
    background-color 0.15s;
}

.section-card:hover {
  border-color: var(--color-primary);
  background-color: var(--color-bg-elevated);
}

.section-card__icon {
  font-size: 22px;
  color: var(--color-primary);
  flex-shrink: 0;
}

.section-card__label {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 2px;
}

.section-card__desc {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
}

.section-card__arrow {
  margin-left: auto;
  font-size: 16px;
  color: var(--color-text-muted);
  flex-shrink: 0;
}
</style>
