<script setup lang="ts">
import AppButton from '@/components/base/AppButton.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppSelect from '@/components/base/AppSelect.vue'
import { initDraftState } from './hooks'

const {
  form, generating, result, elapsed, generate,
  selectedProvider, availableModels, selectedModel, providerSwitching,
  onProviderChange, onModelChange,
} = initDraftState()
</script>

<template>
  <div class="draft-layout">
    <!-- 左：输入参数 -->
    <div class="draft-form-panel">
      <h2 class="panel-title">场景参数</h2>
      <div class="form-fields">
        <AppTextarea
          v-model="form.sceneGoal"
          label="场景目标 *"
          placeholder="这个场景要完成什么叙事任务？&#10;例：主角第一次见到导师，感受到强烈的压迫感"
          :rows="4"
        />
        <AppTextarea
          v-model="form.conflict"
          label="核心冲突"
          placeholder="场景中存在什么矛盾或张力？"
          :rows="3"
        />
        <AppInput
          v-model="form.emotionCurve"
          label="情绪弧线"
          placeholder="如：紧张 → 震撼 → 压抑"
        />
      </div>

      <!-- AI 配置 -->
      <div class="ai-config">
        <p class="ai-config__label">AI 配置</p>
        <!-- 渠道切换 -->
        <div class="provider-toggle">
          <button
            :class="['provider-btn', { active: selectedProvider === 'OpenRouter' }]"
            :disabled="providerSwitching || generating"
            @click="onProviderChange('OpenRouter')"
          >
            OpenRouter
          </button>
          <button
            :class="['provider-btn', { active: selectedProvider === 'DeepSeek' }]"
            :disabled="providerSwitching || generating"
            @click="onProviderChange('DeepSeek')"
          >
            DeepSeek
          </button>
        </div>
        <!-- 模型选择（仅 OpenRouter 渠道显示） -->
        <AppSelect
          v-if="selectedProvider === 'OpenRouter' && availableModels.length > 0"
          :model-value="selectedModel"
          :options="availableModels.map(m => ({ value: m.id, label: `${m.label}  (${m.id})` }))"
          :disabled="providerSwitching || generating"
          @update:model-value="onModelChange"
        />
        <p v-else-if="selectedProvider === 'DeepSeek'" class="ai-config__hint">
          使用 DeepSeek 默认模型
        </p>
      </div>

      <div class="form-actions">
        <AppButton
          size="lg"
          style="width: 100%"
          :loading="generating"
          :disabled="!form.sceneGoal.trim()"
          @click="generate"
        >
          <i class="i-lucide-sparkles" />
          {{ generating ? '生成中...' : '生成草稿' }}
        </AppButton>
      </div>
    </div>

    <!-- 右：生成结果 -->
    <div class="draft-result-panel">
      <h2 class="panel-title">生成结果</h2>
      <div class="result-body">
        <!-- 初始空状态 -->
        <div v-if="!generating && !result" class="result-empty">
          <i class="i-lucide-file-text result-empty__icon" />
          <p class="result-empty__text">填写左侧参数后点击「生成草稿」</p>
        </div>

        <!-- 生成中 -->
        <div v-else-if="generating" class="result-loading">
          <div class="spinner" />
          <p class="loading-text">AI 生成中...</p>
          <p class="elapsed-text">已等待 {{ elapsed }} 秒</p>
        </div>

        <!-- 结果 -->
        <div v-else-if="result" class="result-content">
          <div class="result-meta">
            <AppBadge v-if="result.skillName" variant="primary">{{ result.skillName }}</AppBadge>
            <AppBadge v-if="result.promptVersion" variant="muted">{{ result.promptVersion }}</AppBadge>
            <AppBadge v-if="result.durationMs" variant="muted">
              耗时 {{ (result.durationMs / 1000).toFixed(1) }}s
            </AppBadge>
          </div>
          <div class="result-text">{{ result.generatedText }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.draft-layout {
  display: grid;
  grid-template-columns: 400px 1fr;
  gap: 20px;
  height: 100%;
  min-height: 0;
}

.draft-form-panel,
.draft-result-panel {
  display: flex;
  flex-direction: column;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 20px;
  overflow: hidden;
}

.panel-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 16px;
  flex-shrink: 0;
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 14px;
  flex: 1;
  overflow-y: auto;
}

.form-actions {
  margin-top: 16px;
  flex-shrink: 0;
}

/* AI 配置区块 */
.ai-config {
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px solid var(--color-border);
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.ai-config__label {
  font-size: 12px;
  font-weight: 500;
  color: var(--color-text-muted);
  margin: 0;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.provider-toggle {
  display: flex;
  gap: 6px;
}

.provider-btn {
  flex: 1;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
  background-color: var(--color-bg-page);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.15s;
}

.provider-btn:hover:not(:disabled) {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.provider-btn.active {
  background-color: var(--color-primary);
  border-color: var(--color-primary);
  color: #fff;
}

.provider-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.ai-config__hint {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

.result-body {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}

.result-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  flex: 1;
  gap: 12px;
  opacity: 0.45;
}

.result-empty__icon {
  font-size: 48px;
  color: var(--color-text-muted);
}

.result-empty__text {
  font-size: 14px;
  color: var(--color-text-muted);
  margin: 0;
}

.result-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  flex: 1;
  gap: 12px;
}

.spinner {
  width: 36px;
  height: 36px;
  border: 3px solid var(--color-border);
  border-top-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.loading-text {
  font-size: 15px;
  font-weight: 500;
  color: var(--color-text-primary);
  margin: 0;
}

.elapsed-text {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
}

.result-content {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.result-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.result-text {
  font-size: 14px;
  line-height: 1.85;
  color: var(--color-text-primary);
  white-space: pre-wrap;
  font-family: 'Georgia', 'Noto Serif SC', serif;
}
</style>

