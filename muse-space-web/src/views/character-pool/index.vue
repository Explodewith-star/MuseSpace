<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import AppLayout from '@/components/layout/AppLayout.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppSelect from '@/components/base/AppSelect.vue'
import { getGlobalCharacterPool, createInPool, deleteFromPool, importFromPool, generateCharacter, copyPoolCharactersToProject } from '@/api/characters'
import { getProjects } from '@/api/projects'
import { getStoryOutlines } from '@/api/outlines'
import { getNovels } from '@/api/novels'
import { useToast } from '@/composables/useToast'
import type { CharacterResponse, StoryProjectResponse, StoryOutlineResponse, NovelResponse, GenerationMode, CreateCharacterRequest } from '@/types/models'

const router = useRouter()
const toast = useToast()

// ── 数据 ──
const characters = ref<CharacterResponse[]>([])
const projects = ref<StoryProjectResponse[]>([])
const loading = ref(false)
const projectOutlines = ref<Map<string, StoryOutlineResponse[]>>(new Map())

// GenerationMode 枚举映射
const ALL_MODES: GenerationMode[] = [
  'Original',
  'ContinueFromOriginal',
  'SideStoryFromOriginal',
  'ExpandOrRewrite',
]
const MODE_LABELS: Record<GenerationMode, string> = {
  Original: '原创主线',
  ContinueFromOriginal: '原著续写',
  SideStoryFromOriginal: '支线番外',
  ExpandOrRewrite: '扩写/改写',
}
const OUTLINE_TYPES = ['全部', ...ALL_MODES.map((m) => MODE_LABELS[m])]
const TYPE_TO_MODE: Record<string, GenerationMode> = {
  原创主线: 'Original',
  原著续写: 'ContinueFromOriginal',
  支线番外: 'SideStoryFromOriginal',
  '扩写/改写': 'ExpandOrRewrite',
}
const activeType = ref('全部')

/** outlineId → mode 快速查表 */
const outlineMode = computed(() => {
  const map = new Map<string, GenerationMode>()
  for (const [, outlines] of projectOutlines.value) {
    for (const o of outlines) map.set(o.id, o.mode)
  }
  return map
})

async function loadData() {
  loading.value = true
  try {
    ;[characters.value, projects.value] = await Promise.all([
      getGlobalCharacterPool(),
      getProjects(),
    ])
    // 并行加载各项目大纲
    const entries = await Promise.all(
      projects.value.map(async (p) => {
        const outlines = await getStoryOutlines(p.id).catch(() => [] as StoryOutlineResponse[])
        return [p.id, outlines] as const
      }),
    )
    projectOutlines.value = new Map(entries)
  } catch {
    // http 层已 toast
  } finally {
    loading.value = false
  }
}
onMounted(loadData)

// ── 筛选 ──
const searchQuery = ref('')
const roleFilter = ref('')

const ROLES = ['主角', '配角', '反派', '龙套', '其他']
const ROLE_FILTER_OPTIONS = ROLES.map((r) => ({ value: r, label: r }))

/** 快速查表：projectId → 该项目拥有哪些 mode */
const projectModes = computed(() => {
  const map = new Map<string, Set<GenerationMode>>()
  for (const [pid, outlines] of projectOutlines.value) {
    const modes = new Set<GenerationMode>(outlines.map((o) => o.mode))
    map.set(pid, modes)
  }
  return map
})

const filteredChars = computed(() => {
  let list = characters.value

  // 按大类筛选
  // 全局池角色 storyOutlineId 始终为 null，因此回退到所属项目是否含该 mode 的大纲
  if (activeType.value !== '全部') {
    const targetMode = TYPE_TO_MODE[activeType.value]
    list = list.filter((c) => {
      if (c.storyOutlineId) {
        return outlineMode.value.get(c.storyOutlineId) === targetMode
      }
      return projectModes.value.get(c.storyProjectId)?.has(targetMode) ?? false
    })
  }

  // 按角色名/定位/标签搜索
  if (searchQuery.value.trim()) {
    const q = searchQuery.value.trim().toLowerCase()
    list = list.filter(
      (c) =>
        c.name.toLowerCase().includes(q) ||
        (c.role ?? '').toLowerCase().includes(q) ||
        (c.tags ?? '').toLowerCase().includes(q),
    )
  }

  // 按定位筛选（包含匹配，支持「配角/丈夫」等复合定位）
  if (roleFilter.value) {
    list = list.filter((c) => (c.role ?? '').includes(roleFilter.value))
  }

  return list
})

