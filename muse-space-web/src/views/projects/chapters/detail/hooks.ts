import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getChapter, updateChapter } from '@/api/chapters'
import { useToast } from '@/composables/useToast'
import type { ChapterResponse, UpdateChapterRequest } from '@/types/models'

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

type EditableSection = 'meta' | 'draft' | 'final' | null

export function initChapterDetailState() {
  const route = useRoute()
  const router = useRouter()
  const toast = useToast()

  const projectId = route.params.id as string
  const chapterId = route.params.chapterId as string

  const chapter = ref<ChapterResponse | null>(null)
  const loading = ref(false)
  const saving = ref(false)
  const editingSection = ref<EditableSection>(null)

  // 本地编辑表单
  const metaForm = reactive({ title: '', goal: '', summary: '', status: 0 })
  const draftForm = reactive({ draftText: '' })
  const finalForm = reactive({ finalText: '' })

  async function load() {
    loading.value = true
    try {
      chapter.value = await getChapter(projectId, chapterId)
    } catch {
      router.push(`/projects/${projectId}/chapters`)
    } finally {
      loading.value = false
    }
  }

  function startEdit(section: EditableSection) {
    if (!chapter.value) return
    if (section === 'meta') {
      metaForm.title = chapter.value.title ?? ''
      metaForm.goal = chapter.value.goal ?? ''
      metaForm.summary = chapter.value.summary ?? ''
      metaForm.status = chapter.value.status ?? 0
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

  async function saveEdit() {
    if (!chapter.value || !editingSection.value) return
    saving.value = true
    const payload: UpdateChapterRequest = {}
    if (editingSection.value === 'meta') {
      payload.title = metaForm.title || undefined
      payload.goal = metaForm.goal || undefined
      payload.summary = metaForm.summary || undefined
      payload.status = metaForm.status
    } else if (editingSection.value === 'draft') {
      payload.draftText = draftForm.draftText
    } else if (editingSection.value === 'final') {
      payload.finalText = finalForm.finalText
    }
    try {
      chapter.value = await updateChapter(projectId, chapterId, payload)
      editingSection.value = null
      toast.success('保存成功')
    } catch {
      // handled
    } finally {
      saving.value = false
    }
  }

  const statusLabel = computed(() =>
    chapter.value ? (CHAPTER_STATUS_LABELS[chapter.value.status] ?? '未知') : '',
  )

  onMounted(load)

  return {
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
    statusLabel,
    CHAPTER_STATUS_LABELS,
  }
}
