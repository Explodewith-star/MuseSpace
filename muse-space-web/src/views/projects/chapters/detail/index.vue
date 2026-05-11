<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import AppButton from '@/components/base/AppButton.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import AppCard from '@/components/base/AppCard.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import ChapterEventsPanel from './ChapterEventsPanel.vue'
import { initChapterDetailState, CHAPTER_STATUS_LABELS, CHAPTER_STATUS_VARIANTS } from './hooks'

const router = useRouter()
const route = useRoute()

const draftExpanded = ref(false)
const finalExpanded = ref(false)

const {
  chapter,
  characters,
  outline,
  characterMap,
  consistencySuggestions,
  loading,
  saving,
  editingSection,
  metaForm,
  planForm,
  draftForm,
  finalForm,
  startEdit,
  cancelEdit,
  saveEdit,
  togglePlanCharacter,
  addPlanPoint,
  removePlanPoint,
  triggerAutoPlan,
  triggerGenerateDraft,
  autoPlanLoading,
  generateDraftLoading,
  triggerAdoptDraft,
  confirmAdoptDraftOverride,
  cancelAdoptDraft,
  adoptDraftLoading,
  adoptDraftConfirmVisible,
  adoptDraftConfirmInfo,
} = initChapterDetailState()

const MODE_LABELS: Record<string, string> = {
  Original: '原创主线',
  ContinueFromOriginal: '原著续写',
  SideStoryFromOriginal: '支线番外',
  ExpandOrRewrite: '扩写/改写',
}

const REVEAL_LEVEL_LABELS: Record<number, string> = {
  0: '日常',
  1: '伏笔',
  2: '直接异常',
  3: '正面冲突',
  4: '揭示/收束',
}

function goBack() {
  router.push(`/projects/${route.params.id}/chapters`)
}

interface ConsistencyItem {
  dimension?: string
  severity?: 'high' | 'medium' | 'low' | string
  excerpt?: string
  issue?: string
  suggestion?: string
  // 角色一致性使用的字段（兼容显示）
  characterName?: string
  conflictType?: string
  conflictSnippet?: string
  explanation?: string
}

function parseConsistency(json: string): ConsistencyItem {
  try {
    return JSON.parse(json) as ConsistencyItem
  } catch {
    return {}
  }
}

function severityVariant(s?: string): 'danger' | 'warning' | 'muted' {
  if (s === 'high') return 'danger'
  if (s === 'medium') return 'warning'
  return 'muted'
}
function severityLabel(s?: string): string {
  if (s === 'high') return '高'
  if (s === 'medium') return '中'
  if (s === 'low') return '低'
  return s ?? '—'
}
</script>

