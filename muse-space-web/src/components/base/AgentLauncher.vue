<script setup lang="ts">
/**
 * D3-2 菜单 Agent 化统一启动器。
 *
 * 用法：在任意菜单页放一个 <AgentLauncher>，传入：
 *  - menuKey：当前菜单标识，用于本地缓存最近一次输入
 *  - presets：可选的快捷动作列表（预设 agentType）
 *  - 默认 agentType：当用户不点 preset 直接输入时的兜底类型
 *
 * 组件统一负责：
 *  1. 收集"一句话目标"和（可选）下拉选择的 Agent 类型
 *  2. 调 /agent-tasks 触发后端
 *  3. 订阅 SignalR 进度并展示进度条
 *  4. 完成后给出"前往建议中心"的跳转
 */
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppCard from '@/components/base/AppCard.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import { triggerAgentTask, type AgentType, type ConsistencyScope } from '@/api/agentTasks'
import { useAgentProgress } from '@/composables/useAgentProgress'
import { useToast } from '@/composables/useToast'

interface Preset {
  /** 显示用标签，例如 "提取角色" */
  label: string
  /** 触发的 Agent 类型 */
  agentType: AgentType
  /** 可选图标 class（unocss icon），如 'i-lucide-users' */
  icon?: string
}

interface Props {
  projectId: string
  /** 标题，例如 "角色 Agent 工作台" */
  title?: string
  /** 副标题/说明 */
  description?: string
  /** 预设动作（最多 4 个） */
  presets?: Preset[]
  /** 默认 agentType（当用户直接输入而不点 preset 时使用） */
  defaultAgentType?: AgentType
  /** placeholder */
  placeholder?: string
  /** 是否显示"前往建议中心"链接 */
  showSuggestionsLink?: boolean
  /** 当前菜单的建议类目，用于跳转过滤 */
  suggestionCategory?: string
  /** 章节 ID（一致性审查 / 章节自动规划 时附带） */
  chapterId?: string
  /** 一致性审查的文本来源；默认 latest-draft */
  scope?: ConsistencyScope
  /** scope=raw-text 时使用的自定义文本 */
  rawText?: string
}

const props = withDefaults(defineProps<Props>(), {
  title: 'AI 助手',
  description: '描述你想做的事，AI 完成后结果会进入建议中心等待你确认。',
  presets: () => [],
  placeholder: '一句话描述你的目标，例如"提取所有重要配角"',
  showSuggestionsLink: true,
})

const emit = defineEmits<{
  (e: 'done', taskType: string): void
  (e: 'failed', error?: string): void
}>()

const router = useRouter()
const toast = useToast()
const userInput = ref('')
const submitting = ref(false)
const lastTaskType = ref<string | null>(null)

// ── SignalR 进度订阅 ─────────────────────────────────────────
const { latestEvent, joinProject, leaveProject } = useAgentProgress()

const currentStage = computed(() => {
  const e = latestEvent.value
  if (!e || !lastTaskType.value) return null
  if (e.taskType !== lastTaskType.value) return null
  return e
})

const stageLabels: Record<string, string> = {
  started: '正在准备...',
  generating: 'AI 正在思考...',
  done: '已完成',
  failed: '失败',
}

watch(currentStage, (e) => {
  if (!e) return
  if (e.stage === 'done') {
    toast.success(e.summary ?? '任务已完成')
    emit('done', e.taskType)
  } else if (e.stage === 'failed') {
    toast.error(e.error ?? '任务失败')
    emit('failed', e.error)
  }
})

onMounted(() => joinProject(props.projectId))
onUnmounted(() => leaveProject(props.projectId))

// ── 触发 Agent ─────────────────────────────────────────────
async function runWith(agentType: AgentType) {
  if (submitting.value) return
  submitting.value = true
  try {
    const resp = await triggerAgentTask(props.projectId, {
      agentType,
      userInput: userInput.value.trim() || undefined,
      chapterId: props.chapterId,
      scope: props.scope,
      rawText: props.rawText,
    })
    lastTaskType.value = resp.taskType
    toast.success('任务已提交，请关注下方进度')
  } catch {
    // global interceptor handles
  } finally {
    submitting.value = false
  }
}

function runDefault() {
  if (!props.defaultAgentType) return
  runWith(props.defaultAgentType)
}

function goSuggestions() {
  const query = props.suggestionCategory ? { category: props.suggestionCategory } : undefined
  router.push({ path: `/projects/${props.projectId}/suggestions`, query })
}

const isRunning = computed(
  () => currentStage.value?.stage === 'started' || currentStage.value?.stage === 'generating',
)
</script>

<template>
  <AppCard class="agent-launcher">
    <header class="launcher-head">
      <div class="head-text">
        <h3 class="launcher-title">
          <i class="i-lucide-sparkles" />
          {{ title }}
        </h3>
        <p class="launcher-desc">{{ description }}</p>
      </div>
      <AppButton
        v-if="showSuggestionsLink"
        variant="ghost"
        size="sm"
        @click="goSuggestions"
      >
        <i class="i-lucide-inbox" />
        建议中心
      </AppButton>
    </header>

    <AppTextarea
      v-model="userInput"
      :placeholder="placeholder"
      :rows="2"
      class="launcher-input"
    />

    <div class="launcher-actions">
      <AppButton
        v-for="p in presets"
        :key="p.agentType"
        variant="secondary"
        size="sm"
        :loading="submitting"
        :disabled="isRunning"
        @click="runWith(p.agentType)"
      >
        <i v-if="p.icon" :class="p.icon" />
        {{ p.label }}
      </AppButton>
      <AppButton
        v-if="defaultAgentType"
        :loading="submitting"
        :disabled="isRunning || !userInput.trim()"
        @click="runDefault"
      >
        <i class="i-lucide-send" />
        提交
      </AppButton>
    </div>

    <!-- 进度条 -->
    <div
      v-if="currentStage && currentStage.stage !== 'done'"
      class="launcher-progress"
      :class="`stage-${currentStage.stage}`"
    >
      <i v-if="currentStage.stage === 'failed'" class="i-lucide-alert-circle" />
      <i v-else class="i-lucide-loader-2 spin" />
      <span>{{ stageLabels[currentStage.stage] ?? currentStage.stage }}</span>
      <span v-if="currentStage.error" class="progress-error">{{ currentStage.error }}</span>
    </div>
    <div v-else-if="currentStage?.stage === 'done'" class="launcher-progress stage-done">
      <i class="i-lucide-check-circle-2" />
      <span>{{ currentStage.summary ?? '已完成，请前往建议中心查看' }}</span>
      <AppButton variant="ghost" size="sm" @click="goSuggestions">前往查看 →</AppButton>
    </div>
  </AppCard>
</template>

<style scoped>
.agent-launcher {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.launcher-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.head-text {
  flex: 1;
  min-width: 0;
}

.launcher-title {
  display: flex;
  align-items: center;
  gap: 6px;
  margin: 0 0 4px;
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.launcher-desc {
  margin: 0;
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.launcher-input {
  width: 100%;
}

.launcher-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.launcher-progress {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 8px;
  font-size: 13px;
  background: var(--color-bg-muted);
}

.launcher-progress.stage-failed {
  background: rgba(220, 38, 38, 0.08);
  color: rgb(185, 28, 28);
}

.launcher-progress.stage-done {
  background: rgba(34, 197, 94, 0.08);
  color: rgb(21, 128, 61);
}

.progress-error {
  color: rgb(185, 28, 28);
  font-size: 12px;
}

.spin {
  animation: launcher-spin 1s linear infinite;
}

@keyframes launcher-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
