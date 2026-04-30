<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  getAdminAgentRuns,
  getAdminAgentRunStats,
  type AgentRunListItem,
  type AgentRunStats,
  type AgentRunStatus,
} from '@/api/admin'
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'

const items = ref<AgentRunListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 50
const loading = ref(false)

const filterAgentName = ref('')
const filterStatus = ref<'' | AgentRunStatus>('')

const stats = ref<AgentRunStats | null>(null)
const statsDays = ref(7)

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

function statusBadge(s: AgentRunStatus) {
  if (s === 'Succeeded') return 'success'
  if (s === 'Failed') return 'danger'
  return 'info'
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
          <tr v-for="r in items" :key="r.id" class="border-t hover:bg-gray-50">
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
  </div>
</template>
