import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getProjects, createProject, deleteProject } from '@/api/projects'
import { useToast } from '@/composables/useToast'
import type { StoryProjectResponse } from '@/types/models'
import type { CreateProjectForm } from './types'

export function initHomeState() {
  const router = useRouter()
  const toast = useToast()

  const projects = ref<StoryProjectResponse[]>([])
  const loading = ref(false)

  const drawerOpen = ref(false)
  const createLoading = ref(false)
  const createForm = reactive<CreateProjectForm>({
    name: '',
    description: '',
    genre: '',
    narrativePerspective: '',
  })

  const deleteTarget = ref<StoryProjectResponse | null>(null)
  const deleteLoading = ref(false)

  async function loadProjects(): Promise<void> {
    loading.value = true
    try {
      projects.value = await getProjects()
    } catch {
      // error toast shown by http layer
    } finally {
      loading.value = false
    }
  }

  function openCreate(): void {
    Object.assign(createForm, { name: '', description: '', genre: '', narrativePerspective: '' })
    drawerOpen.value = true
  }

  async function submitCreate(): Promise<void> {
    if (!createForm.name.trim()) return
    createLoading.value = true
    try {
      const project = await createProject({
        name: createForm.name,
        description: createForm.description || undefined,
        genre: createForm.genre || undefined,
        narrativePerspective: createForm.narrativePerspective || undefined,
      })
      projects.value.unshift(project)
      drawerOpen.value = false
      toast.success('项目创建成功')
    } catch {
      // handled
    } finally {
      createLoading.value = false
    }
  }

  function openDelete(project: StoryProjectResponse): void {
    deleteTarget.value = project
  }

  function cancelDelete(): void {
    deleteTarget.value = null
  }

  async function confirmDelete(): Promise<void> {
    if (!deleteTarget.value) return
    deleteLoading.value = true
    try {
      await deleteProject(deleteTarget.value.id)
      projects.value = projects.value.filter((p) => p.id !== deleteTarget.value!.id)
      deleteTarget.value = null
      toast.success('项目已删除')
    } catch {
      // handled
    } finally {
      deleteLoading.value = false
    }
  }

  function navigateTo(id: string): void {
    router.push(`/projects/${id}/overview`)
  }

  onMounted(loadProjects)

  return {
    projects,
    loading,
    drawerOpen,
    createForm,
    createLoading,
    openCreate,
    submitCreate,
    deleteTarget,
    deleteLoading,
    openDelete,
    cancelDelete,
    confirmDelete,
    navigateTo,
  }
}
