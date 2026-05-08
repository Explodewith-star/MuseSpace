import { ref, reactive, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { generateSceneDraft } from '@/api/draft'
import { getLlmProvider } from '@/api/llmProvider'
import { useToast } from '@/composables/useToast'
import type { GenerateSceneDraftResponse, LlmProviderType } from '@/types/models'
import type { SceneDraftForm } from './types'

export const REFERENCE_FOCUS_OPTIONS = [
  { value: 'Emotion', label: '情绪氛围' },
  { value: 'Dialogue', label: '对话方式' },
  { value: 'NarrativeRhythm', label: '叙事节奏' },
  { value: 'StyleTexture', label: '文风质感' },
  { value: 'SceneStructure', label: '场景结构' },
  { value: 'InteractionTension', label: '人物互动' },
]

export const REFERENCE_STRENGTH_OPTIONS = [
  { value: 'Low', label: '轻度参考' },
  { value: 'Medium', label: '中度参考' },
  { value: 'High', label: '强参考' },
]

export function initDraftState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string
  const STORAGE_KEY = `muse-draft-form-${projectId}`

  const form = reactive<SceneDraftForm>({
    sceneGoal: '',
    conflict: '',
    emotionCurve: '',
    referenceText: '',
    referenceFocus: 'Emotion',
    referenceStrength: 'Medium',
  })

  // 从 localStorage 恢复表单
  onMounted(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY)
      if (saved) {
        const parsed = JSON.parse(saved) as Partial<SceneDraftForm>
        if (parsed.sceneGoal) form.sceneGoal = parsed.sceneGoal
        if (parsed.conflict) form.conflict = parsed.conflict
        if (parsed.emotionCurve) form.emotionCurve = parsed.emotionCurve
        if (parsed.referenceText) form.referenceText = parsed.referenceText
        if (parsed.referenceFocus) form.referenceFocus = parsed.referenceFocus
        if (parsed.referenceStrength) form.referenceStrength = parsed.referenceStrength
      }
    } catch {
      // ignore
    }
  })

  // 自动保存到 localStorage
  watch(
    () => ({ ...form }),
    (val) => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(val))
    },
    { deep: true },
  )

  const generating = ref(false)
  const result = ref<GenerateSceneDraftResponse | null>(null)
  const elapsed = ref(0)

  // ── AI 渠道 & 模型（只读展示，全局切换在顶部用户菜单） ──────────────
  const selectedProvider = ref<LlmProviderType>('OpenRouter')
  const selectedModel = ref('')

  onMounted(async () => {
    try {
      const status = await getLlmProvider()
      selectedProvider.value = status.active
      selectedModel.value = status.currentModel
    } catch {
      // 获取失败时不影响生成功能
    }
  })

  let timer: ReturnType<typeof setInterval> | null = null

  function startTimer(): void {
    elapsed.value = 0
    timer = setInterval(() => {
      elapsed.value++
    }, 1000)
  }

  function stopTimer(): void {
    if (timer !== null) {
      clearInterval(timer)
      timer = null
    }
  }

  async function generate(): Promise<void> {
    if (!form.sceneGoal.trim()) return
    generating.value = true
    result.value = null
    startTimer()
    try {
      result.value = await generateSceneDraft({
        storyProjectId: projectId,
        sceneGoal: form.sceneGoal,
        conflict: form.conflict || undefined,
        emotionCurve: form.emotionCurve || undefined,
        referenceText: form.referenceText.trim() || undefined,
        referenceFocus: form.referenceText.trim() ? form.referenceFocus : undefined,
        referenceStrength: form.referenceText.trim() ? form.referenceStrength : undefined,
      })
      localStorage.removeItem(STORAGE_KEY)
      toast.success('草稿生成完成')
    } catch {
      // handled
    } finally {
      stopTimer()
      generating.value = false
    }
  }

  return {
    form, generating, result, elapsed, generate,
    selectedProvider, selectedModel,
    REFERENCE_FOCUS_OPTIONS,
    REFERENCE_STRENGTH_OPTIONS,
  }
}
