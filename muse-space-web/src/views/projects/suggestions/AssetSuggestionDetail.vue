<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import {
  getSuggestionById,
  ignoreSuggestion,
  acceptSuggestion,
  applySuggestion,
  reApplySuggestion,
  deleteSuggestion,
} from '@/api/suggestions'
import { CATEGORY_LABELS, STATUS_LABELS, STATUS_VARIANTS } from './utils'
import type { AgentSuggestionResponse } from '@/types/models'
import { useToast } from '@/composables/useToast'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const projectId = route.params.id as string
const suggestionId = route.params.suggestionId as string

// ── 加载 ──────────────────────────────────────────────────────
const suggestion = ref<AgentSuggestionResponse | null>(null)
const loading = ref(true)

async function loadSuggestion() {
  loading.value = true
  try {
    suggestion.value = await getSuggestionById(projectId, suggestionId)
    initForm()
  } catch {
    toast.error('加载失败')
  } finally {
    loading.value = false
  }
}

// ── 表单（可编辑 contentJson）─────────────────────────────────
const form = ref<Record<string, unknown>>({})

function initForm() {
  if (!suggestion.value) return
  try {
    form.value = JSON.parse(suggestion.value.contentJson) ?? {}
  } catch {
    form.value = {}
  }
}

const category = computed(() => suggestion.value?.category ?? '')

// 各类型字段配置
const characterFields = [
  { key: 'name',               label: '姓名',       type: 'text' },
  { key: 'age',                label: '年龄',       type: 'number' },
  { key: 'role',               label: '身份定位',   type: 'text' },
  { key: 'personalitySummary', label: '性格概述',   type: 'textarea' },
  { key: 'motivation',         label: '核心动机',   type: 'textarea' },
  { key: 'speakingStyle',      label: '说话风格',   type: 'textarea' },
  { key: 'forbiddenBehaviors', label: '禁忌行为',   type: 'textarea' },
  { key: 'currentState',       label: '当前状态',   type: 'textarea' },
]

const worldRuleFields = [
  { key: 'title',           label: '规则名称',   type: 'text' },
  { key: 'category',        label: '所属分类',   type: 'text' },
  { key: 'description',     label: '规则描述',   type: 'textarea' },
  { key: 'priority',        label: '优先级 (1~10)', type: 'number' },
  { key: 'isHardConstraint', label: '硬性约束',  type: 'checkbox' },
]

const styleProfileFields = [
  { key: 'name',                    label: '画像名称',     type: 'text' },
  { key: 'tone',                    label: '整体语气',     type: 'text' },
  { key: 'sentenceLengthPreference', label: '句子长度偏好', type: 'text' },
  { key: 'dialogueRatio',           label: '对话占比',     type: 'text' },
  { key: 'descriptionDensity',      label: '描写密度',     type: 'text' },
  { key: 'forbiddenExpressions',    label: '禁用表达',     type: 'textarea' },
  { key: 'sampleReferenceText',     label: '参考样本',     type: 'textarea' },
]

const activeFields = computed(() => {
  switch (category.value) {
    case 'Character':    return characterFields
    case 'WorldRule':    return worldRuleFields
    case 'StyleProfile': return styleProfileFields
    default:             return []
  }
})

// ── 操作 ──────────────────────────────────────────────────────
const actionLoading = ref(false)

async function handleAcceptAndApply() {
  if (!suggestion.value) return
  actionLoading.value = true
  try {
    if (suggestion.value.status === 'Pending') {
      await acceptSuggestion(projectId, suggestionId)
    }
    await applySuggestion(projectId, suggestionId)
    toast.success('已成功应用到项目')
    router.back()
  } catch {
    toast.error('应用失败')
  } finally {
    actionLoading.value = false
  }
}

async function handleIgnore() {
  actionLoading.value = true
  try {
    await ignoreSuggestion(projectId, suggestionId)
    toast.success('已忽略并删除')
    router.back()
  } catch {
    toast.error('操作失败')
  } finally {
    actionLoading.value = false
  }
}

async function handleWithdraw() {
  if (!suggestion.value) return
  actionLoading.value = true
  try {
    await ignoreSuggestion(projectId, suggestionId)
    toast.success('已撤回导入（已创建的资产保留）')
    router.back()
  } catch {
    toast.error('操作失败')
  } finally {
    actionLoading.value = false
  }
}

async function handleReApply() {
  actionLoading.value = true
  try {
    await reApplySuggestion(projectId, suggestionId)
    toast.success('已重新导入')
    router.back()
  } catch {
    toast.error('操作失败')
  } finally {
    actionLoading.value = false
  }
}

async function handleDelete() {
  actionLoading.value = true
  try {
    await deleteSuggestion(projectId, suggestionId)
    toast.success('已删除')
    router.back()
  } catch {
    toast.error('删除失败')
  } finally {
    actionLoading.value = false
  }
}

function goBack() {
  router.back()
}

onMounted(loadSuggestion)
</script>

