import { ref, reactive, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { generateSceneDraft } from '@/api/draft'
import { getLlmProvider, setLlmProvider, setLlmModel } from '@/api/llmProvider'
import { useToast } from '@/composables/useToast'
import type { GenerateSceneDraftResponse, LlmModelOption, LlmProviderType } from '@/types/models'
import type { SceneDraftForm } from './types'

export function initDraftState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string
  const STORAGE_KEY = `muse-draft-form-${projectId}`

  const form = reactive<SceneDraftForm>({
    sceneGoal: '',
    conflict: '',
    emotionCurve: '',
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
      }
    } catch {
      // ignore
    }
  })

  // 自动保存到 localStorage
  watch(
    () => ({ sceneGoal: form.sceneGoal, conflict: form.conflict, emotionCurve: form.emotionCurve }),
    (val) => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(val))
    },
    { deep: true },
  )

  const generating = ref(false)
  const result = ref<GenerateSceneDraftResponse | null>(null)
  const elapsed = ref(0)

  // ── AI 渠道 & 模型 ──────────────────────────────────────────────
  const selectedProvider = ref<LlmProviderType>('OpenRouter')
  const availableModels = ref<LlmModelOption[]>([])
  const selectedModel = ref('')
  const providerSwitching = ref(false)

  onMounted(async () => {
    try {
      const status = await getLlmProvider()
      selectedProvider.value = status.active
      availableModels.value = status.availableModels
      selectedModel.value = status.currentModel
    } catch {
      // 获取失败时不影响生成功能
    }
  })

  async function onProviderChange(provider: LlmProviderType): Promise<void> {
    if (provider === selectedProvider.value) return
    providerSwitching.value = true
    try {
      const status = await setLlmProvider(provider)
      selectedProvider.value = status.active
      selectedModel.value = status.currentModel
      toast.success(`已切换至 ${provider}`)
    } catch {
      toast.error('切换渠道失败')
    } finally {
      providerSwitching.value = false
    }
  }

  async function onModelChange(modelId: string): Promise<void> {
    if (modelId === selectedModel.value) return
    providerSwitching.value = true
    try {
      const status = await setLlmModel(modelId)
      selectedModel.value = status.currentModel
      toast.success(`已切换：${modelId}`)
    } catch {
      toast.error('切换模型失败')
    } finally {
      providerSwitching.value = false
    }
  }

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
    selectedProvider, availableModels, selectedModel, providerSwitching,
    onProviderChange, onModelChange,
  }
}
