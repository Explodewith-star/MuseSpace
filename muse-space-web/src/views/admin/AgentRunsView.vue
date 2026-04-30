<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  getAdminAgentRuns,
  getAdminAgentRunStats,
  getAdminAgentRunDetail,
  type AgentRunListItem,
  type AgentRunDetail,
  type AgentRunStats,
  type AgentRunStatus,
} from '@/api/admin'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppModal from '@/components/base/AppModal.vue'

const items = ref<AgentRunListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 50
const loading = ref(false)

const filterAgentName = ref('')
const filterStatus = ref<'' | AgentRunStatus>('')

const stats = ref<AgentRunStats | null>(null)
const statsDays = ref(7)

// ── 详情弹窗 ────────────────────────────────────────────────
const detailOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<AgentRunDetail | null>(null)
const activeTab = ref<'input' | 'output'>('input')

async function openDetail(id: string) {
  detailOpen.value = true
  detailLoading.value = true
  detail.value = null
  activeTab.value = 'input'
  try {
    detail.value = await getAdminAgentRunDetail(id)
  } finally {
    detailLoading.value = false
  }
}

async function copyText(text?: string | null) {
  if (!text) return
  try {
    await navigator.clipboard.writeText(text)
  } catch {
    /* ignore clipboard errors */
  }
}

async function fetchList() {
  loading.value = true
  try {
    const resp = await getAdminAgentRuns({
      agentName: filterAgentName.value || undefined,
      status: filterStatus.value || undefined,
      page: page.value,
      pageSize,
    })
    items.value = resp.items
    total.value = resp.total
  } finally {
    loading.value = false
  }
}

async function fetchStats() {
  stats.value = await getAdminAgentRunStats(statsDays.value)
}

function applyFilters() {
  page.value = 1
  fetchList()
}

function nextPage() {
  if (page.value * pageSize < total.value) {
    page.value++
    fetchList()
  }
}
function prevPage() {
  if (page.value > 1) {
    page.value--
    fetchList()
  }
}

function formatDuration(ms: number) {
  if (ms < 1000) return `${ms} ms`
  return `${(ms / 1000).toFixed(2)} s`
}

function formatTime(s: string) {
  return new Date(s).toLocaleString()
}

function statusBadge(s: AgentRunStatus): 'success' | 'danger' | 'accent' {
  if (s === 'Succeeded') return 'success'
  if (s === 'Failed') return 'danger'
  return 'accent'
}

onMounted(() => {
  fetchList()
  fetchStats()
})
</script>