<template>
  <div class="chapter-detail">
    <!-- 顶部导航 -->
    <div class="detail-header">
      <button class="back-btn" @click="goBack">
        <i class="i-lucide-arrow-left" />
        返回章节列表
      </button>
    </div>

    <!-- 加载骨架 -->
    <div v-if="loading" class="skeleton-wrap">
      <AppSkeleton width="30%" height="24px" style="margin-bottom:12px" />
      <AppSkeleton width="100%" height="80px" style="margin-bottom:12px" />
      <AppSkeleton width="100%" height="200px" />
    </div>

    <template v-else-if="chapter">
      <!-- 基本信息卡片 -->
      <AppCard class="section-card">
        <div class="section-header">
          <div class="chapter-title-row">
            <span class="chapter-number">第 {{ chapter.number }} 章</span>
            <h1 class="chapter-title">{{ chapter.title || '（未命名）' }}</h1>
            <AppBadge :variant="(CHAPTER_STATUS_VARIANTS[chapter.status] as any)">
              {{ CHAPTER_STATUS_LABELS[chapter.status] }}
            </AppBadge>
          </div>
          <AppButton v-if="editingSection !== 'meta'" variant="ghost" size="sm" @click="startEdit('meta')">
            <i class="i-lucide-pencil" /> 编辑
          </AppButton>
        </div>

        <!-- 查看模式 -->
        <template v-if="editingSection !== 'meta'">
          <div v-if="chapter.goal" class="field-block">
            <span class="field-label">本章目标</span>
            <p class="field-value">{{ chapter.goal }}</p>
          </div>
          <div v-if="chapter.summary" class="field-block">
            <span class="field-label">章节概要</span>
            <p class="field-value">{{ chapter.summary }}</p>
          </div>
          <div v-if="!chapter.goal && !chapter.summary" class="empty-hint">
            还没有设置目标和概要，点击编辑添加
          </div>
        </template>

        <!-- 编辑模式 -->
        <div v-else class="edit-form">
          <AppInput v-model="metaForm.title" label="章节标题" placeholder="第X章标题" />
          <AppTextarea v-model="metaForm.goal" label="本章目标" placeholder="这一章需要完成什么叙事任务？" :rows="2" />
          <AppTextarea v-model="metaForm.summary" label="章节概要" placeholder="用几句话概括本章内容..." :rows="3" />
          <div class="field-block">
            <span class="field-label">章节状态</span>
            <div class="status-options">
              <button
                v-for="(label, val) in CHAPTER_STATUS_LABELS"
                :key="val"
                :class="['status-option', { 'status-option--active': metaForm.status === Number(val) }]"
                @click="metaForm.status = Number(val)"
              >
                {{ label }}
              </button>
            </div>
          </div>
          <div class="edit-actions">
            <AppButton variant="secondary" size="sm" @click="cancelEdit">取消</AppButton>
            <AppButton size="sm" :loading="saving" @click="saveEdit">保存</AppButton>
          </div>
        </div>
      </AppCard>

      <!-- 章节计划卡片 -->
      <AppCard class="section-card">
        <div class="section-header">
          <h2 class="section-title">
            <i class="i-lucide-list-checks" /> 章节计划
          </h2>
          <div class="header-actions">
            <AppButton
              v-if="editingSection !== 'plan'"
              variant="ghost"
              size="sm"
              :loading="autoPlanLoading"
              @click="triggerAutoPlan"
            >
              <i class="i-lucide-sparkles" /> 自动填充计划
            </AppButton>
            <AppButton
              v-if="editingSection !== 'plan'"
              variant="ghost"
              size="sm"
              @click="startEdit('plan')"
            >
              <i class="i-lucide-pencil" /> 编辑
            </AppButton>
          </div>
        </div>

        <template v-if="editingSection !== 'plan'">
          <div v-if="chapter.conflict" class="field-block">
            <span class="field-label">核心冲突</span>
            <p class="field-value">{{ chapter.conflict }}</p>
          </div>
          <div v-if="chapter.emotionCurve" class="field-block">
            <span class="field-label">情感曲线</span>
            <p class="field-value">{{ chapter.emotionCurve }}</p>
          </div>
          <div v-if="chapter.keyCharacterIds && chapter.keyCharacterIds.length" class="field-block">
            <span class="field-label">关键角色</span>
            <div class="tag-list">
              <span v-for="id in chapter.keyCharacterIds" :key="id" class="tag">
                {{ characterMap[id]?.name ?? id.slice(0, 8) }}
              </span>
            </div>
          </div>
          <div v-if="chapter.mustIncludePoints && chapter.mustIncludePoints.length" class="field-block">
            <span class="field-label">必中要点</span>
            <ul class="point-list">
              <li v-for="(p, i) in chapter.mustIncludePoints" :key="i">{{ p }}</li>
            </ul>
          </div>
          <div class="field-block">
            <span class="field-label">揭示等级</span>
            <p class="field-value">
              {{ chapter.allowedRevealLevel == null ? '自动推断' : REVEAL_LEVEL_LABELS[chapter.allowedRevealLevel] }}
            </p>
          </div>
          <div
            v-if="!chapter.conflict && !chapter.emotionCurve
              && (!chapter.keyCharacterIds || chapter.keyCharacterIds.length === 0)
              && (!chapter.mustIncludePoints || chapter.mustIncludePoints.length === 0)"
            class="empty-hint plan-empty-hint"
          >
            <i class="i-lucide-lightbulb" />
            <span>
              还没有章节计划。点击「<strong>自动填充计划</strong>」让 AI 根据章节大纲自动产出冲突、情感曲线、关键角色和必中要点；也可点击「<strong>编辑</strong>」手动填写。
              <br />
              <em>建议先填写计划再生成草稿，质量会更好。</em>
            </span>
          </div>
        </template>

        <div v-else class="edit-form">
          <AppTextarea
            v-model="planForm.conflict"
            label="核心冲突"
            placeholder="本章对立面与张力来源..."
            :rows="2"
          />
          <AppTextarea
            v-model="planForm.emotionCurve"
            label="情感曲线"
            placeholder="例：平静→好奇→惊愕→愤怒→决断"
            :rows="2"
          />
          <div class="field-block">
            <span class="field-label">关键角色（可多选）</span>
            <div v-if="characters.length === 0" class="empty-hint">项目暂无角色</div>
            <div v-else class="tag-list">
              <button
                v-for="c in characters"
                :key="c.id"
                type="button"
                :class="['tag-toggle', { active: planForm.keyCharacterIds.includes(c.id) }]"
                @click="togglePlanCharacter(c.id)"
              >
                {{ c.name }}
              </button>
            </div>
          </div>
          <div class="field-block">
            <span class="field-label">必中要点</span>
            <ul class="point-edit-list">
              <li v-for="(p, i) in planForm.mustIncludePoints" :key="i">
                <span class="point-text">{{ p }}</span>
                <button class="point-remove" @click="removePlanPoint(i)">
                  <i class="i-lucide-x" />
                </button>
              </li>
            </ul>
            <div class="point-add">
              <AppInput
                v-model="planForm.pointInput"
                placeholder="输入一条要点后回车添加"
                @keydown.enter.prevent="addPlanPoint"
              />
              <AppButton size="sm" variant="secondary" @click="addPlanPoint">添加</AppButton>
            </div>
          </div>
          <div class="field-block">
            <span class="field-label">揭示等级</span>
            <select v-model.number="planForm.allowedRevealLevel" class="reveal-select">
              <option :value="undefined">自动推断</option>
              <option :value="0">日常</option>
              <option :value="1">伏笔</option>
              <option :value="2">直接异常</option>
              <option :value="3">正面冲突</option>
              <option :value="4">揭示/收束</option>
            </select>
          </div>
          <div class="edit-actions">
            <AppButton variant="secondary" size="sm" @click="cancelEdit">取消</AppButton>
            <AppButton size="sm" :loading="saving" @click="saveEdit">保存</AppButton>
          </div>
        </div>
      </AppCard>

      <!-- 草稿文本卡片 -->
      <AppCard class="section-card">
        <div class="section-header">
          <h2 class="section-title">
            <i class="i-lucide-file-text" /> 草稿
          </h2>
          <div class="header-actions">
            <AppButton
              v-if="editingSection !== 'draft'"
              variant="ghost"
              size="sm"
              :loading="generateDraftLoading"
              @click="triggerGenerateDraft"
            >
              <i class="i-lucide-wand-2" /> 生成本章草稿
            </AppButton>
            <AppButton
              v-if="editingSection !== 'draft' && chapter.draftText"
              variant="ghost"
              size="sm"
              :loading="adoptDraftLoading"
              @click="triggerAdoptDraft"
            >
              <i class="i-lucide-check-check" /> 采用为定稿
            </AppButton>
            <AppButton
              v-if="editingSection !== 'draft'"
              variant="ghost"
              size="sm"
              @click="startEdit('draft')"
            >
              <i class="i-lucide-pencil" /> 编辑
            </AppButton>
          </div>
        </div>

        <div v-if="editingSection !== 'draft'" class="mode-panel">
          <div class="mode-panel__head">
            <span class="field-label">所属大纲</span>
            <AppBadge v-if="outline" size="sm" variant="default">
              {{ MODE_LABELS[outline.mode] }}
            </AppBadge>
          </div>
          <div class="mode-panel__summary">
            <strong>{{ outline?.name ?? '默认大纲' }}</strong>
            <span v-if="outline?.outlineSummary">{{ outline.outlineSummary }}</span>
            <span v-else-if="outline?.branchTopic">{{ outline.branchTopic }}</span>
          </div>
        </div>

        <!-- 当前章节参考片段：临时关闭排查问题
        <div v-if="editingSection !== 'draft'" class="reference-panel">
          <div class="reference-panel__head">
            <span class="field-label">当前章节参考片段（可选）</span>
            <span class="reference-panel__hint">只影响本次生成，不写入长期设定</span>
          </div>
          <div class="reference-panel__controls">
            <AppSelect
              v-model="referenceForm.focus"
              :options="REFERENCE_FOCUS_OPTIONS"
              label="参考方向"
              :searchable="false"
            />
            <AppSelect
              v-model="referenceForm.strength"
              :options="REFERENCE_STRENGTH_OPTIONS"
              label="参考强度"
              :searchable="false"
            />
          </div>
          <AppTextarea
            v-model="referenceForm.text"
            placeholder="粘贴一段希望本章参考的片段。系统只参考你选择的方向，例如情绪、对话、节奏或人物互动，不会要求 AI 复述或照搬。"
            :rows="5"
          />
        </div>
        -->

        <template v-if="editingSection !== 'draft'">
          <div v-if="chapter.draftText" :class="['text-content', { 'text-content--collapsed': !draftExpanded }]">{{ chapter.draftText }}</div>
          <button v-if="chapter.draftText" class="expand-toggle" @click="draftExpanded = !draftExpanded">
            <i :class="draftExpanded ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'" />
            {{ draftExpanded ? '收起' : '展开全文' }}
          </button>
          <div v-else-if="chapter.conflict || chapter.emotionCurve" class="empty-hint draft-ready-hint">
            <i class="i-lucide-wand-2" />
            <span>章节计划已就绪，点击「<strong>生成本章草稿</strong>」让 AI 写出完整草稿，完成后将自动进行文风一致性审查。</span>
          </div>
          <div v-else class="empty-hint">
            暂无草稿内容。建议先填充章节计划，再点击「生成本章草稿」。
          </div>
        </template>

        <div v-else class="edit-form">
          <AppTextarea v-model="draftForm.draftText" label="" placeholder="在这里编写草稿..." :rows="16" />
          <div class="edit-actions">
            <AppButton variant="secondary" size="sm" @click="cancelEdit">取消</AppButton>
            <AppButton size="sm" :loading="saving" @click="saveEdit">保存</AppButton>
          </div>
        </div>
      </AppCard>

      <!-- 一致性审查结果卡片 -->
      <AppCard v-if="consistencySuggestions.length" class="section-card">
        <div class="section-header">
          <h2 class="section-title">
            <i class="i-lucide-shield-alert" /> 一致性审查结果
            <span class="count-badge">{{ consistencySuggestions.length }}</span>
          </h2>
        </div>
        <div class="consistency-list">
          <div
            v-for="s in consistencySuggestions"
            :key="s.id"
            class="consistency-item"
          >
            <div class="consistency-head">
              <span class="consistency-title">{{ s.title }}</span>
              <AppBadge
                v-if="parseConsistency(s.contentJson).severity"
                size="sm"
                :variant="(severityVariant(parseConsistency(s.contentJson).severity) as any)"
              >
                {{ severityLabel(parseConsistency(s.contentJson).severity) }}
              </AppBadge>
              <AppBadge size="sm" variant="muted">{{ s.status }}</AppBadge>
            </div>
            <div
              v-if="parseConsistency(s.contentJson).excerpt || parseConsistency(s.contentJson).conflictSnippet"
              class="consistency-excerpt"
            >
              "{{ parseConsistency(s.contentJson).excerpt ?? parseConsistency(s.contentJson).conflictSnippet }}"
            </div>
            <div
              v-if="parseConsistency(s.contentJson).issue || parseConsistency(s.contentJson).explanation"
              class="consistency-issue"
            >
              <span class="field-label">问题</span>
              <p class="field-value">
                {{ parseConsistency(s.contentJson).issue ?? parseConsistency(s.contentJson).explanation }}
              </p>
            </div>
            <div v-if="parseConsistency(s.contentJson).suggestion" class="consistency-suggestion">
              <span class="field-label">建议</span>
              <p class="field-value">{{ parseConsistency(s.contentJson).suggestion }}</p>
            </div>
          </div>
        </div>
      </AppCard>

      <!-- 定稿文本卡片 -->
      <AppCard class="section-card">
        <div class="section-header">
          <h2 class="section-title">
            <i class="i-lucide-book-open" /> 定稿
          </h2>
          <AppButton v-if="editingSection !== 'final'" variant="ghost" size="sm" @click="startEdit('final')">
            <i class="i-lucide-pencil" /> 编辑
          </AppButton>
        </div>

        <template v-if="editingSection !== 'final'">
          <div v-if="chapter.finalText" :class="['text-content', 'final-text', { 'text-content--collapsed': !finalExpanded }]">{{ chapter.finalText }}</div>
          <button v-if="chapter.finalText" class="expand-toggle" @click="finalExpanded = !finalExpanded">
            <i :class="finalExpanded ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'" />
            {{ finalExpanded ? '收起' : '展开全文' }}
          </button>
          <div v-else class="empty-hint">暂无定稿内容</div>
        </template>

        <div v-else class="edit-form">
          <AppTextarea v-model="finalForm.finalText" label="" placeholder="在这里编写定稿..." :rows="16" />
          <div class="edit-actions">
            <AppButton variant="secondary" size="sm" @click="cancelEdit">取消</AppButton>
            <AppButton size="sm" :loading="saving" @click="saveEdit">保存</AppButton>
          </div>
        </div>
      </AppCard>
    </template>

    <!-- Module D 正典事实层 / 章节事件面板 -->
    <ChapterEventsPanel
      v-if="chapter"
      :project-id="(route.params.id as string)"
      :chapter-id="chapter.id"
    />

    <!-- 采用草稿为定稿：覆盖二次确认 -->
    <AppConfirm
      :model-value="adoptDraftConfirmVisible"
      title="覆盖现有定稿？"
      :message="adoptDraftConfirmInfo
        ? `当前定稿已有 ${adoptDraftConfirmInfo.previousFinalLength} 字，将被草稿（${adoptDraftConfirmInfo.draftLength} 字）覆盖。此操作不会清空草稿，但会替换定稿全文。`
        : '当前定稿已有内容，将被草稿覆盖。'"
      confirm-text="确认覆盖"
      variant="danger"
      :loading="adoptDraftLoading"
      @update:model-value="(v: boolean) => { if (!v) cancelAdoptDraft() }"
      @confirm="confirmAdoptDraftOverride"
    />
  </div>
