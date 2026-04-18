<script setup lang="ts">
import AppBadge from '@/components/base/AppBadge.vue'
import type { StoryProjectResponse } from '@/types/models'

defineProps<{ project: StoryProjectResponse }>()
defineEmits<{ click: []; delete: [] }>()
</script>

<template>
  <div class="project-card" @click="$emit('click')">
    <div class="project-card__main">
      <h3 class="project-card__name">{{ project.name }}</h3>
      <p v-if="project.description" class="project-card__desc">{{ project.description }}</p>
      <div class="project-card__tags">
        <AppBadge v-if="project.genre" variant="primary">{{ project.genre }}</AppBadge>
        <AppBadge v-if="project.narrativePerspective" variant="muted">
          {{ project.narrativePerspective }}
        </AppBadge>
      </div>
    </div>
    <div class="project-card__footer">
      <span class="project-card__date">
        {{ new Date(project.createdAt).toLocaleDateString('zh-CN') }}
      </span>
      <button class="project-card__delete" title="删除项目" @click.stop="$emit('delete')">
        <i class="i-lucide-trash-2" />
      </button>
    </div>
  </div>
</template>

<style scoped>
.project-card {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 20px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 16px;
  transition:
    border-color 0.15s,
    box-shadow 0.15s;
}

.project-card:hover {
  border-color: var(--color-primary);
  box-shadow: 0 4px 16px rgba(108, 92, 231, 0.1);
}

.project-card__main {
  flex: 1;
}

.project-card__name {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 6px;
}

.project-card__desc {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0 0 10px;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.project-card__tags {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.project-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.project-card__date {
  font-size: 12px;
  color: var(--color-text-muted);
}

.project-card__delete {
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

.project-card:hover .project-card__delete {
  opacity: 1;
}

.project-card__delete:hover {
  background-color: color-mix(in srgb, var(--color-danger) 12%, transparent);
  color: var(--color-danger);
}
</style>
