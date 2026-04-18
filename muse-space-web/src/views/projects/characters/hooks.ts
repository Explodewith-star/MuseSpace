import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { getCharacters, createCharacter, updateCharacter, deleteCharacter } from '@/api/characters'
import { useToast } from '@/composables/useToast'
import type { CharacterResponse } from '@/types/models'
import type { CreateCharacterForm } from './types'

const emptyForm = (): CreateCharacterForm => ({
  name: '',
  age: '',
  role: '',
  personalitySummary: '',
  motivation: '',
  speakingStyle: '',
  forbiddenBehaviors: '',
  publicSecrets: '',
  privateSecrets: '',
  currentState: '',
  tags: '',
})

export function initCharactersState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string

  const characters = ref<CharacterResponse[]>([])
  const loading = ref(false)

  const drawerOpen = ref(false)
  const createLoading = ref(false)
  const createForm = reactive<CreateCharacterForm>(emptyForm())

  const deleteTarget = ref<CharacterResponse | null>(null)
  const deleteLoading = ref(false)

  async function loadCharacters(): Promise<void> {
    loading.value = true
    try {
      characters.value = await getCharacters(projectId)
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
    if (!createForm.name.trim()) return
    createLoading.value = true
    try {
      const character = await createCharacter(projectId, {
        name: createForm.name,
        age: createForm.age ? parseInt(createForm.age) : undefined,
        role: createForm.role || undefined,
        personalitySummary: createForm.personalitySummary || undefined,
        motivation: createForm.motivation || undefined,
        speakingStyle: createForm.speakingStyle || undefined,
        forbiddenBehaviors: createForm.forbiddenBehaviors || undefined,
        publicSecrets: createForm.publicSecrets || undefined,
        privateSecrets: createForm.privateSecrets || undefined,
        currentState: createForm.currentState || undefined,
        tags: createForm.tags || undefined,
      })
      characters.value.push(character)
      drawerOpen.value = false
      toast.success('角色添加成功')
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
      await deleteCharacter(projectId, deleteTarget.value.id)
      characters.value = characters.value.filter((c) => c.id !== deleteTarget.value!.id)
      deleteTarget.value = null
      toast.success('角色已删除')
    } catch {
      // handled
    } finally {
      deleteLoading.value = false
    }
  }

  // ── 编辑 ──────────────────────────────────────────────
  const editTarget = ref<CharacterResponse | null>(null)
  const editDrawerOpen = ref(false)
  const editForm = reactive<CreateCharacterForm>(emptyForm())
  const editLoading = ref(false)

  function openEdit(c: CharacterResponse): void {
    editTarget.value = c
    Object.assign(editForm, {
      name: c.name,
      age: c.age != null ? String(c.age) : '',
      role: c.role ?? '',
      personalitySummary: c.personalitySummary ?? '',
      motivation: c.motivation ?? '',
      speakingStyle: c.speakingStyle ?? '',
      forbiddenBehaviors: c.forbiddenBehaviors ?? '',
      publicSecrets: '',
      privateSecrets: '',
      currentState: c.currentState ?? '',
      tags: c.tags ?? '',
    })
    editDrawerOpen.value = true
  }

  async function submitEdit(): Promise<void> {
    if (!editTarget.value || !editForm.name.trim()) return
    editLoading.value = true
    try {
      const updated = await updateCharacter(projectId, editTarget.value.id, {
        name: editForm.name,
        age: editForm.age ? parseInt(editForm.age) : undefined,
        role: editForm.role || undefined,
        personalitySummary: editForm.personalitySummary || undefined,
        motivation: editForm.motivation || undefined,
        speakingStyle: editForm.speakingStyle || undefined,
        forbiddenBehaviors: editForm.forbiddenBehaviors || undefined,
        publicSecrets: editForm.publicSecrets || undefined,
        privateSecrets: editForm.privateSecrets || undefined,
        currentState: editForm.currentState || undefined,
        tags: editForm.tags || undefined,
      })
      const idx = characters.value.findIndex((c) => c.id === updated.id)
      if (idx !== -1) characters.value.splice(idx, 1, updated)
      editDrawerOpen.value = false
      toast.success('角色已更新')
    } catch {
      // handled
    } finally {
      editLoading.value = false
    }
  }

  onMounted(loadCharacters)

  return {
    characters,
    loading,
    drawerOpen,
    createForm,
    createLoading,
    openCreate,
    submitCreate,
    deleteTarget,
    deleteLoading,
    openDelete: (c: CharacterResponse) => { deleteTarget.value = c },
    cancelDelete: () => { deleteTarget.value = null },
    confirmDelete,
    editDrawerOpen,
    editForm,
    editLoading,
    openEdit,
    submitEdit,
  }
}
