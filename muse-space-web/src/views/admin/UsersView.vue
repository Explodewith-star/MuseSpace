<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getUsers, createUser, deleteUser } from '@/api/auth'
import type { UserResponse } from '@/api/auth'
import { useToast } from '@/composables/useToast'
import AppInput from '@/components/base/AppInput.vue'
import AppButton from '@/components/base/AppButton.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'

const toast = useToast()
const users = ref<UserResponse[]>([])
const loading = ref(false)
const addPhone = ref('')
const addLoading = ref(false)

const confirmVisible = ref(false)
const confirmLoading = ref(false)
const pendingDelete = ref<UserResponse | null>(null)

async function fetchUsers() {
  loading.value = true
  try {
    users.value = await getUsers()
  } finally {
    loading.value = false
  }
}

async function handleAdd() {
  const phone = addPhone.value.trim()
  if (!phone) {
    toast.warning('请输入手机号')
    return
  }
  addLoading.value = true
  try {
    const user = await createUser({ phoneNumber: phone })
    users.value.push(user)
    addPhone.value = ''
    toast.success('用户添加成功')
  } finally {
    addLoading.value = false
  }
}

function askDelete(user: UserResponse) {
  pendingDelete.value = user
  confirmVisible.value = true
}

async function doDelete() {
  if (!pendingDelete.value) return
  confirmLoading.value = true
  try {
    await deleteUser(pendingDelete.value.id)
    users.value = users.value.filter(u => u.id !== pendingDelete.value!.id)
    toast.success('删除成功')
    confirmVisible.value = false
    pendingDelete.value = null
  } finally {
    confirmLoading.value = false
  }
}

onMounted(fetchUsers)
</script>

<template>
  <div class="users-page">
    <h1 class="users-page__title">用户管理</h1>

    <!-- 添加用户 -->
    <div class="users-page__add">
      <div class="users-page__add-input">
        <AppInput
          v-model="addPhone"
          placeholder="输入手机号添加用户"
          prefix-icon="i-lucide-smartphone"
          @keyup.enter="handleAdd"
        />
      </div>
      <AppButton :loading="addLoading" @click="handleAdd">
        <i class="i-lucide-user-plus" />
        添加
      </AppButton>
    </div>

    <!-- 用户列表 -->
    <div class="users-page__list">
      <div v-if="loading" class="users-page__loading">
        <i class="i-lucide-loader-circle users-page__spin" />
        <span>加载中...</span>
      </div>
      <template v-else>
        <AppEmpty v-if="users.length === 0" icon="i-lucide-users" title="暂无用户" />
        <div
          v-for="user in users"
          v-else
          :key="user.id"
          class="users-page__item"
        >
          <div class="users-page__item-main">
            <div class="users-page__item-phone">
              <i class="i-lucide-smartphone users-page__item-icon" />
              {{ user.phoneNumber }}
              <AppBadge v-if="user.role === 'Admin'" variant="primary" size="sm">管理员</AppBadge>
              <AppBadge v-else variant="default" size="sm">普通用户</AppBadge>
            </div>
            <div class="users-page__item-meta">
              加入于 {{ new Date(user.createdAt).toLocaleDateString() }}
              <template v-if="user.lastLoginAt">
                · 最后登录 {{ new Date(user.lastLoginAt).toLocaleDateString() }}
              </template>
            </div>
          </div>
          <AppButton
            v-if="user.role !== 'Admin'"
            variant="danger"
            size="sm"
            @click="askDelete(user)"
          >
            <i class="i-lucide-trash-2" />
            删除
          </AppButton>
        </div>
      </template>
    </div>

    <AppConfirm
      v-model="confirmVisible"
      title="删除用户"
      :message="`确认删除用户 ${pendingDelete?.phoneNumber ?? ''} 吗？此操作不可撤销。`"
      variant="danger"
      confirm-text="删除"
      :loading="confirmLoading"
      @confirm="doDelete"
    />
  </div>
</template>

<style scoped>
.users-page {
  padding: 24px;
  max-width: 720px;
  margin: 0 auto;
}

.users-page__title {
  font-size: 20px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0 0 20px;
}

.users-page__add {
  display: flex;
  gap: 12px;
  align-items: flex-start;
  margin-bottom: 20px;
}

.users-page__add-input {
  flex: 1;
}

.users-page__list {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  overflow: hidden;
}

.users-page__loading {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 40px;
  color: var(--color-text-muted);
  font-size: 14px;
}

.users-page__spin {
  animation: users-spin 0.9s linear infinite;
}
@keyframes users-spin {
  to { transform: rotate(360deg); }
}

.users-page__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 16px;
  border-bottom: 1px solid var(--color-border);
}
.users-page__item:last-child {
  border-bottom: none;
}

.users-page__item-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.users-page__item-phone {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.users-page__item-icon {
  font-size: 15px;
  color: var(--color-text-muted);
}

.users-page__item-meta {
  font-size: 12px;
  color: var(--color-text-muted);
  padding-left: 22px;
}
</style>
