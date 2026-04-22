<script setup lang="ts">
import { useRouter, useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AppCard from '@/components/base/AppCard.vue'
import { initChapterDetailState, CHAPTER_STATUS_LABELS, CHAPTER_STATUS_VARIANTS } from './hooks'

const router = useRouter()
const route = useRoute()

const {
  chapter,
  loading,
  saving,
  editingSection,
  metaForm,
  draftForm,
  finalForm,
  startEdit,
  cancelEdit,
  saveEdit,
} = initChapterDetailState()

function goBack() {
  router.push(`/projects/${route.params.id}/chapters`)
}
</script>

<template>
  <div class="chapter-detail">
    <!-- 顶部导航 -->
    <div class="detail-header">
      <button class="back-btn" @click="goBack">
        <i class="i-lucide-arrow-left" />
        返回章节列表
      </button>
    </div>

    <!-- 加载骨架 -->
    <div v-if="loading" class="skeleton-wrap">
      <AppSkeleton width="30%" height="24px" style="margin-bottom:12px" />
      <AppSkeleton width="100%" height="80px" style="margin-bottom:12px" />
      <AppSkeleton width="100%" height="200px" />
    </div>

    <template v-else-if="chapter">
      <!-- 基本信息卡片 -->
      <AppCard class="section-card">
        <div class="section-header">
          <div class="chapter-title-row">
            <span class="chapter-number">第 {{ chapter.number }} 章</span>
            <h1 class="chapter-title">{{ chapter.title || '（未命名）' }}</h1>
            <AppBadge :variant="(CHAPTER_STATUS_VARIANTS[chapter.status] as any)">
              {{ CHAPTER_STATUS_LABELS[chapter.status] }}
            </AppBadge>
          </div>
          <AppButton v-if="editingSection !== 'meta'" variant="ghost" size="sm" @click="startEdit('meta')">
            <i class="i-lucide-pencil" /> 编辑
          </AppButton>
        </div>

        <!-- 查看模式 -->
        <template v-if="editingSection !== 'meta'">
          <div v-if="chapter.goal" class="field-block">
            <span class="field-label">本章目标</span>
            <p class="field-value">{{ chapter.goal }}</p>
          </div>
          <div v-if="chapter.summary" class="field-block">
            <span class="field-label">章节概要</span>
            <p class="field-value">{{ chapter.summary }}</p>
          </div>
          <div v-if="!chapter.goal && !chapter.summary" class="empty-hint">
            还没有设置目标和概要，点击编辑添加
          </div>
        </template>

        <!-- 编辑模式 -->
        <div v-else class="edit-form">
          <AppInput v-model="metaForm.title" label="章节标题" placeholder="第X章标题" />
          <AppTextarea v-model="metaForm.goal" label="本章目标" placeholder="这一章需要完成什么叙事任务？" :rows="2" />
          <AppTextarea v-model="metaForm.summary" label="章节概要" placeholder="用几句话概括本章内容..." :rows="3" />
          <div class="field-block">
            <span class="field-label">章节状态</span>
            <div class="status-options">
              <button
                v-for="(label, val) in CHAPTER_STATUS_LABELS"
                :key="val"
                :class="['status-option', { 'status-option--active': metaForm.status === Number(val) }]"
                @click="metaForm.status = Number(val)"
              >
                {{ label }}
              </button>
            </div>
          </div>
          <div class="edit-actions">
            <AppButton variant="secondary" size="sm" @click="cancelEdit">取消</AppButton>
            <AppButton size="sm" :loading="saving" @click="saveEdit">保存</AppButton>
          </div>
        </div>
      </AppCard>

      <!-- 草稿文本卡片 -->
      <AppCard class="section-card">
        <div class="section-header">
          <h2 class="section-title">
            <i class="i-lucide-file-text" /> 草稿
          </h2>
          <AppButton v-if="editingSection !== 'draft'" variant="ghost" size="sm" @click="startEdit('draft')">
            <i class="i-lucide-pencil" /> 编辑
          </AppButton>
        </div>

        <template v-if="editingSection !== 'draft'">
          <div v-if="chapter.draftText" class="text-content">{{ chapter.draftText }}</div>
          <div v-else class="empty-hint">暂无草稿内容</div>
        </template>

        <div v-else class="edit-form">
          <AppTextarea v-model="draftForm.draftText" label="" placeholder="在这里编写草稿..." :rows="16" />
          <div class="edit-actions">
            <AppButton variant="secondary" size="sm" @click="cancelEdit">取消</AppButton>
            <AppButton size="sm" :loading="saving" @click="saveEdit">保存</AppButton>
          </div>
        </div>
      </AppCard>

      <!-- 定稿文本卡片 -->
      <AppCard class="section-card">
        <div class="section-header">
          <h2 class="section-title">
            <i class="i-lucide-book-open" /> 定稿
          </h2>
          <AppButton v-if="editingSection !== 'final'" variant="ghost" size="sm" @click="startEdit('final')">
            <i class="i-lucide-pencil" /> 编辑
          </AppButton>
        </div>

        <template v-if="editingSection !== 'final'">
          <div v-if="chapter.finalText" class="text-content final-text">{{ chapter.finalText }}</div>
          <div v-else class="empty-hint">暂无定稿内容</div>
        </template>

        <div v-else class="edit-form">
          <AppTextarea v-model="finalForm.finalText" label="" placeholder="在这里编写定稿..." :rows="16" />
          <div class="edit-actions">
            <AppButton variant="secondary" size="sm" @click="cancelEdit">取消</AppButton>
            <AppButton size="sm" :loading="saving" @click="saveEdit">保存</AppButton>
          </div>
        </div>
      </AppCard>
    </template>
  </div>
</template>

<style scoped>
.chapter-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.detail-header {
  margin-bottom: 4px;
}

.back-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 0;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 13px;
  color: var(--color-text-muted);
  transition: color 0.15s;
}

.back-btn:hover {
  color: var(--color-primary);
}

.skeleton-wrap {
  padding: 4px 0;
}

.section-card {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.section-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.chapter-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.chapter-number {
  font-size: 13px;
  color: var(--color-text-muted);
  font-weight: 500;
}

.chapter-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.field-block {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.field-value {
  font-size: 14px;
  color: var(--color-text-primary);
  line-height: 1.6;
  margin: 0;
}

.empty-hint {
  font-size: 13px;
  color: var(--color-text-muted);
  font-style: italic;
}

.text-content {
  font-size: 14px;
  line-height: 1.85;
  color: var(--color-text-primary);
  white-space: pre-wrap;
}

.final-text {
  font-family: 'Georgia', 'Noto Serif SC', serif;
}

.edit-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.edit-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.status-options {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.status-option {
  padding: 4px 14px;
  border-radius: 99px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-elevated);
  cursor: pointer;
  font-size: 12px;
  color: var(--color-text-muted);
  transition: all 0.15s;
}

.status-option--active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 15%, transparent);
  color: var(--color-primary);
  font-weight: 500;
}
</style>
