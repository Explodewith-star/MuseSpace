<script setup lang="ts">
import { useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppCard from '@/components/base/AppCard.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AgentLauncher from '@/components/base/AgentLauncher.vue'
import PendingSuggestionPanel from '@/components/base/PendingSuggestionPanel.vue'
import { initStyleProfileState } from './hooks'

const route = useRoute()
const projectId = route.params.id as string

const { loading, saveLoading, form, saveProfile } = initStyleProfileState()
</script>

<template>
  <div class="page">
    <div class="page__header">
      <div>
        <h2 class="page__title">文风配置</h2>
        <p class="page__subtitle">设定写作基调，让 AI 生成时保持统一风格</p>
      </div>
      <AppButton :loading="saveLoading" :disabled="!form.name.trim()" @click="saveProfile">
        <i class="i-lucide-save" />
        保存
      </AppButton>
    </div>

    <!-- D3-2 Agent 工作台：从原著提取候选文风画像 -->
    <AgentLauncher
      class="agent-launcher-block"
      :project-id="projectId"
      title="文风提取 Agent"
      description="从已导入原著中归纳候选文风画像。结果进入建议中心后可确认并应用为项目文风。"
      :default-agent-type="'styleprofile-extract'"
      placeholder="可选：补充约束，例如“重点记录对话风格和叙事节奏”"
      suggestion-category="StyleProfile"
      :presets="[{ label: '从原著提取文风', agentType: 'styleprofile-extract', icon: 'i-lucide-feather' }]"
    />

    <PendingSuggestionPanel
      :project-id="projectId"
      :categories="['StyleConsistency']"
      title="待处理文风偏离"
    />

    <!-- 骨架屏 -->
    <div v-if="loading" class="form-skeleton">
      <AppSkeleton v-for="i in 6" :key="i" width="100%" height="60px" style="margin-bottom:14px" />
    </div>

    <!-- 表单 -->
    <AppCard v-else>
      <div class="form-fields">
        <AppInput
          v-model="form.name"
          label="配置名称 *"
          placeholder="如：主文档文风 v1"
        />

        <div class="form-row">
          <AppInput v-model="form.tone" label="语气基调" placeholder="如：悲壮、轻松、压抑" />
          <AppInput
            v-model="form.sentenceLengthPreference"
            label="句长偏好"
            placeholder="如：长句为主，短句点缀"
          />
        </div>

        <div class="form-row">
          <AppInput
            v-model="form.dialogueRatio"
            label="对话比例"
            placeholder="如：30% 对话，70% 叙事"
          />
          <AppInput
            v-model="form.descriptionDensity"
            label="描写密度"
            placeholder="如：高密度环境描写"
          />
        </div>

        <AppTextarea
          v-model="form.forbiddenExpressions"
          label="禁止表达"
          placeholder="列举不希望出现的词汇或句式，每行一条..."
          :rows="3"
        />

        <AppTextarea
          v-model="form.sampleReferenceText"
          label="参考文本"
          placeholder="粘贴一段你喜欢的文风样本，AI 会参考其风格进行生成..."
          :rows="6"
        />
      </div>
    </AppCard>
  </div>
</template>

<style scoped>
.page__header {
  display: flex;
  align-items: flex-start;
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
  margin: 0 0 4px;
}

.page__subtitle {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
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
</style>

