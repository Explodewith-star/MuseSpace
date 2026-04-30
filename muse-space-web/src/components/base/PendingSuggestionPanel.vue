<script setup lang="ts">
/**
 * 菜单内嵌的"待处理建议（含一致性冲突）"折叠列表。
 *
 * 用法示例（在世界观菜单顶部 AgentLauncher 下方）：
 *   <PendingSuggestionPanel
 *     :project-id="projectId"
 *     :categories="['WorldRuleConsistency']"
 *     title="待处理世界观冲突"
 *   />
 *
 * 仅展示 Pending 状态的最近若干条，提供跳转到建议中心的链接。
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getSuggestions } from '@/api/suggestions'
import type { AgentSuggestionResponse } from '@/types/models'
import AppButton from '@/components/base/AppButton.vue'
import { CATEGORY_LABELS, CATEGORY_ICONS } from '@/views/projects/suggestions/utils'

interface Props {
  projectId: string
  /** 要展示的建议类目（一个或多个） */
  categories: string[]
  /** 标题 */
  title: string
  /** 最多展示条数，默认 5 */
  limit?: number
  /** 是否默认展开 */
  defaultOpen?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  limit: 5,
  defaultOpen: false,
})

const router = useRouter()
const open = ref(props.defaultOpen)
const items = ref<AgentSuggestionResponse[]>([])
const loading = ref(false)

const visibleItems = computed(() => items.value.slice(0, props.limit))

async function load() {
  loading.value = true
  try {
    const all = await Promise.all(
      props.categories.map((c) =>
        getSuggestions(props.projectId, { category: c, status: 'Pending' }),
      ),
    )
    items.value = all.flat().sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    )
  } finally {
    loading.value = false
  }
}

defineExpose({ refresh: load })

onMounted(load)

function gotoCenter(category?: string) {
  router.push({
    path: `/projects/${props.projectId}/suggestions`,
    query: category ? { category } : { category: props.categories[0] },
  })
}
</script>

<template>
  <div v-if="items.length > 0" class="pending-suggestion-panel" :class="{ open }">
    <button class="panel-head" type="button" @click="open = !open">
      <i class="i-lucide-alert-triangle" />
      <span class="head-title">{{ title }}</span>
      <span class="count">{{ items.length }}</span>
      <i class="i-lucide-chevron-down chevron" />
    </button>

    <div v-if="open" class="panel-body">
      <ul class="item-list">
        <li v-for="item in visibleItems" :key="item.id" class="item-row">
          <i :class="CATEGORY_ICONS[item.category] ?? 'i-lucide-file-text'" />
          <div class="item-text">
            <div class="item-title">{{ item.title }}</div>
            <div class="item-meta">
              {{ CATEGORY_LABELS[item.category] ?? item.category }}
              · {{ new Date(item.createdAt).toLocaleString() }}
            </div>
          </div>
          <AppButton variant="ghost" size="sm" @click="gotoCenter(item.category)">查看</AppButton>
        </li>
      </ul>
      <div v-if="items.length > limit" class="more-row">
        <AppButton variant="ghost" size="sm" @click="gotoCenter()">
          查看全部 {{ items.length }} 条 →
        </AppButton>
      </div>
    </div>
  </div>
</template>

<style scoped>
.pending-suggestion-panel {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background: var(--color-bg-elevated);
  overflow: hidden;
}

.panel-head {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 10px 14px;
  background: rgba(245, 158, 11, 0.06);
  color: rgb(180, 83, 9);
  font-size: 13px;
  font-weight: 500;
  border: 0;
  cursor: pointer;
  text-align: left;
}

.panel-head:hover {
  background: rgba(245, 158, 11, 0.12);
}

.head-title {
  flex: 1;
}

.count {
  background: rgba(220, 38, 38, 0.12);
  color: rgb(185, 28, 28);
  border-radius: 999px;
  padding: 2px 8px;
  font-size: 12px;
  font-weight: 600;
}

.chevron {
  transition: transform 0.2s;
}

.pending-suggestion-panel.open .chevron {
  transform: rotate(180deg);
}

.panel-body {
  padding: 8px 14px 12px;
}

.item-list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.item-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 0;
  border-top: 1px dashed var(--color-border);
  font-size: 13px;
}

.item-row:first-child {
  border-top: 0;
}

.item-text {
  flex: 1;
  min-width: 0;
}

.item-title {
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.item-meta {
  margin-top: 2px;
  font-size: 12px;
  color: var(--color-text-secondary);
}

.more-row {
  margin-top: 4px;
  text-align: right;
}
</style>