// 按项目分组展示
const groupedByProject = computed(() => {
  const map = new Map<string, { project: StoryProjectResponse; chars: CharacterResponse[] }>()
  for (const c of filteredChars.value) {
    if (!map.has(c.storyProjectId)) {
      const project = projects.value.find((p) => p.id === c.storyProjectId)
      if (!project) continue
      map.set(c.storyProjectId, { project, chars: [] })
    }
    map.get(c.storyProjectId)!.chars.push(c)
  }
  return [...map.values()]
})

// ── 多选 ──
const selectedIds = ref<Set<string>>(new Set())
const isSelectMode = computed(() => selectedIds.value.size > 0)

function toggleSelect(id: string) {
  if (selectedIds.value.has(id)) {
    selectedIds.value.delete(id)
  } else {
    selectedIds.value.add(id)
  }
  // trigger reactivity
  selectedIds.value = new Set(selectedIds.value)
}

function clearSelection() {
  selectedIds.value = new Set()
}

// ── 引用到大纲弹窗 ──
const showImportModal = ref(false)
const importTargetProjectId = ref('')
const importTargetOutlineId = ref('')
const importLoading = ref(false)

const importProjectOutlines = computed(
  () => projectOutlines.value.get(importTargetProjectId.value) ?? [],
)

watch(importTargetProjectId, () => {
  importTargetOutlineId.value = ''
})

function openImportModal() {
  importTargetProjectId.value = projects.value[0]?.id ?? ''
  importTargetOutlineId.value = ''
  showImportModal.value = true
}

async function confirmImport() {
  if (!importTargetOutlineId.value || !importTargetProjectId.value) return
  importLoading.value = true
  try {
    const charIds = [...selectedIds.value]

    // Step 1: 将全局池角色复制到目标项目的本地池（不同项目来源的角色需要此步骤）
    const projectPoolChars = await copyPoolCharactersToProject(importTargetProjectId.value, charIds)

    // Step 2: 将本地池的新副本引入到目标大纲
    const newPoolIds = projectPoolChars.map((c) => c.id)
    await importFromPool(importTargetProjectId.value, importTargetOutlineId.value, newPoolIds)

    const outlineName =
      importProjectOutlines.value.find((o) => o.id === importTargetOutlineId.value)?.name ?? ''
    showImportModal.value = false
    clearSelection()
    // 跳转到目标项目角色库，让用户直接看到引用结果
    router.push(`/projects/${importTargetProjectId.value}/characters`)
    toast.success(`已将 ${charIds.length} 个角色引用到「${outlineName}」`)
  } catch {
    // toast by http
  } finally {
    importLoading.value = false
  }
}

// ── 批量删除 ──
const showDeleteConfirm = ref(false)
const deleteLoading = ref(false)

async function confirmBatchDelete() {
  deleteLoading.value = true
  try {
    // 按项目分组删除
    const byProject = new Map<string, string[]>()
    for (const id of selectedIds.value) {
      const c = characters.value.find((x) => x.id === id)
      if (!c) continue
      if (!byProject.has(c.storyProjectId)) byProject.set(c.storyProjectId, [])
      byProject.get(c.storyProjectId)!.push(id)
    }
    await Promise.all(
      [...byProject.entries()].map(([projectId, ids]) => deleteFromPool(projectId, ids)),
    )
    const count = selectedIds.value.size
    characters.value = characters.value.filter((c) => !selectedIds.value.has(c.id))
    clearSelection()
    showDeleteConfirm.value = false
    toast.success(`已删除 ${count} 个角色`)
  } catch {
    // toast by http
  } finally {
    deleteLoading.value = false
  }
}