</template>

<style scoped>
.chapter-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.detail-header {
  margin-bottom: 4px;
}

.back-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 0;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 13px;
  color: var(--color-text-muted);
  transition: color 0.15s;
}

.back-btn:hover {
  color: var(--color-primary);
}

.skeleton-wrap {
  padding: 4px 0;
}

.section-card {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.section-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.chapter-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.chapter-number {
  font-size: 13px;
  color: var(--color-text-muted);
  font-weight: 500;
}

.chapter-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.field-block {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.field-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.field-value {
  font-size: 14px;
  color: var(--color-text-primary);
  line-height: 1.6;
  margin: 0;
}

.empty-hint {
  font-size: 13px;
  color: var(--color-text-muted);
  font-style: italic;
}

.plan-empty-hint,
.draft-ready-hint {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  font-style: normal;
  line-height: 1.6;
}

.plan-empty-hint i,
.draft-ready-hint i {
  font-size: 15px;
  flex-shrink: 0;
  margin-top: 1px;
}

.plan-empty-hint i {
  color: var(--color-warning, #f59e0b);
}

.draft-ready-hint {
  color: var(--color-text-secondary);
  background: color-mix(in srgb, var(--color-accent) 6%, transparent);
  border: 1px solid color-mix(in srgb, var(--color-accent) 20%, transparent);
  border-radius: 8px;
  padding: 10px 14px;
  font-style: normal;
}

.draft-ready-hint i {
  color: var(--color-accent);
}

.reference-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-primary) 4%, transparent);
}

