<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getAdminProjects, assignProject, deleteAdminProject, getUsers } from '@/api/auth'
import type { AdminProject, UserResponse } from '@/api/auth'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const projects = ref<AdminProject[]>([])
const users = ref<UserResponse[]>([])
const loading = ref(false)

async function fetchData() {
  loading.value = true
  try {
    ;[projects.value, users.value] = await Promise.all([getAdminProjects(), getUsers()])
  } finally {
    loading.value = false
  }
}

async function handleAssign(project: AdminProject, userId: string) {
  const targetId = userId === '' ? null : userId
  await assignProject(project.id, targetId)
  project.userId = targetId ?? undefined
  toast.success('分配成功')
}

async function handleDelete(project: AdminProject) {
  if (!confirm(`确认删除项目「${project.name}」吗？此操作不可撤销。`)) return
  await deleteAdminProject(project.id)
  projects.value = projects.value.filter(p => p.id !== project.id)
  toast.success('删除成功')
}

onMounted(fetchData)
</script>

<template>
  <div class="p-6 max-w-5xl mx-auto">
    <h1 class="text-xl font-bold text-[var(--color-text-primary)] mb-6">项目管理</h1>

    <div class="bg-[var(--color-bg-surface)] rounded-xl border border-[var(--color-border)] overflow-hidden">
      <div v-if="loading" class="p-8 text-center text-[var(--color-text-muted)]">
        <i class="i-lucide-loader-2 animate-spin mr-2" />加载中...
      </div>
      <template v-else>
        <!-- 表头 -->
        <div class="grid grid-cols-[1fr_120px_160px_100px] px-4 py-2 border-b border-[var(--color-border)]
                    text-xs text-[var(--color-text-muted)] bg-[var(--color-bg-base)]">
          <div>项目名称</div>
          <div>类型</div>
          <div>所属用户</div>
          <div>操作</div>
        </div>

        <div
          v-for="project in projects"
          :key="project.id"
          class="grid grid-cols-[1fr_120px_160px_100px] items-center px-4 py-3
                 border-b border-[var(--color-border)] last:border-0"
        >
          <div>
            <div class="text-sm font-medium text-[var(--color-text-primary)]">{{ project.name }}</div>
            <div class="text-xs text-[var(--color-text-muted)] truncate">{{ project.description }}</div>
          </div>
          <div class="text-xs text-[var(--color-text-secondary)]">{{ project.genre ?? '未设置' }}</div>

          <!-- 分配下拉 -->
          <div>
            <select
              :value="project.userId ?? ''"
              class="text-xs px-2 py-1 rounded border border-[var(--color-border)] bg-[var(--color-bg-base)]
                     text-[var(--color-text-primary)] outline-none focus:border-[var(--color-primary)]"
              @change="handleAssign(project, ($event.target as HTMLSelectElement).value)"
            >
              <option value="">游客共享</option>
              <option v-for="u in users" :key="u.id" :value="u.id">
                {{ u.phoneNumber }}
              </option>
            </select>
          </div>

          <div>
            <button
              class="text-xs text-red-500 hover:text-red-600 px-3 py-1 rounded border border-red-200 hover:border-red-400"
              @click="handleDelete(project)"
            >
              删除
            </button>
          </div>
        </div>

        <div v-if="projects.length === 0" class="p-8 text-center text-[var(--color-text-muted)] text-sm">
          暂无项目
        </div>
      </template>
    </div>
  </div>
</template>