// ── 新建角色（池内直接新建） ──
const drawerOpen = ref(false)
const createTrigger = ref(0)
const createLoading = ref(false)
const createForm = ref<CreateCharacterRequest & { projectId: string }>({
  projectId: '',
  name: '',
  role: '',
  age: undefined,
  personalitySummary: '',
  motivation: '',
  speakingStyle: '',
  forbiddenBehaviors: '',
  currentState: '',
  tags: '',
})

// AI 生成状态
const aiDesc = ref('')
const aiFromNovel = ref(false)
const aiLoading = ref(false)
const createOutlineId = ref('')
const aiNovelId = ref('')
const projectNovels = ref<NovelResponse[]>([])

const projectOptions = computed(() =>
  projects.value.map((p) => ({ value: p.id, label: p.name })),
)

const createProjectOutlineOptions = computed(() =>
  (projectOutlines.value.get(createForm.value.projectId) ?? []).map((o) => ({
    value: o.id,
    label: `${MODE_LABELS[o.mode]} · ${o.name}`,
  })),
)

const novelOptions = computed(() =>
  projectNovels.value
    .filter((n) => n.status === 'Indexed')
    .map((n) => ({ value: n.id, label: n.title || n.fileName })),
)

watch(
  () => createForm.value.projectId,
  async (pid) => {
    createOutlineId.value = ''
    aiNovelId.value = ''
    projectNovels.value = []
    if (pid) {
      projectNovels.value = await getNovels(pid).catch(() => [])
    }
  },
)

watch(aiFromNovel, (v) => {
  if (!v) aiNovelId.value = ''
})

async function generateForPool() {
  const desc = aiDesc.value.trim()
  if (!desc || !createOutlineId.value) return
  aiLoading.value = true
  try {
    const result = await generateCharacter(
      createForm.value.projectId,
      createOutlineId.value,
      desc,
      aiFromNovel.value,
    )
    Object.assign(createForm.value, {
      name: result.name ?? '',
      role: result.role ?? '',
      age: result.age ?? undefined,
      personalitySummary: result.personalitySummary ?? '',
      motivation: result.motivation ?? '',
      speakingStyle: result.speakingStyle ?? '',
      forbiddenBehaviors: result.forbiddenBehaviors ?? '',
      currentState: result.currentState ?? '',
    })
    toast.success('AI 已生成角色信息，请检查并修改')
  } catch {
    // toast by http
  } finally {
    aiLoading.value = false
  }
}

const ROLE_OPTIONS = ['主角', '配角', '反派', '龙套', '其他'].map((r) => ({ value: r, label: r }))

function openCreate(projectId?: string) {
  createForm.value = {
    projectId: projectId ?? (projects.value[0]?.id ?? ''),
    name: '',
    role: '',
    age: undefined,
    personalitySummary: '',
    motivation: '',
    speakingStyle: '',
    forbiddenBehaviors: '',
    currentState: '',
    tags: '',
  }
  createOutlineId.value = ''
  aiDesc.value = ''
  aiFromNovel.value = false
  aiNovelId.value = ''
  projectNovels.value = []
  drawerOpen.value = true
  createTrigger.value++
}

async function submitCreate() {
  if (!createForm.value.name.trim()) return
  createLoading.value = true
  try {
    const newChar = await createInPool(createForm.value.projectId, {
      name: createForm.value.name,
      role: createForm.value.role || undefined,
      age: createForm.value.age,
      personalitySummary: createForm.value.personalitySummary || undefined,
      motivation: createForm.value.motivation || undefined,
      speakingStyle: createForm.value.speakingStyle || undefined,
      forbiddenBehaviors: createForm.value.forbiddenBehaviors || undefined,
      currentState: createForm.value.currentState || undefined,
      tags: createForm.value.tags || undefined,
    })
    characters.value.push(newChar)
    drawerOpen.value = false
    toast.success('角色已添加到角色池')
  } catch {
    // toast by http
  } finally {
    createLoading.value = false
  }
}

// ── 编辑抽屉（点击角色卡） ──
const editDrawerOpen = ref(false)
const editTrigger = ref(0)
const editChar = ref<CharacterResponse | null>(null)
const editForm = ref<Partial<CharacterResponse>>({})

