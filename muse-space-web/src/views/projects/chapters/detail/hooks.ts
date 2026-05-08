import { ref, reactive, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRoute, useRouter, onBeforeRouteUpdate } from 'vue-router'
import {
  getChapter,
  updateChapter,
  autoPlanChapter,
  generateChapterDraft,
  adoptChapterDraft,
  type AdoptDraftResponse,
  type GenerateChapterDraftRequest,
} from '@/api/chapters'
import { getCharacters } from '@/api/characters'
import { getNovels } from '@/api/novels'
import { getSuggestions } from '@/api/suggestions'
import { useToast } from '@/composables/useToast'
import { useAgentProgress } from '@/composables/useAgentProgress'
import type {
  AgentSuggestionResponse,
  ChapterResponse,
  CharacterResponse,
  UpdateChapterRequest,
  NovelResponse,
} from '@/types/models'

export const CHAPTER_STATUS_LABELS: Record<number, string> = {
  0: '计划中',
  1: '草稿中',
  2: '修改中',
  3: '已定稿',
}

export const CHAPTER_STATUS_VARIANTS: Record<number, string> = {
  0: 'muted',
  1: 'accent',
  2: 'primary',
  3: 'success',
}

type EditableSection = 'meta' | 'plan' | 'draft' | 'final' | null

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