.reference-panel__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  flex-wrap: wrap;
}

.reference-panel__hint {
  font-size: 12px;
  color: var(--color-text-muted);
}

.reference-panel__controls {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.plan-empty-hint em,
.draft-ready-hint em {
  color: var(--color-text-muted);
  font-style: italic;
  font-size: 12px;
}

.text-content {
  font-size: 14px;
  line-height: 1.85;
  color: var(--color-text-primary);
  white-space: pre-wrap;
}

.text-content--collapsed {
  max-height: calc(1.85em * 5);
  overflow: hidden;
  position: relative;
}

.text-content--collapsed::after {
  content: '';
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 2em;
  background: linear-gradient(transparent, var(--color-bg-surface));
  pointer-events: none;
}

.expand-toggle {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 0;
  border: none;
  background: none;
  cursor: pointer;
  font-size: 12px;
  color: var(--color-primary);
  font-weight: 500;
}

.expand-toggle:hover {
  text-decoration: underline;
}

.final-text {
  font-family: 'Georgia', 'Noto Serif SC', serif;
}

.edit-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.edit-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.status-options {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.status-option {
  padding: 4px 14px;
  border-radius: 99px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-elevated);
  cursor: pointer;
  font-size: 12px;
  color: var(--color-text-muted);
  transition: all 0.15s;
}

.status-option--active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 15%, transparent);
  color: var(--color-primary);
  font-weight: 500;
}

