<script setup lang="ts">
import { useRoute } from 'vue-router'
import AppCard from '@/components/base/AppCard.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AgentLauncher from '@/components/base/AgentLauncher.vue'
import PendingSuggestionPanel from '@/components/base/PendingSuggestionPanel.vue'
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

const workflowSteps = [
  { step: '①', label: '导入原著', desc: '上传 TXT，AI 自动切片向量化', icon: 'i-lucide-book-open', to: 'novels' },
  { step: '②', label: '提取资产', desc: '一键提取角色、世界观、文风', icon: 'i-lucide-box', to: 'novels' },
  { step: '③', label: '规划大纲', desc: '前往章节页 → AI 规划大纲', icon: 'i-lucide-list-tree', to: 'chapters' },
  { step: '④', label: '导入章节', desc: '审核大纲后一键生成章节', icon: 'i-lucide-file-plus', to: 'suggestions?category=Outline' },
  { step: '⑤', label: '填充计划', desc: '进入章节 → 自动填充写作计划', icon: 'i-lucide-clipboard-list', to: 'chapters' },
  { step: '⑥', label: '生成草稿', desc: '一键生成草稿，审查文风一致性', icon: 'i-lucide-sparkles', to: 'chapters' },
]
</script>

<template>
  <div class="overview-page">
    <!-- D3-2 统一 Agent 入口：一句话提交任务 -->
    <AgentLauncher
      :project-id="projectId"
      title="项目 Agent 工作台"
      description="用一句话描述你想让 AI 做的事，下面预设可一键从已导入原著中提取全部资产，或生成项目摘要。"
      :default-agent-type="'extract-all'"
      placeholder="例如：提取原著中所有主要角色、世界观规则、文风画像"
      :presets="[
        { label: '一键提取全部', agentType: 'extract-all', icon: 'i-lucide-wand-2' },
        { label: '只提取角色', agentType: 'character-extract', icon: 'i-lucide-users' },
        { label: '只提取世界观', agentType: 'worldrule-extract', icon: 'i-lucide-globe' },
        { label: '只提取文风', agentType: 'styleprofile-extract', icon: 'i-lucide-feather' },
        { label: '生成项目摘要', agentType: 'project-summary', icon: 'i-lucide-clipboard-list' },
      ]"
    />

    <PendingSuggestionPanel
      :project-id="projectId"
      :categories="['ProjectSummary']"
      title="项目摘要与下一步建议"
      :default-open="true"
    />

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

    <!-- 推荐工作流 -->
    <AppCard class="workflow-card">
      <div class="workflow-header">
        <i class="i-lucide-compass workflow-icon" />
        <div>
          <p class="workflow-title">推荐创作流程</p>
          <p class="workflow-subtitle">跟着这 6 步，让 AI 全程辅助你的创作</p>
        </div>
      </div>
      <div class="workflow-steps">
        <div
          v-for="s in workflowSteps"
          :key="s.step"
          class="workflow-step"
          @click="$router.push(`/projects/${projectId}/${s.to}`)"
        >
          <div class="step-num">{{ s.step }}</div>
          <i :class="['step-icon', s.icon]" />
          <div class="step-body">
            <p class="step-label">{{ s.label }}</p>
            <p class="step-desc">{{ s.desc }}</p>
          </div>
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

/* 推荐工作流卡片 */
.workflow-card {
  border-left: 3px solid var(--color-accent);
}

.workflow-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.workflow-icon {
  font-size: 22px;
  color: var(--color-accent);
  flex-shrink: 0;
}

.workflow-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 2px;
}

.workflow-subtitle {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

.workflow-steps {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}

@media (max-width: 600px) {
  .workflow-steps {
    grid-template-columns: repeat(2, 1fr);
  }
}

.workflow-step {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 10px 12px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  cursor: pointer;
  transition: border-color 0.15s, background-color 0.15s;
}

.workflow-step:hover {
  border-color: var(--color-accent);
  background-color: color-mix(in srgb, var(--color-accent) 5%, transparent);
}

.step-num {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-accent);
  line-height: 1.4;
  flex-shrink: 0;
  margin-top: 1px;
}

.step-icon {
  font-size: 14px;
  color: var(--color-text-muted);
  flex-shrink: 0;
  margin-top: 2px;
}

.step-body {
  min-width: 0;
}

.step-label {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 2px;
}

.step-desc {
  font-size: 11px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.4;
}
</style>