<template>
  <div class="p-6 space-y-6">
    <h1 class="text-xl font-semibold">Agent 运行管理</h1>

    <!-- 全局统计 -->
    <div v-if="stats" class="grid grid-cols-2 md:grid-cols-5 gap-3">
      <div class="rounded border bg-white p-3">
        <div class="text-xs text-gray-500">最近 {{ statsDays }} 天总运行</div>
        <div class="text-lg font-semibold">{{ stats.totalRuns }}</div>
      </div>
      <div class="rounded border bg-white p-3">
        <div class="text-xs text-gray-500">成功</div>
        <div class="text-lg font-semibold text-green-600">{{ stats.succeededRuns }}</div>
      </div>
      <div class="rounded border bg-white p-3">
        <div class="text-xs text-gray-500">失败</div>
        <div class="text-lg font-semibold text-red-600">{{ stats.failedRuns }}</div>
      </div>
      <div class="rounded border bg-white p-3">
        <div class="text-xs text-gray-500">成功率</div>
        <div class="text-lg font-semibold">{{ (stats.successRate * 100).toFixed(1) }}%</div>
      </div>
      <div class="rounded border bg-white p-3">
        <div class="text-xs text-gray-500">平均耗时 / 平均 token</div>
        <div class="text-sm">{{ formatDuration(stats.avgDurationMs) }} / {{ stats.avgTotalTokens }}</div>
      </div>
    </div>

    <div v-if="stats && stats.byAgent.length" class="rounded border bg-white p-3">
      <div class="text-sm font-semibold mb-2">按 Agent 分组</div>
      <table class="w-full text-sm">
        <thead><tr class="text-left text-gray-500">
          <th class="py-1">AgentName</th><th>总数</th><th>成功</th><th>平均耗时</th>
        </tr></thead>
        <tbody>
          <tr v-for="b in stats.byAgent" :key="b.agentName" class="border-t">
            <td class="py-1">{{ b.agentName }}</td>
            <td>{{ b.total }}</td>
            <td>{{ b.succeeded }}</td>
            <td>{{ formatDuration(b.avgDurationMs) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 过滤 -->
    <div class="flex flex-wrap gap-2 items-end">
      <div>
        <div class="text-xs text-gray-500 mb-1">AgentName</div>
        <input v-model="filterAgentName" class="border px-2 py-1 rounded text-sm" placeholder="如 chapter-draft" />
      </div>
      <div>
        <div class="text-xs text-gray-500 mb-1">状态</div>
        <select v-model="filterStatus" class="border px-2 py-1 rounded text-sm">
          <option value="">全部</option>
          <option value="Running">Running</option>
          <option value="Succeeded">Succeeded</option>
          <option value="Failed">Failed</option>
        </select>
      </div>
      <AppButton @click="applyFilters">筛选</AppButton>
      <AppButton variant="secondary" @click="fetchStats">刷新统计</AppButton>
    </div>

    <!-- 列表 -->
    <div class="rounded border bg-white">
      <table class="w-full text-sm">
        <thead class="bg-gray-50">
          <tr class="text-left text-gray-600">
            <th class="px-3 py-2">AgentName</th>
            <th>状态</th>
            <th>耗时</th>
            <th>tokens (in/out)</th>
            <th>开始时间</th>
            <th>错误</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="r in items" :key="r.id" class="border-t hover:bg-gray-50 cursor-pointer" @click="openDetail(r.id)">
            <td class="px-3 py-2 font-mono">{{ r.agentName }}</td>
            <td><AppBadge :variant="statusBadge(r.status)">{{ r.status }}</AppBadge></td>
            <td>{{ formatDuration(r.durationMs) }}</td>
            <td>{{ r.inputTokens }} / {{ r.outputTokens }}</td>
            <td>{{ formatTime(r.startedAt) }}</td>
            <td class="text-red-600 truncate max-w-xs">{{ r.errorMessage ?? '' }}</td>
          </tr>
        </tbody>
      </table>
      <AppEmpty v-if="!loading && items.length === 0" />
    </div>

    <div class="flex justify-between items-center">
      <div class="text-sm text-gray-500">共 {{ total }} 条 · 第 {{ page }} 页</div>
      <div class="space-x-2">
        <AppButton variant="secondary" :disabled="page <= 1" @click="prevPage">上一页</AppButton>
        <AppButton variant="secondary" :disabled="page * pageSize >= total" @click="nextPage">下一页</AppButton>
      </div>
    </div>

    <!-- 详情弹窗：完整 Prompt / Response -->
    <AppModal v-model="detailOpen" title="Agent 运行详情" width="860px">
      <div v-if="detailLoading" class="text-sm text-gray-500 py-6 text-center">加载中...</div>
      <div v-else-if="detail" class="space-y-3">
        <div class="grid grid-cols-2 md:grid-cols-4 gap-2 text-sm">
          <div><span class="text-gray-500">Agent：</span><span class="font-mono">{{ detail.agentName }}</span></div>
          <div><span class="text-gray-500">状态：</span><AppBadge :variant="statusBadge(detail.status)">{{ detail.status }}</AppBadge></div>
          <div><span class="text-gray-500">耗时：</span>{{ formatDuration(detail.durationMs) }}</div>
          <div><span class="text-gray-500">Steps：</span>{{ detail.stepCount }}</div>
          <div><span class="text-gray-500">Tokens：</span>{{ detail.inputTokens }} / {{ detail.outputTokens }}</div>
          <div><span class="text-gray-500">开始：</span>{{ formatTime(detail.startedAt) }}</div>
          <div v-if="detail.finishedAt"><span class="text-gray-500">结束：</span>{{ formatTime(detail.finishedAt) }}</div>
          <div v-if="detail.projectId"><span class="text-gray-500">项目：</span><span class="font-mono text-xs">{{ detail.projectId }}</span></div>
        </div>
        <div v-if="detail.errorMessage" class="rounded border border-red-200 bg-red-50 p-2 text-sm text-red-700">
          错误：{{ detail.errorMessage }}
        </div>

        <div class="border-b flex gap-1">
          <button
            class="px-3 py-1 text-sm border-b-2 -mb-px"
            :class="activeTab === 'input' ? 'border-blue-500 text-blue-600 font-semibold' : 'border-transparent text-gray-600'"
            @click="activeTab = 'input'"
          >Input（Prompt）</button>
          <button
            class="px-3 py-1 text-sm border-b-2 -mb-px"
            :class="activeTab === 'output' ? 'border-blue-500 text-blue-600 font-semibold' : 'border-transparent text-gray-600'"
            @click="activeTab = 'output'"
          >Output（Response）</button>
          <div class="ml-auto">
            <AppButton size="sm" variant="ghost"
              @click="copyText(activeTab === 'input' ? (detail.inputFull ?? detail.inputPreview) : (detail.outputFull ?? detail.outputPreview))">
              <i class="i-lucide-copy" /> 复制
            </AppButton>
          </div>
        </div>

        <pre v-if="activeTab === 'input'" class="bg-gray-50 border rounded p-3 text-xs whitespace-pre-wrap break-words max-h-[460px] overflow-auto">{{ detail.inputFull ?? detail.inputPreview ?? '（无 Input 记录）' }}</pre>
        <pre v-else class="bg-gray-50 border rounded p-3 text-xs whitespace-pre-wrap break-words max-h-[460px] overflow-auto">{{ detail.outputFull ?? detail.outputPreview ?? '（无 Output 记录）' }}</pre>

        <div class="text-xs text-gray-400">
          注：完整文本上限 200K 字符，超长部分已截断；旧记录可能仅有 500 字符的 Preview。
        </div>
      </div>
    </AppModal>
  </div>
</template>