<template>
  <div class="asset-detail-page">
    <!-- 顶部导航 -->
    <div class="detail-header">
      <button class="back-btn" @click="goBack">
        <i class="i-lucide-arrow-left" />
        返回
      </button>
      <div class="header-meta" v-if="suggestion">
        <span class="category-label">{{ CATEGORY_LABELS[suggestion.category] ?? suggestion.category }}</span>
        <AppBadge :variant="STATUS_VARIANTS[suggestion.status]">
          {{ STATUS_LABELS[suggestion.status] }}
        </AppBadge>
      </div>
    </div>

    <!-- 骨架屏 -->
    <template v-if="loading">
      <div class="skeleton-wrap">
        <AppSkeleton v-for="i in 6" :key="i" height="40px" style="margin-bottom: 12px" />
      </div>
    </template>

    <!-- 主内容 -->
    <template v-else-if="suggestion">
      <h2 class="detail-title">{{ suggestion.title }}</h2>

      <div class="fields-form">
        <template v-for="field in activeFields" :key="field.key">
          <div class="field-row">
            <label class="field-label">{{ field.label }}</label>

            <!-- checkbox -->
            <div v-if="field.type === 'checkbox'" class="field-checkbox">
              <input
                type="checkbox"
                :id="`field-${field.key}`"
                v-model="(form as Record<string, unknown>)[field.key]"
                :disabled="suggestion.status !== 'Pending'"
              />
              <label :for="`field-${field.key}`">是</label>
            </div>

            <!-- number -->
            <input
              v-else-if="field.type === 'number'"
              type="number"
              class="field-input"
              v-model="(form as Record<string, unknown>)[field.key]"
              :disabled="suggestion.status !== 'Pending'"
            />

            <!-- textarea -->
            <textarea
              v-else-if="field.type === 'textarea'"
              class="field-textarea"
              rows="3"
              v-model="(form as Record<string, unknown>)[field.key] as string"
              :disabled="suggestion.status !== 'Pending'"
            />

            <!-- text -->
            <input
              v-else
              type="text"
              class="field-input"
              v-model="(form as Record<string, unknown>)[field.key] as string"
              :disabled="suggestion.status !== 'Pending'"
            />
          </div>
        </template>
      </div>

      <!-- 操作区 -->
      <div class="action-bar" v-if="suggestion.status === 'Pending'">
        <AppButton
          variant="primary"
          :loading="actionLoading"
          @click="handleAcceptAndApply"
        >
          <i class="i-lucide-zap" />
          导入
        </AppButton>
        <AppButton
          variant="ghost"
          :disabled="actionLoading"
          @click="handleIgnore"
        >
          <i class="i-lucide-trash-2" />
          忽略删除
        </AppButton>
      </div>

      <div class="action-bar" v-else-if="suggestion.status === 'Accepted'">
        <AppButton
          variant="primary"
          :loading="actionLoading"
          @click="handleAcceptAndApply"
        >
          <i class="i-lucide-zap" />
          导入到正式资产
        </AppButton>
      </div>

      <div class="action-bar" v-else-if="suggestion.status === 'Applied'">
        <AppButton
          variant="ghost"
          :loading="actionLoading"
          @click="handleWithdraw"
        >
          <i class="i-lucide-undo-2" />
          撤回导入
        </AppButton>
        <span class="applied-tip">
          <i class="i-lucide-check-circle" />
          该建议已应用到项目中（撤回不删除已创建的资产）
        </span>
      </div>

      <div class="action-bar" v-else-if="suggestion.status === 'Ignored'">
        <AppButton
          variant="primary"
          :loading="actionLoading"
          @click="handleReApply"
        >
          <i class="i-lucide-rotate-ccw" />
          重新导入
        </AppButton>
        <AppButton
          variant="ghost"
          :disabled="actionLoading"
          @click="handleDelete"
        >
          <i class="i-lucide-trash-2" />
          删除记录
        </AppButton>
      </div>
    </template>

    <div v-else class="error-tip">建议不存在或已被删除</div>
  </div>
</template>

<style scoped>
.asset-detail-page {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px 20px 48px;
}

.detail-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.back-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 0.9rem;
  color: var(--color-text-secondary);
  padding: 4px 8px;
  border-radius: 6px;
  transition: background 0.15s;
}
.back-btn:hover {
  background: var(--color-bg-hover);
  color: var(--color-text-primary);
}

.header-meta {
  display: flex;
  align-items: center;
  gap: 8px;
}

.category-label {
  font-size: 0.8rem;
  color: var(--color-text-tertiary);
  background: var(--color-bg-subtle);
  padding: 2px 8px;
  border-radius: 4px;
}

.detail-title {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 24px;
}

.skeleton-wrap {
  padding-top: 16px;
}

.fields-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.field-row {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.field-input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  font-size: 0.9rem;
  background: var(--color-bg-input, var(--color-bg));
  color: var(--color-text-primary);
  transition: border-color 0.15s;
  box-sizing: border-box;
}
.field-input:focus {
  outline: none;
  border-color: var(--color-primary);
}
.field-input:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.field-textarea {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  font-size: 0.9rem;
  background: var(--color-bg-input, var(--color-bg));
  color: var(--color-text-primary);
  resize: vertical;
  line-height: 1.6;
  transition: border-color 0.15s;
  box-sizing: border-box;
}
.field-textarea:focus {
  outline: none;
  border-color: var(--color-primary);
}
.field-textarea:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.field-checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.9rem;
}

.action-bar {
  display: flex;
  gap: 12px;
  margin-top: 32px;
  padding-top: 20px;
  border-top: 1px solid var(--color-border);
}

.applied-tip {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 32px;
  padding: 12px 16px;
  background: var(--color-success-bg, #f0fdf4);
  border-radius: 8px;
  color: var(--color-success, #16a34a);
  font-size: 0.9rem;
}

.error-tip {
  text-align: center;
  color: var(--color-text-tertiary);
  padding: 48px 0;
  font-size: 0.9rem;
}
</style>