function openEdit(char: CharacterResponse) {
  editChar.value = char
  editForm.value = { ...char }
  editDrawerOpen.value = true
  editTrigger.value++
}

// ── 工具方法 ──
function getProjectName(projectId: string) {
  return projects.value.find((p) => p.id === projectId)?.name ?? '未知项目'
}

function roleColor(role?: string) {
  const map: Record<string, string> = {
    主角: 'primary',
    配角: 'info',
    反派: 'danger',
    龙套: 'default',
    其他: 'default',
  }
  return (map[role ?? ''] ?? 'default') as any
}
</script>

<template>
  <AppLayout>
    <template #header-left>
      <div class="pool-header-left">
        <button class="back-btn" @click="router.push('/projects')">
          <i class="i-lucide-chevron-left" />
        </button>
        <span class="pool-title">
          <i class="i-lucide-users" />
          全局角色池
        </span>
      </div>
    </template>

    <div class="pool-page">
      <!-- 大类 Tab -->
      <div class="type-tabs">
        <button
          v-for="t in OUTLINE_TYPES"
          :key="t"
          class="type-tab"
          :class="{ active: activeType === t }"
          @click="activeType = t"
        >
          {{ t }}
        </button>
      </div>

      <!-- 工具栏 -->
      <div class="toolbar">
        <div class="toolbar-left">
          <div class="search-box">
            <i class="i-lucide-search search-icon" />
            <input
              v-model="searchQuery"
              class="search-input"
              type="text"
              placeholder="搜索角色名、定位、标签…"
            />
          </div>
          <AppSelect
            v-model="roleFilter"
            :options="ROLE_FILTER_OPTIONS"
            placeholder="全部定位"
          />
        </div>
        <div class="toolbar-right">
          <template v-if="isSelectMode">
            <span class="select-count">已选 {{ selectedIds.size }} 个</span>
            <AppButton variant="secondary" size="sm" @click="openImportModal">
              <i class="i-lucide-link-2" />
              引用到大纲
            </AppButton>
            <AppButton variant="danger" size="sm" @click="showDeleteConfirm = true">
              <i class="i-lucide-trash-2" />
              删除
            </AppButton>
            <AppButton variant="ghost" size="sm" @click="clearSelection">取消</AppButton>
          </template>
          <AppButton v-else @click="openCreate()">
            <i class="i-lucide-plus" />
            新建角色
          </AppButton>
        </div>
      </div>

      <!-- 加载骨架 -->
      <div v-if="loading" class="skeleton-area">
        <div v-for="i in 3" :key="i" class="skeleton-group">
          <div class="skeleton-project-title" />
          <div class="skeleton-cards">
            <div v-for="j in 3" :key="j" class="skeleton-char-card" />
          </div>
        </div>
      </div>

      <!-- 空状态 -->
      <AppEmpty
        v-else-if="!groupedByProject.length"
        icon="i-lucide-users"
        title="角色池为空"
        description="在项目中创建角色并同步到角色池，或直接在此新建"
      >
        <template #action>
          <AppButton :disabled="!projects.length" @click="openCreate()">
            <i class="i-lucide-plus" />
            新建角色
          </AppButton>
        </template>
      </AppEmpty>

      <!-- 分组角色卡片 -->
      <div v-else class="group-list">
        <div
          v-for="group in groupedByProject"
          :key="group.project.id"
          class="project-group"
        >
          <div class="project-group__header">
            <span class="project-group__name">{{ group.project.name }}</span>
            <span v-if="group.project.outlineType" class="project-group__type">{{ group.project.outlineType }}</span>
            <button class="add-to-group-btn" title="在此项目角色池新建角色" @click="openCreate(group.project.id)">
              <i class="i-lucide-plus" />
            </button>
          </div>
          <div class="char-grid">
            <div
              v-for="char in group.chars"
              :key="char.id"
              class="char-card"
              :class="{ selected: selectedIds.has(char.id) }"
              @click.exact="openEdit(char)"
            >
              <!-- 复选框区域（左上角点击触发多选） -->
              <div
                class="char-card__checkbox"
                @click.stop="toggleSelect(char.id)"
              >
                <div class="checkbox" :class="{ checked: selectedIds.has(char.id) }">
                  <i v-if="selectedIds.has(char.id)" class="i-lucide-check" />
                </div>
              </div>
              <div class="char-card__body">
                <div class="char-card__name">{{ char.name }}</div>
                <div class="char-card__meta">
                  <AppBadge v-if="char.role" :variant="roleColor(char.role)" size="sm">
                    {{ char.role }}
                  </AppBadge>
                  <span v-if="char.age" class="char-age">{{ char.age }}岁</span>
                </div>
                <p v-if="char.personalitySummary" class="char-card__summary">
                  {{ char.personalitySummary }}
                </p>
                <div v-if="char.tags" class="char-card__tags">
                  <span v-for="tag in char.tags.split(',')" :key="tag" class="tag">
                    {{ tag.trim() }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ── 新建角色抽屉 ── -->
    <AppDrawer
      v-model="drawerOpen"
      title="在角色池新建角色"
      :clear-handler="() => openCreate()"
      :open-trigger="createTrigger"
    >
      <div class="form-fields">
        <!-- AI 生成区块 -->
        <div class="ai-block">
          <div class="ai-block__title">
            <i class="i-lucide-wand-sparkles" />
            AI 生成角色
          </div>
          <!-- 来源切换 -->
          <div class="source-btns">
            <button
              type="button"
              :class="['source-btn', { active: !aiFromNovel }]"
              @click="aiFromNovel = false"
            >
              <i class="i-lucide-sparkles" /> 自由生成
            </button>
            <button
              type="button"
              :class="['source-btn', { active: aiFromNovel }]"
              @click="aiFromNovel = true"
            >
              <i class="i-lucide-book-open" /> 从原著提取
            </button>
          </div>
          <!-- 所属项目（AI 需要大纲上下文） -->
          <AppSelect
            v-model="createForm.projectId"
            :options="projectOptions"
            placeholder="选择项目"
            label="所属项目"
          />
          <!-- 选大纲（AI 调用必须） -->
          <AppSelect
            v-if="createForm.projectId"
            v-model="createOutlineId"
            :options="createProjectOutlineOptions"
            placeholder="选择大纲（AI 生成必须）"
            label="关联大纲"
          />
          <!-- 从原著提取：选择已索引原著 -->
          <AppSelect
            v-if="aiFromNovel && createForm.projectId"
            v-model="aiNovelId"
            :options="novelOptions"
            :placeholder="novelOptions.length ? '选择参考原著' : '暂无已索引原著'"
            :disabled="!novelOptions.length"
            label="参考原著"
          />
          <!-- 描述输入 + 生成按钮 -->
          <div class="ai-generate-row">
            <AppInput
              v-model="aiDesc"
              :placeholder="aiFromNovel ? '描述原著中角色特征，如：男主的挚友，性格沉稳' : '自由描述角色特征，如：冷静睿智的女谋士'"
              style="flex: 1"
            />
            <AppButton
              :loading="aiLoading"
              :disabled="!aiDesc.trim() || !createOutlineId"
              size="sm"
              @click="generateForPool"
            >
              生成
            </AppButton>
          </div>
          <p v-if="!createOutlineId && createForm.projectId" class="ai-hint">
            <i class="i-lucide-info" /> 请先选择大纲，AI 将以该大纲为世界观背景生成角色
          </p>
        </div>

        <div class="form-divider">或手动填写</div>

        <AppInput v-model="createForm.name" label="角色名 *" placeholder="角色名称" />
        <AppSelect
          v-model="createForm.role"
          :options="ROLE_OPTIONS"
          placeholder="选择定位"
          label="身份定位"
        />
        <AppInput v-model.number="createForm.age" label="年龄" type="number" placeholder="如：18" />
        <AppTextarea
          v-model="createForm.personalitySummary"
          label="性格概述"
          :rows="3"
          placeholder="角色的性格特点…"
        />
        <AppTextarea
          v-model="createForm.motivation"
          label="核心动机"
          :rows="2"
          placeholder="驱动角色行动的根本动机…"
        />
        <AppInput v-model="createForm.tags" label="标签" placeholder="多个标签用逗号分隔" />
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="drawerOpen = false">取消</AppButton>
        <AppButton
          :loading="createLoading"
          :disabled="!createForm.name.trim() || !createForm.projectId"
          @click="submitCreate"
        >
          添加到角色池
        </AppButton>
      </template>
    </AppDrawer>

    <!-- ── 编辑抽屉（只读展示，后续可扩展） ── -->
    <AppDrawer
      v-model="editDrawerOpen"
      :title="editChar?.name ?? '角色详情'"
      :open-trigger="editTrigger"
    >
      <div v-if="editChar" class="edit-view">
        <div class="edit-view__row">
          <span class="edit-view__label">所属项目</span>
          <span>{{ getProjectName(editChar.storyProjectId) }}</span>
        </div>
        <div v-if="editChar.role" class="edit-view__row">
          <span class="edit-view__label">定位</span>
          <AppBadge :variant="roleColor(editChar.role)">{{ editChar.role }}</AppBadge>
        </div>
        <div v-if="editChar.age" class="edit-view__row">
          <span class="edit-view__label">年龄</span>
          <span>{{ editChar.age }}岁</span>
        </div>
        <div v-if="editChar.personalitySummary" class="edit-view__block">
          <span class="edit-view__label">性格概述</span>
          <p>{{ editChar.personalitySummary }}</p>
        </div>
        <div v-if="editChar.motivation" class="edit-view__block">
          <span class="edit-view__label">核心动机</span>
          <p>{{ editChar.motivation }}</p>
        </div>
        <div v-if="editChar.speakingStyle" class="edit-view__block">
          <span class="edit-view__label">说话风格</span>
          <p>{{ editChar.speakingStyle }}</p>
        </div>
        <div v-if="editChar.forbiddenBehaviors" class="edit-view__block">
          <span class="edit-view__label">禁止行为</span>
          <p>{{ editChar.forbiddenBehaviors }}</p>
        </div>
        <div v-if="editChar.currentState" class="edit-view__block">
          <span class="edit-view__label">当前状态</span>
          <p>{{ editChar.currentState }}</p>
        </div>
        <div v-if="editChar.tags" class="edit-view__row">
          <span class="edit-view__label">标签</span>
          <div class="char-card__tags">
            <span v-for="tag in editChar.tags.split(',')" :key="tag" class="tag">{{ tag.trim() }}</span>
          </div>
        </div>
        <div v-if="editChar.sourcePoolCharacterId" class="edit-view__row source-info">
          <i class="i-lucide-link-2" />
          <span>此角色已被引用自其他大纲</span>
        </div>
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="editDrawerOpen = false">关闭</AppButton>
        <AppButton @click="toggleSelect(editChar!.id); editDrawerOpen = false">
          <i class="i-lucide-check-square" />
          选中此角色
        </AppButton>
      </template>
    </AppDrawer>

    <!-- ── 引用到大纲弹窗 ── -->
    <div v-if="showImportModal" class="modal-overlay" @click.self="showImportModal = false">
      <div class="modal">
        <div class="modal__header">
          <h3>引用到大纲</h3>
          <button class="modal__close" @click="showImportModal = false"><i class="i-lucide-x" /></button>
        </div>
        <p class="modal__desc">选择目标项目及大纲，将 {{ selectedIds.size }} 个角色引用过去（独立副本，不影响角色池）</p>

        <!-- Step 1: 选项目 -->
        <div class="import-section">
          <div class="import-section__label">
            <span class="step-badge">1</span> 目标项目
          </div>
          <div class="project-radio-list">
            <label
              v-for="p in projects"
              :key="p.id"
              class="project-radio-item"
              :class="{ active: importTargetProjectId === p.id }"
            >
              <input type="radio" :value="p.id" v-model="importTargetProjectId" />
              <span>{{ p.name }}</span>
            </label>
          </div>
        </div>

        <!-- Step 2: 选大纲 -->
        <div v-if="importTargetProjectId" class="import-section">
          <div class="import-section__label">
            <span class="step-badge">2</span> 目标大纲
          </div>
          <div v-if="importProjectOutlines.length === 0" class="import-empty">该项目暂无大纲</div>
          <div v-else class="outline-radio-list">
            <label
              v-for="o in importProjectOutlines"
              :key="o.id"
              class="outline-radio-item"
              :class="{ active: importTargetOutlineId === o.id }"
            >
              <input type="radio" :value="o.id" v-model="importTargetOutlineId" />
              <span class="outline-mode-tag">{{ MODE_LABELS[o.mode] }}</span>
              <span>{{ o.name }}</span>
            </label>
          </div>
        </div>

        <div class="modal__footer">
          <AppButton variant="secondary" @click="showImportModal = false">取消</AppButton>
          <AppButton
            :loading="importLoading"
            :disabled="!importTargetOutlineId"
            @click="confirmImport"
          >
            引用到此大纲
          </AppButton>
        </div>
      </div>
    </div>

    <!-- 批量删除确认 -->
    <AppConfirm
      :model-value="showDeleteConfirm"
      title="批量删除角色"
      :message="`确定从角色池删除选中的 ${selectedIds.size} 个角色吗？此操作不可恢复。`"
      variant="danger"
      :loading="deleteLoading"
      @confirm="confirmBatchDelete"
      @cancel="showDeleteConfirm = false"
      @update:model-value="showDeleteConfirm = $event"
    />
  </AppLayout>
</template>

<style scoped>
.pool-page {
  max-width: 1100px;
  margin: 0 auto;
  padding: 20px;
}

/* Header */
.pool-header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.back-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  border: none;
  background: transparent;
  color: var(--color-text-secondary);
  cursor: pointer;
  font-size: 18px;
  transition: background 0.15s;
}

