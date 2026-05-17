<script setup lang="ts">
import { useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AgentLauncher from '@/components/base/AgentLauncher.vue'
import PendingSuggestionPanel from '@/components/base/PendingSuggestionPanel.vue'
import { initWorldRulesState } from './hooks'

const route = useRoute()
const projectId = route.params.id as string

const {
  rules,
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
  editDrawerOpen,
  editTrigger,
  editForm,
  editLoading,
  openEdit,
  resetEditForm,
  submitEdit,
} = initWorldRulesState()

function priorityVariant(p: number): 'danger' | 'accent' | 'muted' {
  if (p >= 8) return 'danger'
  if (p >= 5) return 'accent'
  return 'muted'
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">世界观规则</h2>
      <AppButton @click="openCreate">
        <i class="i-lucide-plus" />
        添加规则
      </AppButton>
    </div>

    <!-- D3-2 Agent 工作台：从原著批量提取候选世界观规则 -->
    <AgentLauncher
      class="agent-launcher-block"
      :project-id="projectId"
      title="世界观提取 Agent"
      description="从已导入原著中提取候选世界观规则（如设定、社会结构、力量体系），结果进入建议中心等待确认。"
      :default-agent-type="'worldrule-extract'"
      placeholder="可选：补充约束，例如“重点提取修真等级体系”"
      suggestion-category="WorldRule"
      :presets="[{ label: '从原著提取规则', agentType: 'worldrule-extract', icon: 'i-lucide-globe' }]"
    />

    <PendingSuggestionPanel
      :project-id="projectId"
      :categories="['WorldRuleConsistency']"
      title="待处理世界观冲突"
    />

    <!-- 骨架屏 -->
    <div v-if="loading" class="rule-list">
      <div v-for="i in 4" :key="i" class="rule-row skeleton-row">
        <AppSkeleton width="200px" height="14px" />
        <AppSkeleton width="80px" height="14px" />
        <AppSkeleton width="60px" height="20px" style="border-radius:99px" />
      </div>
    </div>

    <!-- 空状态 -->
    <AppEmpty
      v-else-if="!rules.length"
      icon="i-lucide-globe"
      title="还没有规则"
      description="定义你的世界法则，让 AI 生成时遵守约束"
    >
      <template #action>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          添加规则
        </AppButton>
      </template>
    </AppEmpty>

    <!-- 规则列表 -->
    <div v-else class="rule-list">
      <div v-for="rule in rules" :key="rule.id" class="rule-row">
        <div class="rule-main">
          <div class="rule-title-row">
            <span class="rule-title">{{ rule.title }}</span>
            <i
              v-if="rule.isHardConstraint"
              class="i-lucide-lock rule-lock"
              title="硬约束"
            />
          </div>
          <p v-if="rule.description" class="rule-desc">{{ rule.description }}</p>
        </div>
        <div class="rule-meta">
          <AppBadge v-if="rule.category" variant="muted">{{ rule.category }}</AppBadge>
          <AppBadge :variant="priorityVariant(rule.priority)">P{{ rule.priority }}</AppBadge>
        </div>
        <div class="rule-actions">
          <button class="row-action-btn" title="编辑规则" @click="openEdit(rule)">
            <i class="i-lucide-pencil" />
          </button>
          <button class="row-delete-btn" title="删除规则" @click="openDelete(rule)">
            <i class="i-lucide-trash-2" />
          </button>
        </div>
      </div>
    </div>

    <!-- 添加规则抽屉 -->
    <AppDrawer v-model="drawerOpen" title="添加世界观规则" :clear-handler="openCreate" :open-trigger="createTrigger">
      <div class="form-fields">
        <AppInput v-model="createForm.title" label="规则标题 *" placeholder="如：魔法禁止用于战争" />
        <AppTextarea
          v-model="createForm.description"
          label="规则描述"
          placeholder="详细说明这条规则的内容与范围..."
          :rows="3"
        />
        <AppInput v-model="createForm.category" label="分类" placeholder="如：魔法体系、社会制度" />
        <div class="form-row">
          <div class="form-field">
            <label class="field-label">优先级（1-10）</label>
            <input
              v-model.number="createForm.priority"
              type="range"
              min="1"
              max="10"
              class="priority-slider"
            />
            <span class="priority-value">{{ createForm.priority }}</span>
          </div>
          <div class="form-field">
            <label class="field-label">是否硬约束</label>
            <div class="toggle-row">
              <button
                :class="['toggle-btn', { 'toggle-btn--active': createForm.isHardConstraint }]"
                @click="createForm.isHardConstraint = !createForm.isHardConstraint"
              >
                <i
                  :class="createForm.isHardConstraint ? 'i-lucide-lock' : 'i-lucide-lock-open'"
                />
                {{ createForm.isHardConstraint ? '硬约束' : '软约束' }}
              </button>
            </div>
          </div>
        </div>
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="drawerOpen = false">取消</AppButton>
        <AppButton
          :loading="createLoading"
          :disabled="!createForm.title.trim()"
          @click="submitCreate"
        >
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 编辑规则抽屉 -->
    <AppDrawer v-model="editDrawerOpen" title="编辑世界观规则" :clear-handler="resetEditForm" :open-trigger="editTrigger">
      <div class="form-fields">
        <AppInput v-model="editForm.title" label="规则标题 *" placeholder="如：魔法禁止用于战争" />
        <AppTextarea
          v-model="editForm.description"
          label="规则描述"
          placeholder="详细说明这条规则的内容与范围..."
          :rows="3"
        />
        <AppInput v-model="editForm.category" label="分类" placeholder="如：魔法体系、社会制度" />
        <div class="form-row">
          <div class="form-field">
            <label class="field-label">优先级（1-10）</label>
            <input
              v-model.number="editForm.priority"
              type="range"
              min="1"
              max="10"
              class="priority-slider"
            />
            <span class="priority-value">{{ editForm.priority }}</span>
          </div>
          <div class="form-field">
            <label class="field-label">是否硬约束</label>
            <div class="toggle-row">
              <button
                :class="['toggle-btn', { 'toggle-btn--active': editForm.isHardConstraint }]"
                @click="editForm.isHardConstraint = !editForm.isHardConstraint"
              >
                <i
                  :class="editForm.isHardConstraint ? 'i-lucide-lock' : 'i-lucide-lock-open'"
                />
                {{ editForm.isHardConstraint ? '硬约束' : '软约束' }}
              </button>
            </div>
          </div>
        </div>
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="editDrawerOpen = false">取消</AppButton>
        <AppButton :loading="editLoading" :disabled="!editForm.title.trim()" @click="submitEdit">
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 删除确认 -->
    <AppConfirm
      :model-value="!!deleteTarget"
      title="删除规则"
      :message="`确定删除规则「${deleteTarget?.title}」吗？`"
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

.agent-launcher-block {
  margin-bottom: 20px;
}

.page__title {
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.rule-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.rule-row {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 14px 16px;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  transition: border-color 0.15s;
}

.rule-row:hover {
  border-color: var(--color-primary);
}

.rule-row:hover .row-delete-btn,
.rule-row:hover .row-action-btn {
  opacity: 1;
}

.rule-main {
  flex: 1;
  min-width: 0;
}

.rule-title-row {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
}

.rule-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.rule-lock {
  font-size: 13px;
  color: var(--color-accent);
}

.rule-desc {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rule-meta {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}

.skeleton-row {
  height: 52px;
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
  flex-shrink: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.row-action-btn {
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
  flex-shrink: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.row-action-btn:hover {
  background-color: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
}

.rule-actions {
  display: flex;
  gap: 4px;
  align-items: center;
  flex-shrink: 0;
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

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.field-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.priority-slider {
  width: 100%;
  accent-color: var(--color-primary);
}

.priority-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-primary);
}

.toggle-row {
  display: flex;
}

.toggle-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-elevated);
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-muted);
  transition:
    background-color 0.15s,
    border-color 0.15s,
    color 0.15s;
}

.toggle-btn--active {
  background-color: color-mix(in srgb, var(--color-accent) 15%, transparent);
  border-color: var(--color-accent);
  color: var(--color-accent);
}
</style>

