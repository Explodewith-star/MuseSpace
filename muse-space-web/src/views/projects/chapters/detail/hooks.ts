import { ref, reactive, onMounted, onUnmounted, computed, watch } from 'vue'
import { useRoute, useRouter, onBeforeRouteUpdate } from 'vue-router'
import {
  getChapter,
  updateChapter,
  autoPlanChapter,
  generateChapterDraft,
} from '@/api/chapters'
import { getCharacters } from '@/api/characters'
import { getSuggestions } from '@/api/suggestions'
import { useToast } from '@/composables/useToast'
import { useAgentProgress } from '@/composables/useAgentProgress'
import type {
  AgentSuggestionResponse,
  ChapterResponse,
  CharacterResponse,
  UpdateChapterRequest,
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
  const consistencySuggestions = ref<AgentSuggestionResponse[]>([])

  // 自动规划/草稿生成 loading
  const autoPlanLoading = ref(false)
  const generateDraftLoading = ref(false)
  let autoPlanTimer: ReturnType<typeof setTimeout> | null = null
  let generateDraftTimer: ReturnType<typeof setTimeout> | null = null

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

  async function load() {
    loading.value = true
    try {
      // 并行加载章节、角色、一致性建议
      const [chapterData, , ] = await Promise.all([
        getChapter(projectId.value, chapterId.value),
        characters.value.length === 0
          ? getCharacters(projectId.value).then((res) => { characters.value = res }).catch(() => {})
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
      await generateChapterDraft(projectId.value, chapterId.value)
      toast.success('已提交草稿生成任务，请稍候...')
    } catch {
      generateDraftLoading.value = false
    }
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
    characterMap,
    consistencySuggestions,
    loading,
    saving,
    editingSection,
    metaForm,
    planForm,
    draftForm,
    finalForm,
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
    statusLabel,
    CHAPTER_STATUS_LABELS,
  }
}