.back-btn:hover {
  background: var(--color-bg-hover);
}

.pool-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
}

/* Type Tabs */
.type-tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 20px;
  border-bottom: 1px solid var(--color-border);
  padding-bottom: 0;
}

.type-tab {
  padding: 8px 18px;
  border: none;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 14px;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  margin-bottom: -1px;
  transition: all 0.15s;
}

.type-tab:hover {
  color: var(--color-primary);
}

.type-tab.active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
  font-weight: 500;
}

/* Toolbar */
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 24px;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 10px;
  color: var(--color-text-muted);
  font-size: 14px;
}

.search-input {
  width: 220px;
  padding: 7px 12px 7px 32px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: 13px;
  outline: none;
}

.search-input:focus {
  border-color: var(--color-primary);
}

/* Role filter AppSelect wrapper */
.app-select-field {
  min-width: 130px;
}

.select-count {
  font-size: 13px;
  color: var(--color-text-secondary);
}

/* Group */
.group-list {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.project-group__header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 14px;
}

.project-group__name {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.project-group__type {
  font-size: 12px;
  padding: 2px 8px;
  border-radius: 10px;
  background: var(--color-bg-hover);
  color: var(--color-text-muted);
}

.add-to-group-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: 1px dashed var(--color-border);
  border-radius: 6px;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  font-size: 14px;
  margin-left: 4px;
  transition: all 0.15s;
}

