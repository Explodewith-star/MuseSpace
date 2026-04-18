import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { getChapters, createChapter, deleteChapter } from '@/api/chapters'
import { useToast } from '@/composables/useToast'
import type { ChapterResponse } from '@/types/models'
import type { CreateChapterForm } from './types'

export function initChaptersState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string

  const chapters = ref<ChapterResponse[]>([])
  const loading = ref(false)

  const drawerOpen = ref(false)
  const createLoading = ref(false)
  const createForm = reactive<CreateChapterForm>({ number: '', title: '', summary: '', goal: '' })

  const deleteTarget = ref<ChapterResponse | null>(null)
  const deleteLoading = ref(false)

  async function loadChapters(): Promise<void> {
    loading.value = true
    try {
      const list = await getChapters(projectId)
      chapters.value = list.sort((a, b) => a.number - b.number)
    } catch {
      // handled
    } finally {
      loading.value = false
    }
  }

  function openCreate(): void {
    Object.assign(createForm, { number: '', title: '', summary: '', goal: '' })
    drawerOpen.value = true
  }

  async function submitCreate(): Promise<void> {
    const num = parseInt(createForm.number)
    if (!num || num < 1) return
    createLoading.value = true
    try {
      const chapter = await createChapter(projectId, {
        number: num,
        title: createForm.title || undefined,
        summary: createForm.summary || undefined,
        goal: createForm.goal || undefined,
      })
      chapters.value.push(chapter)
      chapters.value.sort((a, b) => a.number - b.number)
      drawerOpen.value = false
      toast.success('章节添加成功')
    } catch {
      // handled
    } finally {
      createLoading.value = false
    }
  }

  function openDelete(chapter: ChapterResponse): void {
    deleteTarget.value = chapter
  }

  async function confirmDelete(): Promise<void> {
    if (!deleteTarget.value) return
    deleteLoading.value = true
    try {
      await deleteChapter(projectId, deleteTarget.value.id)
      chapters.value = chapters.value.filter((c) => c.id !== deleteTarget.value!.id)
      deleteTarget.value = null
      toast.success('章节已删除')
    } catch {
      // handled
    } finally {
      deleteLoading.value = false
    }
  }

  onMounted(loadChapters)

  return {
    chapters,
    loading,
    drawerOpen,
    createForm,
    createLoading,
    openCreate,
    submitCreate,
    deleteTarget,
    deleteLoading,
    openDelete,
    cancelDelete: () => { deleteTarget.value = null },
    confirmDelete,
  }
}
