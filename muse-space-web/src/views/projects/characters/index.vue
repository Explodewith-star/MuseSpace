<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import PendingSuggestionPanel from '@/components/base/PendingSuggestionPanel.vue'
import { initCharactersState } from './hooks'

const route = useRoute()
const projectId = route.params.id as string

const {
  characters,
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
  editDrawerOpen,
  editForm,
  editLoading,
  openEdit,
  submitEdit,
  generateDesc,
  generateFromNovel,
  generateLoading,
  generateFromDesc,
} = initCharactersState()

// ── 搜索 & 视图切换 ──
const searchQuery = ref('')
const viewMode = ref<'card' | 'list'>('card')

const filteredCharacters = computed(() => {
  if (!searchQuery.value.trim()) return characters.value
  const q = searchQuery.value.trim().toLowerCase()
  return characters.value.filter(
    (c) =>
      c.name.toLowerCase().includes(q) ||
      (c.role ?? '').toLowerCase().includes(q) ||
      (c.tags ?? '').toLowerCase().includes(q) ||
      (c.personalitySummary ?? '').toLowerCase().includes(q),
  )
})

// ── 分区：原创 vs 原著 ──
const originalChars = computed(() => filteredCharacters.value.filter((c) => !c.sourceNovelId))
const importedChars = computed(() => filteredCharacters.value.filter((c) => !!c.sourceNovelId))

