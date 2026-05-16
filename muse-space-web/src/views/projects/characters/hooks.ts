import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import { getCharacters, createCharacter, updateCharacter, deleteCharacter, generateCharacter, copyCharactersToOutline, getCharacterPool, importFromPool } from '@/api/characters'
import { getStoryOutlines } from '@/api/outlines'
import { useToast } from '@/composables/useToast'
import type { CharacterResponse, StoryOutlineResponse } from '@/types/models'
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

  // ── 大纲选择 ──────────────────────────────────────────
  const outlines = ref<StoryOutlineResponse[]>([])
  const currentOutlineId = ref<string>('')

  const currentOutline = computed(() =>
    outlines.value.find(o => o.id === currentOutlineId.value),
  )

  async function loadOutlines(): Promise<void> {
    outlines.value = await getStoryOutlines(projectId)
    // 默认选中第一个大纲
    if (outlines.value.length > 0 && !currentOutlineId.value) {
      const defaultOutline = outlines.value.find(o => o.isDefault)
      currentOutlineId.value = defaultOutline?.id ?? outlines.value[0].id
    }
  }

  async function switchOutline(outlineId: string): Promise<void> {
    currentOutlineId.value = outlineId
    await loadCharacters()
  }

  const characters = ref<CharacterResponse[]>([])
  const loading = ref(false)

  const drawerOpen = ref(false)
  const createLoading = ref(false)
  const createForm = reactive<CreateCharacterForm>(emptyForm())

  // AI 生成角色（统一入口，支持从原著提取或自由生成）
  const generateDesc = ref('')
  const generateFromNovel = ref(false)
  const generateLoading = ref(false)

  async function generateFromDesc(): Promise<void> {
    const desc = generateDesc.value.trim()
    if (!desc || !currentOutlineId.value) return
    generateLoading.value = true
    try {
      const result = await generateCharacter(projectId, currentOutlineId.value, desc, generateFromNovel.value)
      Object.assign(createForm, {
        name: result.name ?? '',
        age: result.age != null ? String(result.age) : '',
        role: result.role ?? '',
        personalitySummary: result.personalitySummary ?? '',
        motivation: result.motivation ?? '',
        speakingStyle: result.speakingStyle ?? '',
        forbiddenBehaviors: result.forbiddenBehaviors ?? '',
        currentState: result.currentState ?? '',
      })
      editDrawerOpen.value = false
      drawerOpen.value = true
      const hint = generateFromNovel.value
        ? `已从 ${result.sourceChunkCount} 个原著片段中提取，请检查并确认`
        : 'AI 已生成角色信息，请检查并修改'
      toast.success(hint)
    } catch {
      // handled by interceptor
    } finally {
      generateLoading.value = false
    }
  }

  const deleteTarget = ref<CharacterResponse | null>(null)
  const deleteLoading = ref(false)

  async function loadCharacters(): Promise<void> {
    if (!currentOutlineId.value) return
    loading.value = true
    try {
      characters.value = await getCharacters(projectId, currentOutlineId.value)
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
    if (!createForm.name.trim() || !currentOutlineId.value) return
    createLoading.value = true
    try {
      const character = await createCharacter(projectId, currentOutlineId.value, {
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
    if (!deleteTarget.value || !currentOutlineId.value) return
    deleteLoading.value = true
    try {
      await deleteCharacter(projectId, currentOutlineId.value, deleteTarget.value.id)
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
    if (!editTarget.value || !editForm.name.trim() || !currentOutlineId.value) return
    editLoading.value = true
    try {
      const updated = await updateCharacter(projectId, currentOutlineId.value, editTarget.value.id, {
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

  onMounted(async () => {
    await loadOutlines()
    await loadCharacters()
    await loadPool()
  })

  // ── 原著角色池 ────────────────────────────────────────
  const poolCharacters = ref<CharacterResponse[]>([])
  const poolLoading = ref(false)
  const showPoolPanel = ref(false)
  const selectedPoolIds = ref<Set<string>>(new Set())
  const importLoading = ref(false)

  async function loadPool(): Promise<void> {
    poolLoading.value = true
    try {
      poolCharacters.value = await getCharacterPool(projectId)
    } catch {
      // handled
    } finally {
      poolLoading.value = false
    }
  }

  function togglePoolSelect(id: string): void {
    if (selectedPoolIds.value.has(id)) {
      selectedPoolIds.value.delete(id)
    } else {
      selectedPoolIds.value.add(id)
    }
  }

  async function importSelectedToOutline(): Promise<void> {
    if (!currentOutlineId.value || selectedPoolIds.value.size === 0) return
    importLoading.value = true
    try {
      const ids = Array.from(selectedPoolIds.value)
      const imported = await importFromPool(projectId, currentOutlineId.value, ids)
      characters.value.push(...imported)
      selectedPoolIds.value.clear()
      showPoolPanel.value = false
      toast.success(`已引入 ${imported.length} 个原著角色到当前大纲`)
    } catch {
      // handled
    } finally {
      importLoading.value = false
    }
  }

  return {
    // 大纲选择
    outlines,
    currentOutlineId,
    currentOutline,
    switchOutline,
    // 角色列表
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
    generateDesc,
    generateFromNovel,
    generateLoading,
    generateFromDesc,
    // 原著角色池
    poolCharacters,
    poolLoading,
    showPoolPanel,
    selectedPoolIds,
    importLoading,
    loadPool,
    togglePoolSelect,
    importSelectedToOutline,
  }
}
