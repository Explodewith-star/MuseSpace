import { ref, reactive, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getCharacters, createCharacter, updateCharacter, deleteCharacter, generateCharacter, createInPool } from '@/api/characters'
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
  const router = useRouter()
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
    if (!outlineId) {
      characters.value = []
      return
    }
    await loadCharacters()
  }

  const characters = ref<CharacterResponse[]>([])
  const loading = ref(false)

  const drawerOpen = ref(false)
  const createLoading = ref(false)
  const createForm = reactive<CreateCharacterForm>(emptyForm())
  /** 新建时是否同步一份到角色池 */
  const syncToPool = ref(false)

  // AI 生成角色（统一入口，支持从原著提取或自由生成）
  const generateDesc = ref('')
  const generateFromNovel = ref(false)
  const generateLoading = ref(false)

  async function generateFromDesc(): Promise<void> {
    const desc = generateDesc.value.trim()
    if (!desc) return
    if (!currentOutlineId.value) {
      toast.error('请先选择一个大纲，才能生成角色')
      return
    }
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

  const createTrigger = ref(0)

  function openCreate(): void {
    if (!currentOutlineId.value) {
      toast.error('请先在「创作」页为当前分类新建一个大纲，才能在其下添加角色')
      return
    }
    if (!drawerOpen.value) {
      Object.assign(createForm, emptyForm())
      syncToPool.value = false
    }
    drawerOpen.value = true
    createTrigger.value++
  }

  async function submitCreate(): Promise<void> {
    if (!createForm.name.trim() || !currentOutlineId.value) return
    createLoading.value = true
    try {
      const charData = {
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
      }
      const character = await createCharacter(projectId, currentOutlineId.value, charData)
      characters.value.push(character)
      modeCharCounts.value[currentOutline.value!.mode] = (modeCharCounts.value[currentOutline.value!.mode] ?? 0) + 1
      // 如勾选"同步到角色池"，同步一份
      if (syncToPool.value) {
        await createInPool(projectId, charData)
        modeCharCounts.value[currentOutline.value!.mode] = (modeCharCounts.value[currentOutline.value!.mode] ?? 0) + 1
        toast.success('角色添加成功，已在角色池保留留档')
      } else {
        toast.success('角色添加成功')
      }
      drawerOpen.value = false
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
      modeCharCounts.value[currentOutline.value!.mode] = Math.max(0, (modeCharCounts.value[currentOutline.value!.mode] ?? 1) - 1)
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
  const editTrigger = ref(0)

  function openEdit(c: CharacterResponse): void {
    const isRestoring = editDrawerOpen.value && editTarget.value?.id === c.id
    editTarget.value = c
    if (!isRestoring) {
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
    }
    editDrawerOpen.value = true
    editTrigger.value++
  }

  function resetEditForm(): void {
    if (editTarget.value) openEdit(editTarget.value)
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

  // ── 各 mode 角色总数（用于 tab badge） ─────────────────────
  const ALL_GEN_MODES = [
    'Original',
    'ContinueFromOriginal',
    'SideStoryFromOriginal',
    'ExpandOrRewrite',
  ] as const

  const modeCharCounts = ref<Record<string, number>>({
    Original: 0,
    ContinueFromOriginal: 0,
    SideStoryFromOriginal: 0,
    ExpandOrRewrite: 0,
  })

  async function loadModeCharCounts(): Promise<void> {
    if (outlines.value.length === 0) return
    const pairs = await Promise.all(
      outlines.value.map(async (o) => {
        const chars = await getCharacters(projectId, o.id).catch(() => [])
        return { mode: o.mode, count: chars.length }
      }),
    )
    const counts: Record<string, number> = {
      Original: 0,
      ContinueFromOriginal: 0,
      SideStoryFromOriginal: 0,
      ExpandOrRewrite: 0,
    }
    for (const { mode, count } of pairs) counts[mode] = (counts[mode] ?? 0) + count
    modeCharCounts.value = counts
  }

  onMounted(async () => {
    await loadOutlines()
    await Promise.all([loadCharacters(), loadModeCharCounts()])
  })

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
    createTrigger,
    createForm,
    createLoading,
    openCreate,
    submitCreate,
    syncToPool,
    deleteTarget,
    deleteLoading,
    openDelete: (c: CharacterResponse) => { deleteTarget.value = c },
    cancelDelete: () => { deleteTarget.value = null },
    confirmDelete,
    editDrawerOpen,
    editTrigger,
    editForm,
    editLoading,
    openEdit,
    resetEditForm,
    submitEdit,
    generateDesc,
    generateFromNovel,
    generateLoading,
    generateFromDesc,
    goToPool: () => router.push('/character-pool'),
    modeCharCounts,
  }
}