// ── 列表视图展开详情 ──
const expandedId = ref<string | null>(null)
function toggleExpand(id: string) {
  expandedId.value = expandedId.value === id ? null : id
}
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">角色库</h2>
      <div class="header-actions">
        <div class="search-box">
          <i class="i-lucide-search search-icon" />
          <input
            v-model="searchQuery"
            class="search-input"
            type="text"
            placeholder="搜索角色名、定位、标签…"
          />
        </div>
        <div class="view-toggle">
          <button
            class="view-toggle-btn"
            :class="{ active: viewMode === 'card' }"
            title="卡片视图"
            @click="viewMode = 'card'"
          >
            <i class="i-lucide-layout-grid" />
          </button>
          <button
            class="view-toggle-btn"
            :class="{ active: viewMode === 'list' }"
            title="列表视图"
            @click="viewMode = 'list'"
          >
            <i class="i-lucide-list" />
          </button>
        </div>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          添加角色
        </AppButton>
      </div>
    </div>

    <!-- 角色生成 Agent -->
    <div class="agent-launcher-block char-generate-agent">
      <div class="char-generate-header">
        <div class="char-generate-title">
          <i class="i-lucide-wand-sparkles" />
          角色生成 Agent
        </div>
        <p class="char-generate-desc">描述角色特征，AI 自动生成完整角色卡并填入添加表单。</p>
      </div>
      <div class="char-generate-source">
        <button
          type="button"
          :class="['source-btn', { active: !generateFromNovel }]"
          @click="generateFromNovel = false"
        >
          <i class="i-lucide-sparkles" /> 自由生成
        </button>
        <button
          type="button"
          :class="['source-btn', { active: generateFromNovel }]"
          @click="generateFromNovel = true"
        >
          <i class="i-lucide-book-open" /> 从原著提取
        </button>
      </div>
      <div class="char-generate-input">
        <input
          v-model="generateDesc"
          class="generate-input"
          type="text"
          :placeholder="generateFromNovel
            ? '描述要提取的角色，如「主角」「石泓」「女二号」'
            : '描述角色特征，如「冷酷的女杀手，内心渴望救赎」'"
        />
        <AppButton
          size="sm"
          :loading="generateLoading"
          :disabled="!generateDesc.trim()"
          @click="generateFromDesc"
        >
          <i class="i-lucide-wand-sparkles" />
          生成
        </AppButton>
      </div>
    </div>

    <PendingSuggestionPanel
      :project-id="projectId"
      :categories="['CharacterConsistency']"
      title="待处理角色冲突"
    />

    <!-- 骨架屏 -->
    <div v-if="loading" class="char-grid">
      <div v-for="i in 4" :key="i" class="char-skeleton">
        <AppSkeleton width="50%" height="18px" style="margin-bottom: 8px" />
        <AppSkeleton width="30%" height="13px" style="margin-bottom: 10px" />
        <AppSkeleton width="100%" height="13px" />
      </div>
    </div>

    <!-- 空状态 -->
    <AppEmpty
      v-else-if="!characters.length"
      icon="i-lucide-users"
      title="还没有角色"
      description="添加第一个角色，丰富你的故事世界"
    >
      <template #action>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          添加角色
        </AppButton>
      </template>
    </AppEmpty>

    <!-- ═══ 角色内容 ═══ -->
    <template v-else>
      <!-- 搜索无结果 -->
      <div v-if="filteredCharacters.length === 0" class="empty">
        无匹配角色
      </div>
      <template v-else>
        <!-- ── 原创角色分区 ── -->
        <div v-if="originalChars.length > 0" class="section">
          <div class="section-header">
            <i class="i-lucide-pen-tool section-icon" />
            <span class="section-title">原创角色</span>
            <span class="section-count">{{ originalChars.length }}</span>
          </div>

          <!-- 卡片视图 -->
          <div v-if="viewMode === 'card'" class="char-grid">
            <div v-for="char in originalChars" :key="char.id" class="char-card">
              <div class="char-card__header">
                <div>
                  <h3 class="char-name">{{ char.name }}</h3>
                  <p v-if="char.role" class="char-role">{{ char.role }}</p>
                </div>
                <div class="char-card__actions">
                  <AppBadge v-if="char.category" variant="primary" size="sm">{{ char.category }}</AppBadge>
                  <AppBadge v-if="char.age" variant="muted">{{ char.age }} 岁</AppBadge>
                  <button class="card-action-btn" title="编辑角色" @click="openEdit(char)">
                    <i class="i-lucide-pencil" />
                  </button>
                  <button class="card-delete-btn" title="删除角色" @click="openDelete(char)">
                    <i class="i-lucide-trash-2" />
                  </button>
                </div>
              </div>
              <p v-if="char.personalitySummary" class="char-summary">{{ char.personalitySummary }}</p>
              <div v-if="char.tags" class="char-tags">
                <AppBadge
                  v-for="tag in char.tags.split(',').filter(Boolean)"
                  :key="tag"
                  variant="primary"
                  size="sm"
                >
                  {{ tag.trim() }}
                </AppBadge>
              </div>
            </div>
          </div>

          <!-- 列表视图 -->
          <table v-else class="char-table">
            <thead>
              <tr>
                <th>名称</th>
                <th>定位</th>
                <th>年龄</th>
                <th>标签</th>
                <th class="col-actions">操作</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="char in originalChars" :key="char.id">
                <tr class="char-row" :class="{ 'char-row--expanded': expandedId === char.id }" @click="toggleExpand(char.id)">
                  <td class="cell-name">{{ char.name }}</td>
                  <td>{{ char.role ?? '—' }}</td>
                  <td>{{ char.age ?? '—' }}</td>
                  <td class="cell-tags">{{ char.tags ?? '—' }}</td>
                  <td class="col-actions" @click.stop>
                    <button class="card-action-btn always-visible" title="编辑角色" @click="openEdit(char)">
                      <i class="i-lucide-pencil" />
                    </button>
                    <button class="card-action-btn always-visible" title="删除角色" @click="openDelete(char)">
                      <i class="i-lucide-trash-2" />
                    </button>
                  </td>
                </tr>
                <tr v-if="expandedId === char.id" class="char-expand-row">
                  <td colspan="5">
                    <div class="expand-detail">
                      <div v-if="char.personalitySummary" class="expand-field"><strong>性格：</strong>{{ char.personalitySummary }}</div>
                      <div v-if="char.motivation" class="expand-field"><strong>动机：</strong>{{ char.motivation }}</div>
                      <div v-if="char.speakingStyle" class="expand-field"><strong>说话风格：</strong>{{ char.speakingStyle }}</div>
                      <div v-if="char.currentState" class="expand-field"><strong>当前状态：</strong>{{ char.currentState }}</div>
                    </div>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>

        <!-- ── 原著人物分区 ── -->
        <div v-if="importedChars.length > 0" class="section">
          <div class="section-header">
            <i class="i-lucide-book-open section-icon" />
            <span class="section-title">原著人物</span>
            <span class="section-count">{{ importedChars.length }}</span>
          </div>

          <!-- 卡片视图 -->
          <div v-if="viewMode === 'card'" class="char-grid">
            <div v-for="char in importedChars" :key="char.id" class="char-card char-card--imported">
              <div class="char-card__header">
                <div>
                  <h3 class="char-name">{{ char.name }}</h3>
                  <p v-if="char.role" class="char-role">{{ char.role }}</p>
                </div>
                <div class="char-card__actions">
                  <AppBadge v-if="char.category" variant="primary" size="sm">{{ char.category }}</AppBadge>
                  <AppBadge v-if="char.age" variant="muted">{{ char.age }} 岁</AppBadge>
                  <button class="card-action-btn" title="编辑角色" @click="openEdit(char)">
                    <i class="i-lucide-pencil" />
                  </button>
                  <button class="card-delete-btn" title="删除角色" @click="openDelete(char)">
                    <i class="i-lucide-trash-2" />
                  </button>
                </div>
              </div>
              <p v-if="char.personalitySummary" class="char-summary">{{ char.personalitySummary }}</p>
              <div v-if="char.tags" class="char-tags">
                <AppBadge
                  v-for="tag in char.tags.split(',').filter(Boolean)"
                  :key="tag"
                  variant="primary"
                  size="sm"
                >
                  {{ tag.trim() }}
                </AppBadge>
              </div>
            </div>
          </div>

          <!-- 列表视图 -->
          <table v-else class="char-table">
            <thead>
              <tr>
                <th>名称</th>
                <th>定位</th>
                <th>年龄</th>
                <th>标签</th>
                <th class="col-actions">操作</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="char in importedChars" :key="char.id">
                <tr class="char-row" :class="{ 'char-row--expanded': expandedId === char.id }" @click="toggleExpand(char.id)">
                  <td class="cell-name">{{ char.name }}</td>
                  <td>{{ char.role ?? '—' }}</td>
                  <td>{{ char.age ?? '—' }}</td>
                  <td class="cell-tags">{{ char.tags ?? '—' }}</td>
                  <td class="col-actions" @click.stop>
                    <button class="card-action-btn always-visible" title="编辑角色" @click="openEdit(char)">
                      <i class="i-lucide-pencil" />
                    </button>
                    <button class="card-action-btn always-visible" title="删除角色" @click="openDelete(char)">
                      <i class="i-lucide-trash-2" />
                    </button>
                  </td>
                </tr>
                <tr v-if="expandedId === char.id" class="char-expand-row">
                  <td colspan="5">
                    <div class="expand-detail">
                      <div v-if="char.personalitySummary" class="expand-field"><strong>性格：</strong>{{ char.personalitySummary }}</div>
                      <div v-if="char.motivation" class="expand-field"><strong>动机：</strong>{{ char.motivation }}</div>
                      <div v-if="char.speakingStyle" class="expand-field"><strong>说话风格：</strong>{{ char.speakingStyle }}</div>
                      <div v-if="char.currentState" class="expand-field"><strong>当前状态：</strong>{{ char.currentState }}</div>
                    </div>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
      </template>
    </template>

    <!-- 添加角色抽屉 -->
    <AppDrawer v-model="drawerOpen" title="添加角色" width="520px">
      <div class="form-fields">
        <!-- AI 角色生成（合并：从原著提取 + 自由生成） -->
        <div class="ai-generate-block">
          <div class="ai-block-title">
            <i class="i-lucide-wand-sparkles" />
            AI 生成角色
          </div>
          <div class="char-generate-source" style="margin-bottom: 6px">
            <button
              type="button"
              :class="['source-btn', { active: !generateFromNovel }]"
              @click="generateFromNovel = false"
            >
              <i class="i-lucide-sparkles" /> 自由生成
            </button>
            <button
              type="button"
              :class="['source-btn', { active: generateFromNovel }]"
              @click="generateFromNovel = true"
            >
              <i class="i-lucide-book-open" /> 从原著提取
            </button>
          </div>
          <div class="ai-generate-row">
            <AppInput
              v-model="generateDesc"
              class="ai-desc-input"
              :placeholder="generateFromNovel
                ? '描述要提取的角色，如「主角」「石泓」'
                : '描述角色特征，如「冷酷的女杀手，内心渴望救赎」'"
            />
            <AppButton
              size="sm"
              :loading="generateLoading"
              :disabled="!generateDesc.trim()"
              @click="generateFromDesc"
            >
              生成
            </AppButton>
          </div>
          <p class="ai-extract-hint">AI 将生成完整角色卡，自动填入下方表单，你可以修改后保存</p>
        </div>

        <div class="form-section-title">基本信息</div>
        <AppInput v-model="createForm.name" label="角色名称 *" placeholder="角色姓名" />
        <div class="form-row">
          <AppInput v-model="createForm.age" label="年龄" placeholder="如：28" />
          <AppInput v-model="createForm.role" label="身份定位" placeholder="如：主角、反派、导师" />
        </div>
        <!-- 角色分类 -->
        <div class="form-field">
          <label class="form-label">角色分类</label>
          <div class="category-options">
            <button
              v-for="cat in ['', '主角', '配角', '反派', '龙套', '其他']"
              :key="cat"
              type="button"
              :class="['category-btn', { active: createForm.category === cat }]"
              @click="createForm.category = cat"
            >
              {{ cat || '不选' }}
            </button>
          </div>
        </div>

        <div class="form-section-title">性格与动机</div>
        <AppTextarea
          v-model="createForm.personalitySummary"
          label="性格摘要"
          placeholder="用几句话描述角色的核心性格..."
          :rows="3"
        />
        <AppTextarea
          v-model="createForm.motivation"
          label="核心动机"
          placeholder="角色行动背后的驱动力..."
          :rows="2"
        />
        <AppTextarea
          v-model="createForm.speakingStyle"
          label="说话风格"
          placeholder="角色的口头禅、语气特点..."
          :rows="2"
        />

        <div class="form-section-title">约束与状态</div>
        <AppTextarea
          v-model="createForm.forbiddenBehaviors"
          label="禁止行为"
          placeholder="角色绝对不会做的事..."
          :rows="2"
        />
        <AppTextarea
          v-model="createForm.publicSecrets"
          label="公开秘密"
          placeholder="读者/部分角色知道的秘密..."
          :rows="2"
        />
        <AppTextarea
          v-model="createForm.privateSecrets"
          label="隐藏秘密"
          placeholder="只有读者知道的秘密..."
          :rows="2"
        />
        <AppInput
          v-model="createForm.currentState"
          label="当前状态"
          placeholder="如：受伤、被通缉"
        />
        <AppInput
          v-model="createForm.tags"
          label="标签"
          placeholder="多个标签用逗号分隔，如：武者,忠义"
        />
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="drawerOpen = false">取消</AppButton>
        <AppButton
          :loading="createLoading"
          :disabled="!createForm.name.trim()"
          @click="submitCreate"
        >
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 编辑角色抽屉 -->
    <AppDrawer v-model="editDrawerOpen" title="编辑角色" width="520px">
      <div class="form-fields">
        <div class="form-section-title">基本信息</div>
        <AppInput v-model="editForm.name" label="角色名称 *" placeholder="角色姓名" />
        <div class="form-row">
          <AppInput v-model="editForm.age" label="年龄" />
          <AppInput v-model="editForm.role" label="身份定位" />
        </div>
        <!-- 角色分类 -->
        <div class="form-field">
          <label class="form-label">角色分类</label>
          <div class="category-options">
            <button
              v-for="cat in ['', '主角', '配角', '反派', '龙套', '其他']"
              :key="cat"
              type="button"
              :class="['category-btn', { active: editForm.category === cat }]"
              @click="editForm.category = cat"
            >
              {{ cat || '不选' }}
            </button>
          </div>
        </div>
        <div class="form-section-title">性格与动机</div>
        <AppTextarea v-model="editForm.personalitySummary" label="性格摘要" :rows="3" />
        <AppTextarea v-model="editForm.motivation" label="核心动机" :rows="2" />
        <AppTextarea v-model="editForm.speakingStyle" label="说话风格" :rows="2" />
        <div class="form-section-title">约束与状态</div>
        <AppTextarea v-model="editForm.forbiddenBehaviors" label="禁止行为" :rows="2" />
        <AppInput v-model="editForm.currentState" label="当前状态" />
        <AppInput v-model="editForm.tags" label="标签" placeholder="多个标签用逗号分隔" />
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="editDrawerOpen = false">取消</AppButton>
        <AppButton :loading="editLoading" :disabled="!editForm.name.trim()" @click="submitEdit">
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 删除确认 -->
    <AppConfirm
      :model-value="!!deleteTarget"
      title="删除角色"
      :message="`确定删除角色「${deleteTarget?.name}」吗？`"
      variant="danger"
      confirm-text="删除"
      :loading="deleteLoading"
      @update:model-value="cancelDelete"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped>