.add-to-group-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

/* Char Grid */
.char-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}

.char-card {
  position: relative;
  background: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 14px;
  cursor: pointer;
  transition: border-color 0.15s, box-shadow 0.15s;
}

.char-card:hover {
  border-color: var(--color-primary);
  box-shadow: 0 2px 8px rgba(108, 92, 231, 0.1);
}

.char-card.selected {
  border-color: var(--color-primary);
  background: rgba(108, 92, 231, 0.05);
}

.char-card__checkbox {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 1;
}

.checkbox {
  width: 18px;
  height: 18px;
  border: 1.5px solid var(--color-border);
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  background: var(--color-bg-base);
  transition: all 0.15s;
}

.checkbox.checked {
  border-color: var(--color-primary);
  background: var(--color-primary);
  color: #fff;
}

.char-card__body {
  padding-right: 24px;
}

.char-card__name {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 6px;
}

.char-card__meta {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 8px;
}

.char-age {
  font-size: 12px;
  color: var(--color-text-muted);
}

.char-card__summary {
  font-size: 12px;
  color: var(--color-text-secondary);
  line-height: 1.5;
  margin: 0 0 8px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.char-card__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.tag {
  font-size: 11px;
  padding: 2px 7px;
  background: var(--color-bg-hover);
  color: var(--color-text-muted);
  border-radius: 10px;
}

/* Skeleton */
.skeleton-area {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.skeleton-group {}

.skeleton-project-title {
  width: 160px;
  height: 16px;
  background: var(--color-bg-hover);
  border-radius: 4px;
  margin-bottom: 14px;
  animation: pulse 1.5s infinite;
}

.skeleton-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}

.skeleton-char-card {
  height: 100px;
  background: var(--color-bg-hover);
  border-radius: 10px;
  animation: pulse 1.5s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

/* Form */
.form-fields {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.form-select {
  padding: 8px 10px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-surface);
  color: var(--color-text-primary);
  font-size: 14px;
  outline: none;
}

.form-select:focus {
  border-color: var(--color-primary);
}

/* Edit View */
.edit-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 4px 0;
}

.edit-view__row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.edit-view__block {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.edit-view__label {
  font-size: 12px;
  color: var(--color-text-muted);
  font-weight: 500;
  min-width: 72px;
}

.edit-view__block p {
  font-size: 14px;
  color: var(--color-text-secondary);
  line-height: 1.6;
  margin: 0;
}

.source-info {
  color: var(--color-text-muted);
  font-size: 12px;
  gap: 6px;
}

/* Copy Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.4);
  z-index: 200;
  display: flex;
  align-items: center;
  justify-content: center;
}

.modal {
  background: var(--color-bg-surface);
  border-radius: 12px;
  padding: 24px;
  width: 400px;
  max-width: calc(100vw - 48px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
}

.modal__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.modal__header h3 {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.modal__close {
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
}

.modal__close:hover {
  background: var(--color-bg-hover);
}

.modal__desc {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin: 0 0 16px;
}

/* Import Modal */
.import-section {
  margin-bottom: 16px;
}

.import-section__label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
  margin-bottom: 8px;
}

.step-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  background: var(--color-primary);
  color: #fff;
  border-radius: 50%;
  font-size: 11px;
  font-weight: 600;
  flex-shrink: 0;
}

