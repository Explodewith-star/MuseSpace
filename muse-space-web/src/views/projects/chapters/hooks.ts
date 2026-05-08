import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import {
  getChapters,
  createChapter,
  deleteChapter,
  batchReorderChapters,
} from '@/api/chapters'
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

  const reorderLoading = ref(false)

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

  /**
   * 重排所有章节编号：按当前列表顺序（已按 Number 升序）重新赋值 1..N，
   * 用于消除删除章节后编号出现的空洞。
   */
  async function reorderAll(): Promise<void> {
    if (reorderLoading.value || chapters.value.length === 0) return
    // 检查是否真的有空洞或不连续
    const ids = chapters.value.map((c) => c.id)
    const hasGap = chapters.value.some((c, i) => c.number !== i + 1)
    if (!hasGap) {
      toast.success('章节编号已是连续 1..N，无需重排')
      return
    }
    reorderLoading.value = true
    try {
      const updated = await batchReorderChapters(projectId, ids, 1)
      // 本地同步：直接按顺序重赋 number，避免再发一次 GET
      chapters.value = chapters.value.map((c, i) => ({ ...c, number: i + 1 }))
      toast.success(`重排完成，共更新 ${updated} 个章节`)
    } catch {
      // handled
    } finally {
      reorderLoading.value = false
    }
  }

  return {
    chapters,
    loading,
    loadChapters,
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
    reorderAll,
    reorderLoading,
  }
}