export function initChapterDetailState() {
  const route = useRoute()
  const router = useRouter()
  const toast = useToast()
  const agentProgress = useAgentProgress()

  const projectId = computed(() => route.params.id as string)
  const chapterId = computed(() => route.params.chapterId as string)

  const chapter = ref<ChapterResponse | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const editingSection = ref<EditableSection>(null)
  const characters = ref<CharacterResponse[]>([])
  const novels = ref<NovelResponse[]>([])
  const consistencySuggestions = ref<AgentSuggestionResponse[]>([])

  // 自动规划/草稿生成 loading
  const autoPlanLoading = ref(false)
  const generateDraftLoading = ref(false)
  let autoPlanTimer: ReturnType<typeof setTimeout> | null = null
  let generateDraftTimer: ReturnType<typeof setTimeout> | null = null

  // ── 一键采用草稿为定稿 ───────────────────────────────────────
  const adoptDraftLoading = ref(false)
  const adoptDraftConfirmVisible = ref(false)
  const adoptDraftConfirmInfo = ref<AdoptDraftResponse | null>(null)

  // 本地编辑表单
  const metaForm = reactive({ title: '', goal: '', summary: '', status: 0 })
  const planForm = reactive({
    conflict: '',
    emotionCurve: '',
    keyCharacterIds: [] as string[],
    mustIncludePoints: [] as string[],
    pointInput: '',
  })
  const draftForm = reactive({ draftText: '' })
  const finalForm = reactive({ finalText: '' })
  const referenceForm = reactive({
    text: '',
    focus: 'Emotion',
    strength: 'Medium',
  })

  // Module E：创作模式表单
  const generationModeForm = reactive({
    mode: 'Original' as 'Original' | 'ContinueFromOriginal' | 'SideStoryFromOriginal' | 'ExpandOrRewrite',
    sourceNovelId: '',
    branchTopic: '',
    originalRangeStart: undefined as number | undefined,
    originalRangeEnd: undefined as number | undefined,
    divergencePolicy: 'SoftCanon' as 'StrictCanon' | 'SoftCanon' | 'AlternateTimeline',
  })

  async function load() {
    loading.value = true
    try {
      // 并行加载章节、角色、原著、一致性建议
      const [chapterData, , ,] = await Promise.all([
        getChapter(projectId.value, chapterId.value),
        characters.value.length === 0
          ? getCharacters(projectId.value).then((res) => { characters.value = res }).catch(() => {})
          : Promise.resolve(),
        novels.value.length === 0
          ? getNovels(projectId.value).then((res) => { novels.value = res }).catch(() => {})
          : Promise.resolve(),
        loadConsistency(),
      ])
      chapter.value = chapterData
    } catch {
      router.push(`/projects/${projectId.value}/chapters`)
    } finally {
      loading.value = false
    }
  }

  async function loadConsistency() {
    try {
      // 直接在服务端按 targetEntityId 过滤，避免全量拉取
      const list = await getSuggestions(projectId.value, {
        category: 'Consistency',
        targetEntityId: chapterId.value,
      })
      consistencySuggestions.value = list
    } catch {
      consistencySuggestions.value = []
    }
  }

  function startEdit(section: EditableSection) {
    if (!chapter.value) return
    if (section === 'meta') {
      metaForm.title = chapter.value.title ?? ''
      metaForm.goal = chapter.value.goal ?? ''
      metaForm.summary = chapter.value.summary ?? ''
      metaForm.status = chapter.value.status ?? 0
    } else if (section === 'plan') {
      planForm.conflict = chapter.value.conflict ?? ''
      planForm.emotionCurve = chapter.value.emotionCurve ?? ''
      planForm.keyCharacterIds = [...(chapter.value.keyCharacterIds ?? [])]
      planForm.mustIncludePoints = [...(chapter.value.mustIncludePoints ?? [])]
      planForm.pointInput = ''
    } else if (section === 'draft') {
      draftForm.draftText = chapter.value.draftText ?? ''
    } else if (section === 'final') {
      finalForm.finalText = chapter.value.finalText ?? ''
    }
    editingSection.value = section
  }

  function cancelEdit() {
    editingSection.value = null
  }

  function togglePlanCharacter(id: string) {
    const idx = planForm.keyCharacterIds.indexOf(id)
    if (idx === -1) planForm.keyCharacterIds.push(id)
    else planForm.keyCharacterIds.splice(idx, 1)
  }

  function addPlanPoint() {
    const v = planForm.pointInput.trim()
    if (!v) return
    planForm.mustIncludePoints.push(v)
    planForm.pointInput = ''
  }
  function removePlanPoint(idx: number) {
    planForm.mustIncludePoints.splice(idx, 1)
  }

  async function saveEdit() {
    if (!chapter.value || !editingSection.value) return
    saving.value = true
    const payload: UpdateChapterRequest = {}
    if (editingSection.value === 'meta') {
      payload.title = metaForm.title || undefined
      payload.goal = metaForm.goal || undefined
      payload.summary = metaForm.summary || undefined
      payload.status = metaForm.status
    } else if (editingSection.value === 'plan') {
      payload.conflict = planForm.conflict
      payload.emotionCurve = planForm.emotionCurve
      payload.keyCharacterIds = [...planForm.keyCharacterIds]
      payload.mustIncludePoints = [...planForm.mustIncludePoints]
    } else if (editingSection.value === 'draft') {
      payload.draftText = draftForm.draftText
    } else if (editingSection.value === 'final') {
      payload.finalText = finalForm.finalText
    }
    try {
      chapter.value = await updateChapter(projectId.value, chapterId.value, payload)
      editingSection.value = null
      toast.success('保存成功')
    } catch {
      // handled
    } finally {
      saving.value = false
    }
  }

  // ── 自动规划 ────────────────────────────────────────────────
  async function triggerAutoPlan() {
    if (autoPlanLoading.value) return
    autoPlanLoading.value = true
    try {
      await autoPlanChapter(projectId.value, chapterId.value)
      toast.success('已提交自动规划任务，请稍候...')
    } catch {
      autoPlanLoading.value = false
    }
  }

  // ── 草稿生成 ────────────────────────────────────────────────
  async function triggerGenerateDraft() {
    if (generateDraftLoading.value) return
    generateDraftLoading.value = true
    try {
      const payload: GenerateChapterDraftRequest = {}
      const referenceText = referenceForm.text.trim()
      if (referenceText) {
        payload.referenceText = referenceText
        payload.referenceFocus = referenceForm.focus
        payload.referenceStrength = referenceForm.strength
      }
      // Module E：续写/外传模式参数
      if (generationModeForm.mode !== 'Original') {
        payload.generationMode = generationModeForm.mode
        if (generationModeForm.sourceNovelId)
          payload.sourceNovelId = generationModeForm.sourceNovelId
        if (generationModeForm.branchTopic.trim())
          payload.branchTopic = generationModeForm.branchTopic.trim()
        if (generationModeForm.originalRangeStart !== undefined)
          payload.originalRangeStart = generationModeForm.originalRangeStart
        if (generationModeForm.originalRangeEnd !== undefined)
          payload.originalRangeEnd = generationModeForm.originalRangeEnd
        payload.divergencePolicy = generationModeForm.divergencePolicy
      }
      await generateChapterDraft(projectId.value, chapterId.value, payload)
      toast.success('已提交草稿生成任务，请稍候...')
    } catch {
      generateDraftLoading.value = false
    }
  }

  // ── 采用草稿为定稿 ──────────────────────────────────────────
  async function adoptDraft(overrideExisting: boolean) {
    if (!chapter.value) return
    if (adoptDraftLoading.value) return
    if (!chapter.value.draftText || chapter.value.draftText.trim().length === 0) {
      toast.error('草稿为空，无法采用')
      return
    }
    adoptDraftLoading.value = true
    try {
      const res = await adoptChapterDraft(projectId.value, chapterId.value, overrideExisting)
      // 成功：刷新章节、关闭确认框
      adoptDraftConfirmVisible.value = false
      adoptDraftConfirmInfo.value = null
      await load()
      toast.success(`已采用为定稿（${res.finalLength} 字）`)
    } catch (err: unknown) {
      // 409：定稿已有内容，弹二次确认
      const e = err as { response?: { status?: number; data?: { data?: AdoptDraftResponse } } }
      if (e.response?.status === 409 && e.response.data?.data) {
        adoptDraftConfirmInfo.value = e.response.data.data
        adoptDraftConfirmVisible.value = true
      } else {
        toast.error('采用失败')
      }
    } finally {
      adoptDraftLoading.value = false
    }
  }

  function triggerAdoptDraft() {
    void adoptDraft(false)
  }

  function confirmAdoptDraftOverride() {
    void adoptDraft(true)
  }

  function cancelAdoptDraft() {
    adoptDraftConfirmVisible.value = false
    adoptDraftConfirmInfo.value = null
  }

  watch(agentProgress.latestEvent, (ev) => {
    if (!ev) return
    if (ev.taskType === 'chapter-auto-plan') {
      if (ev.stage === 'done') {
        if (autoPlanTimer) { clearTimeout(autoPlanTimer); autoPlanTimer = null }
        autoPlanLoading.value = false
        toast.success(ev.summary ?? '章节计划已自动填充')
        void load()
      } else if (ev.stage === 'failed') {
        if (autoPlanTimer) { clearTimeout(autoPlanTimer); autoPlanTimer = null }
        autoPlanLoading.value = false
        toast.error(ev.error ?? '自动规划失败')
      }
    } else if (ev.taskType === 'chapter-draft') {
      if (ev.stage === 'done') {
        if (generateDraftTimer) { clearTimeout(generateDraftTimer); generateDraftTimer = null }
        generateDraftLoading.value = false
        toast.success(ev.summary ?? '章节草稿已生成')
        void load()
      } else if (ev.stage === 'failed') {
        if (generateDraftTimer) { clearTimeout(generateDraftTimer); generateDraftTimer = null }
        generateDraftLoading.value = false
        toast.error(ev.error ?? '草稿生成失败')
      }
    }
  })

  const statusLabel = computed(() =>
    chapter.value ? (CHAPTER_STATUS_LABELS[chapter.value.status] ?? '未知') : '',
  )

  const characterMap = computed(() => {
    const m: Record<string, CharacterResponse> = {}
    for (const c of characters.value) m[c.id] = c
    return m
  })

  onMounted(async () => {
    await agentProgress.joinProject(projectId.value)
    await load()
  })

  onUnmounted(() => {
    if (autoPlanTimer) clearTimeout(autoPlanTimer)
    if (generateDraftTimer) clearTimeout(generateDraftTimer)
    agentProgress.stop()
  })

  onBeforeRouteUpdate(async (to) => {
    loading.value = true
    editingSection.value = null
    try {
      chapter.value = await getChapter(to.params.id as string, to.params.chapterId as string)
    } catch {
      router.push(`/projects/${String(to.params.id)}/chapters`)
    } finally {
      loading.value = false
    }
  })

  return {
    chapter,
    characters,
    novels,
    characterMap,
    consistencySuggestions,
    loading,
    saving,
    editingSection,
    metaForm,
    planForm,
    draftForm,
    finalForm,
    referenceForm,
    generationModeForm,
    REFERENCE_FOCUS_OPTIONS,
    REFERENCE_STRENGTH_OPTIONS,
    startEdit,
    cancelEdit,
    saveEdit,
    togglePlanCharacter,
    addPlanPoint,
    removePlanPoint,
    triggerAutoPlan,
    triggerGenerateDraft,
    autoPlanLoading,
    generateDraftLoading,
    triggerAdoptDraft,
    confirmAdoptDraftOverride,
    cancelAdoptDraft,
    adoptDraftLoading,
    adoptDraftConfirmVisible,
    adoptDraftConfirmInfo,
    statusLabel,
    CHAPTER_STATUS_LABELS,
  }
}