.project-radio-list,
.outline-radio-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-height: 200px;
  overflow-y: auto;
}

.project-radio-item,
.outline-radio-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  cursor: pointer;
  font-size: 14px;
  color: var(--color-text-primary);
  transition: border-color 0.15s, background 0.15s;
}

.project-radio-item:hover,
.outline-radio-item:hover {
  border-color: var(--color-primary);
}

.project-radio-item.active,
.outline-radio-item.active {
  border-color: var(--color-primary);
  background: rgba(108, 92, 231, 0.05);
}

.project-radio-item input,
.outline-radio-item input {
  accent-color: var(--color-primary);
}

.outline-mode-tag {
  font-size: 11px;
  padding: 1px 7px;
  border-radius: 8px;
  background: rgba(108, 92, 231, 0.12);
  color: var(--color-primary);
  white-space: nowrap;
}

.import-empty {
  font-size: 13px;
  color: var(--color-text-muted);
  padding: 12px;
  text-align: center;
}

/* AI Block in Drawer */
.ai-block {
  background: var(--color-bg-hover);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 14px 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.ai-block__title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-primary);
}

.source-btns {
  display: flex;
  gap: 6px;
}

.source-btn {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 5px 12px;
  border: 1px solid var(--color-border);
  border-radius: 20px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.15s;
}

.source-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.source-btn.active {
  border-color: var(--color-primary);
  background: rgba(108, 92, 231, 0.1);
  color: var(--color-primary);
  font-weight: 500;
}

.ai-generate-row {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.ai-hint {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

.form-divider {
  text-align: center;
  font-size: 12px;
  color: var(--color-text-muted);
  position: relative;
  margin: 4px 0;
}

.form-divider::before,
.form-divider::after {
  content: '';
  position: absolute;
  top: 50%;
  width: calc(50% - 40px);
  height: 1px;
  background: var(--color-border);
}

.form-divider::before { left: 0; }
.form-divider::after { right: 0; }

.modal__footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