.header-actions {
  display: flex;
  gap: 8px;
  align-items: center;
}

.tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.tag {
  padding: 3px 10px;
  border-radius: 99px;
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  font-size: 12px;
  font-weight: 500;
}

.tag-toggle {
  padding: 4px 12px;
  border-radius: 99px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-elevated);
  cursor: pointer;
  font-size: 12px;
  color: var(--color-text-muted);
  transition: all 0.15s;
}

.tag-toggle.active {
  border-color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 15%, transparent);
  color: var(--color-primary);
  font-weight: 500;
}

.point-list {
  margin: 0;
  padding-left: 20px;
  font-size: 14px;
  line-height: 1.7;
  color: var(--color-text-primary);
}

.point-edit-list {
  list-style: none;
  margin: 0 0 8px;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.point-edit-list li {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  background: var(--color-bg-elevated);
  border-radius: 6px;
}

.point-text {
  flex: 1;
  font-size: 13px;
  color: var(--color-text-primary);
}

.point-remove {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted);
  display: flex;
  align-items: center;
}

.point-remove:hover {
  color: var(--color-danger, #e54848);
}

.point-add {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.point-add :deep(.app-input) {
  flex: 1;
}

.count-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 22px;
  padding: 0 6px;
  border-radius: 11px;
  background: color-mix(in srgb, var(--color-primary) 18%, transparent);
  color: var(--color-primary);
  font-size: 12px;
  font-weight: 600;
}

.consistency-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.consistency-item {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  background: var(--color-bg-elevated);
}

.consistency-head {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.consistency-title {
  flex: 1;
  font-weight: 600;
  font-size: 14px;
  color: var(--color-text-primary);
}

.consistency-excerpt {
  font-size: 13px;
  color: var(--color-text-secondary);
  font-style: italic;
  background: color-mix(in srgb, var(--color-warning, #f0a020) 10%, transparent);
  padding: 6px 10px;
  border-radius: 4px;
  border-left: 3px solid var(--color-warning, #f0a020);
}

.consistency-issue,
.consistency-suggestion {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

/* ── Module E：创作模式面板 ──────────────────────────────── */
.mode-panel {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: color-mix(in srgb, var(--color-accent) 4%, transparent);
}

.mode-panel__head {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mode-panel__summary {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.5;
}

.mode-panel__summary strong {
  color: var(--color-text-primary);
}

.mode-panel__row {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.mode-panel__field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 140px;
}

.mode-panel__field--wide {
  flex: 1;
  min-width: 220px;
}

.mode-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.4px;
}

.mode-select {
  height: 32px;
  padding: 0 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
  font-size: 13px;
  cursor: pointer;
  outline: none;
  transition: border-color 0.15s;
}

.mode-select:focus {
  border-color: var(--color-primary);
}

.mode-input {
  height: 32px;
  padding: 0 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
  font-size: 13px;
  outline: none;
  transition: border-color 0.15s;
  width: 100%;
  box-sizing: border-box;
}

.mode-input:focus {
  border-color: var(--color-primary);
}

.mode-input--sm {
  width: 90px;
}

.reveal-select {
  height: 32px;
  max-width: 180px;
  padding: 0 8px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-bg-elevated);
  color: var(--color-text-primary);
  font-size: 13px;
  outline: none;
}
</style>