.page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 8px;
  font-size: 14px;
  color: var(--color-text-muted, #999);
  pointer-events: none;
}

.search-input {
  padding: 5px 10px 5px 28px;
  border: 1px solid var(--color-border, #ddd);
  border-radius: 6px;
  font-size: 13px;
  width: 220px;
  outline: none;
  transition: border-color 0.15s;
  background: var(--color-bg, #fff);
}

.search-input:focus {
  border-color: var(--color-primary, #7c3aed);
}

.view-toggle {
  display: flex;
  border: 1px solid var(--color-border, #ddd);
  border-radius: 6px;
  overflow: hidden;
}

.view-toggle-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 30px;
  border: none;
  background: var(--color-bg, #fff);
  cursor: pointer;
  color: var(--color-text-muted, #999);
  font-size: 15px;
  transition: background-color 0.15s, color 0.15s;
}

.view-toggle-btn + .view-toggle-btn {
  border-left: 1px solid var(--color-border, #ddd);
}

.view-toggle-btn.active {
  background: var(--color-primary, #7c3aed);
  color: #fff;
}

.view-toggle-btn:not(.active):hover {
  background: var(--color-bg-muted, #f5f5f5);
}

.empty {
  text-align: center;
  color: var(--color-text-muted, #888);
  padding: 32px;
  font-size: 14px;
}

/* ── 分区 ── */
.section {
  margin-bottom: 24px;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--color-border, #eee);
}

.section-icon {
  font-size: 16px;
  color: var(--color-primary, #7c3aed);
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.section-count {
  font-size: 12px;
  color: var(--color-text-muted, #999);
  background: var(--color-bg-muted, #f5f5f5);
  border-radius: 999px;
  padding: 1px 8px;
}

/* ── 原著人物卡片边饰 ── */
.char-card--imported {
  border-left: 3px solid var(--color-accent, #f59e0b);
}

.agent-launcher-block {
  margin-bottom: 20px;
}

.page__title {
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.char-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 14px;
}

.char-skeleton {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 16px;
  height: 110px;
}

.char-card {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  transition: border-color 0.15s;
  overflow: hidden;
}

.char-card:hover {
  border-color: var(--color-primary);
}

.char-card:hover .card-delete-btn {
  opacity: 1;
}

.char-card__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
}

.char-card__actions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}

.char-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 2px;
}

.char-role {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

.char-summary {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.char-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  overflow: hidden;
  max-height: 52px; /* 最多两行 */
}

.card-delete-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 14px;
  opacity: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.card-action-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 14px;
  opacity: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.card-action-btn:hover {
  background-color: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
}

.char-card:hover .card-action-btn {
  opacity: 1;
}

.card-delete-btn:hover {
  background-color: color-mix(in srgb, var(--color-danger) 12%, transparent);
  color: var(--color-danger);
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.form-section-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  padding-top: 6px;
  border-top: 1px solid var(--color-border);
  margin-top: 4px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.ai-extract-block {
  background: var(--color-surface-raised, rgba(124, 58, 237, 0.06));
  border: 1px solid var(--color-accent-border, rgba(124, 58, 237, 0.2));
  border-radius: 8px;
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.ai-extract-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  color: var(--color-accent, #7c3aed);
}

.ai-extract-row {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.ai-extract-hint {
  font-size: 11px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.5;
}

/* ── 列表视图表格 ── */
.char-table {
  width: 100%;
  border-collapse: collapse;
  background: var(--color-bg-surface, #fff);
  border: 1px solid var(--color-border, #eee);
  border-radius: 8px;
  overflow: hidden;
}

.char-table th,
.char-table td {
  padding: 8px 12px;
  border-bottom: 1px solid var(--color-border, #eee);
  text-align: left;
  font-size: 13px;
}

.char-table th {
  background: var(--surface-2, #f9f9fb);
  font-weight: 600;
  font-size: 12px;
  color: var(--color-text-secondary, #666);
  white-space: nowrap;
}

.char-row {
  cursor: pointer;
  transition: background-color 0.1s;
}

.char-row:hover {
  background: var(--color-bg-muted, #f9f9fb);
}

.char-row--expanded {
  background: var(--color-bg-muted, #f5f5f5);
}

.cell-name {
  font-weight: 600;
}

.cell-tags {
  max-width: 200px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--color-text-muted, #888);
}

.col-actions {
  display: flex;
  gap: 4px;
  justify-content: flex-end;
}

.always-visible {
  opacity: 1 !important;
}

.char-expand-row td {
  padding: 0;
  border-bottom: 1px solid var(--color-border, #eee);
}

.expand-detail {
  padding: 10px 16px 14px;
  background: var(--color-bg-muted, #fafafa);
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.expand-field {
  font-size: 13px;
  color: var(--color-text-secondary, #555);
  line-height: 1.6;
}

.expand-field strong {
  color: var(--color-text-primary);
  margin-right: 4px;
}

/* Category selector */
.form-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.category-options {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
  margin-top: 2px;
}

.category-btn {
  padding: 4px 12px;
  border-radius: 16px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-surface);
  color: var(--color-text-secondary);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s;
}

.category-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.category-btn.active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  font-weight: 600;
}

/* AI generate block */
.ai-generate-block {
  border: 1px solid color-mix(in srgb, var(--color-primary) 25%, transparent);
  background: color-mix(in srgb, var(--color-primary) 5%, transparent);
  border-radius: 8px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.ai-generate-block .ai-block-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
  color: var(--color-primary);
}

.ai-generate-row {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.ai-generate-row .ai-desc-input {
  flex: 1;
}

/* 角色生成 Agent 区块 */
.char-generate-agent {
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 16px 20px;
  background: var(--color-bg-surface);
  margin-bottom: 16px;
}

.char-generate-header {
  margin-bottom: 12px;
}

.char-generate-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 4px;
}

.char-generate-desc {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin: 0;
}

.char-generate-source {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
}

.source-btn {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 5px 14px;
  border-radius: 16px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-surface);
  color: var(--color-text-secondary);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.15s;
}

.source-btn:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.source-btn.active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  font-weight: 600;
}

.char-generate-input {
  display: flex;
  gap: 8px;
  align-items: center;
}

.generate-input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  font-size: 13px;
  background: var(--color-bg-base);
  color: var(--color-text-primary);
  outline: none;
  transition: border-color 0.2s;
}

.generate-input:focus {
  border-color: var(--color-primary);
}

.generate-input::placeholder {
  color: var(--color-text-muted);
}
</style>
