import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { getWorldRules, createWorldRule, updateWorldRule, deleteWorldRule } from '@/api/worldRules'
import { useToast } from '@/composables/useToast'
import type { WorldRuleResponse } from '@/types/models'
import type { CreateWorldRuleForm } from './types'

const emptyForm = (): CreateWorldRuleForm => ({
  title: '',
  description: '',
  category: '',
  priority: 5,
  isHardConstraint: false,
})

export function initWorldRulesState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string

  const rules = ref<WorldRuleResponse[]>([])
  const loading = ref(false)

  const drawerOpen = ref(false)
  const createLoading = ref(false)
  const createForm = reactive<CreateWorldRuleForm>(emptyForm())

  const deleteTarget = ref<WorldRuleResponse | null>(null)
  const deleteLoading = ref(false)

  async function loadRules(): Promise<void> {
    loading.value = true
    try {
      rules.value = await getWorldRules(projectId)
    } catch {
      // handled
    } finally {
      loading.value = false
    }
  }

  function openCreate(): void {
    Object.assign(createForm, emptyForm())
    drawerOpen.value = true
  }

  async function submitCreate(): Promise<void> {
    if (!createForm.title.trim()) return
    createLoading.value = true
    try {
      const rule = await createWorldRule(projectId, {
        title: createForm.title,
        description: createForm.description || undefined,
        category: createForm.category || undefined,
        priority: createForm.priority,
        isHardConstraint: createForm.isHardConstraint,
      })
      rules.value.push(rule)
      drawerOpen.value = false
      toast.success('规则添加成功')
    } catch {
      // handled
    } finally {
      createLoading.value = false
    }
  }

  async function confirmDelete(): Promise<void> {
    if (!deleteTarget.value) return
    deleteLoading.value = true
    try {
      await deleteWorldRule(projectId, deleteTarget.value.id)
      rules.value = rules.value.filter((r) => r.id !== deleteTarget.value!.id)
      deleteTarget.value = null
      toast.success('规则已删除')
    } catch {
      // handled
    } finally {
      deleteLoading.value = false
    }
  }

  onMounted(loadRules)

  return {
    rules,
    loading,
    drawerOpen,
    createForm,
    createLoading,
    openCreate,
    submitCreate,
    deleteTarget,
    deleteLoading,
    openDelete: (r: WorldRuleResponse) => { deleteTarget.value = r },
    cancelDelete: () => { deleteTarget.value = null },
    confirmDelete,
    ...useEditRule(rules, projectId, toast),
  }
}

function useEditRule(
  rules: ReturnType<typeof ref<WorldRuleResponse[]>>,
  projectId: string,
  toast: ReturnType<typeof import('@/composables/useToast').useToast>,
) {
  const editTarget = ref<WorldRuleResponse | null>(null)
  const editDrawerOpen = ref(false)
  const editForm = reactive<CreateWorldRuleForm>({
    title: '',
    description: '',
    category: '',
    priority: 5,
    isHardConstraint: false,
  })
  const editLoading = ref(false)

  function openEdit(r: WorldRuleResponse): void {
    editTarget.value = r
    Object.assign(editForm, {
      title: r.title,
      description: r.description ?? '',
      category: r.category ?? '',
      priority: r.priority,
      isHardConstraint: r.isHardConstraint,
    })
    editDrawerOpen.value = true
  }

  async function submitEdit(): Promise<void> {
    if (!editTarget.value || !editForm.title.trim()) return
    editLoading.value = true
    try {
      const updated = await updateWorldRule(projectId, editTarget.value.id, {
        title: editForm.title,
        description: editForm.description || undefined,
        category: editForm.category || undefined,
        priority: editForm.priority,
        isHardConstraint: editForm.isHardConstraint,
      })
      const idx = (rules.value ?? []).findIndex((r) => r.id === updated.id)
      if (idx !== -1) rules.value!.splice(idx, 1, updated)
      editDrawerOpen.value = false
      toast.success('规则已更新')
    } catch {
      // handled
    } finally {
      editLoading.value = false
    }
  }

  return { editDrawerOpen, editForm, editLoading, openEdit, submitEdit }
}
