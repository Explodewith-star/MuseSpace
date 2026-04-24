<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import AppDropdown from '@/components/base/AppDropdown.vue'
import { useAuthStore } from '@/store/modules/auth'
import { useToast } from '@/composables/useToast'
import { getLlmProvider, setLlmProvider, setLlmModel } from '@/api/llmProvider'
import type { LlmProviderType, LlmModelOption } from '@/types/models'

const router = useRouter()
const toast = useToast()
const authStore = useAuthStore()

// LLM 状态
const active = ref<LlmProviderType>('OpenRouter')
const currentModel = ref('')
const availableModels = ref<LlmModelOption[]>([])
const switching = ref(false)

const modelLabel = computed(() => {
  const found = availableModels.value.find(m => m.id === currentModel.value)
  return found?.label ?? currentModel.value
})

async function loadStatus() {
  try {
    const s = await getLlmProvider()
    active.value = s.active
    currentModel.value = s.currentModel
    availableModels.value = s.availableModels
  } catch {
    // ignore
  }
}

async function switchProvider(provider: LlmProviderType) {
  if (provider === active.value || switching.value) return
  switching.value = true
  try {
    const s = await setLlmProvider(provider)
    active.value = s.active
    currentModel.value = s.currentModel
    toast.success(`已切换至 ${provider}`)
  } finally {
    switching.value = false
  }
}

async function switchModel(modelId: string) {
  if (modelId === currentModel.value || switching.value) return
  switching.value = true
  try {
    const s = await setLlmModel(modelId)
    active.value = s.active
    currentModel.value = s.currentModel
    toast.success('模型已更新')
  } finally {
    switching.value = false
  }
}

function goAdminUsers() {
  router.push('/admin/users')
}

function handleLogout() {
  authStore.logout()
  router.push('/login')
}

onMounted(() => {
  if (authStore.isLoggedIn) loadStatus()
})
</script>

<template>
  <AppDropdown v-if="authStore.isLoggedIn" align="right">
    <!-- 触发器：复用原来的用户 tag 风格 -->
    <template #trigger>
      <button class="user-menu__trigger" type="button">
        <i class="i-lucide-user-circle" />
        <span class="user-menu__phone">{{ authStore.user?.phoneNumber }}</span>
        <span v-if="authStore.isAdmin" class="user-menu__admin">管理员</span>
        <i class="i-lucide-chevron-down user-menu__caret" />
      </button>
    </template>

    <!-- 下拉内容 -->
    <div class="user-menu__panel">
      <!-- 用户信息头 -->
      <div class="user-menu__header">
        <div class="user-menu__avatar">
          <i class="i-lucide-user" />
        </div>
        <div class="user-menu__info">
          <div class="user-menu__info-phone">{{ authStore.user?.phoneNumber }}</div>
          <div class="user-menu__info-role">
            {{ authStore.isAdmin ? '管理员' : '普通用户' }}
          </div>
        </div>
      </div>

      <div class="user-menu__divider" />

      <!-- AI 渠道 -->
      <div class="user-menu__section">
        <div class="user-menu__section-title">
          <i class="i-lucide-cpu" />
          <span>AI 渠道</span>
        </div>
        <div class="user-menu__provider-toggle" @click.stop>
          <button
            :class="['user-menu__provider-btn', { active: active === 'OpenRouter' }]"
            :disabled="switching"
            @click="switchProvider('OpenRouter')"
          >
            OpenRouter
          </button>
          <button
            :class="['user-menu__provider-btn', { active: active === 'DeepSeek' }]"
            :disabled="switching"
            @click="switchProvider('DeepSeek')"
          >
            DeepSeek
          </button>
        </div>
      </div>

      <!-- 模型列表（仅 OpenRouter） -->
      <div v-if="active === 'OpenRouter' && availableModels.length > 0" class="user-menu__section">
        <div class="user-menu__section-title">
          <i class="i-lucide-brain" />
          <span>模型</span>
          <span class="user-menu__current">{{ modelLabel }}</span>
        </div>
        <div class="user-menu__model-list" @click.stop>
          <button
            v-for="m in availableModels"
            :key="m.id"
            :class="['user-menu__model-item', { active: m.id === currentModel }]"
            :disabled="switching"
            @click="switchModel(m.id)"
          >
            <i v-if="m.id === currentModel" class="i-lucide-check" />
            <span class="user-menu__model-label">{{ m.label }}</span>
          </button>
        </div>
      </div>
      <div v-else-if="active === 'DeepSeek'" class="user-menu__hint">
        当前使用 DeepSeek 默认模型
      </div>

      <div class="user-menu__divider" />

      <!-- 管理员入口 -->
      <button v-if="authStore.isAdmin" class="user-menu__item" @click="goAdminUsers">
        <i class="i-lucide-users" />
        <span>用户管理</span>
      </button>

      <!-- 退出 -->
      <button class="user-menu__item user-menu__item--danger" @click="handleLogout">
        <i class="i-lucide-log-out" />
        <span>退出登录</span>
      </button>
    </div>
  </AppDropdown>

  <!-- 未登录：直接显示登录按钮 -->
  <router-link v-else to="/login" class="user-menu__login">
    <i class="i-lucide-log-in" />
    <span>登录</span>
  </router-link>
</template>

<style scoped>
/* ── 触发器 ─────────────────────────────────────────────────────── */
.user-menu__trigger {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--color-text-secondary);
  background: var(--color-bg-base);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  padding: 4px 10px 4px 8px;
  cursor: pointer;
  transition: border-color 0.15s, color 0.15s;
}
.user-menu__trigger:hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}
.user-menu__phone {
  max-width: 110px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.user-menu__admin {
  font-size: 10px;
  color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  border-radius: 4px;
  padding: 1px 5px;
  font-weight: 600;
}
.user-menu__caret {
  font-size: 12px;
  opacity: 0.6;
}

/* ── 面板 ─────────────────────────────────────────────────────── */
.user-menu__panel {
  min-width: 280px;
  padding: 4px;
}

.user-menu__header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 10px 8px;
}
.user-menu__avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--color-primary) 14%, transparent);
  color: var(--color-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  flex-shrink: 0;
}
.user-menu__info-phone {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-primary);
}
.user-menu__info-role {
  font-size: 12px;
  color: var(--color-text-muted);
}

.user-menu__divider {
  height: 1px;
  background: var(--color-border);
  margin: 4px 0;
}

.user-menu__section {
  padding: 8px 10px;
}
.user-menu__section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  font-weight: 600;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 6px;
}
.user-menu__current {
  margin-left: auto;
  text-transform: none;
  letter-spacing: 0;
  font-weight: 500;
  color: var(--color-primary);
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-menu__provider-toggle {
  display: flex;
  background: var(--color-bg-elevated);
  border-radius: 8px;
  padding: 2px;
  gap: 2px;
}
.user-menu__provider-btn {
  flex: 1;
  padding: 5px 8px;
  font-size: 12px;
  font-weight: 500;
  background: transparent;
  color: var(--color-text-muted);
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.15s;
}
.user-menu__provider-btn:hover:not(:disabled):not(.active) {
  color: var(--color-text-primary);
}
.user-menu__provider-btn.active {
  background: var(--color-bg-surface);
  color: var(--color-primary);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
}
.user-menu__provider-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.user-menu__model-list {
  display: flex;
  flex-direction: column;
  gap: 2px;
  max-height: 220px;
  overflow-y: auto;
}
.user-menu__model-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 8px;
  font-size: 12px;
  text-align: left;
  background: transparent;
  color: var(--color-text-primary);
  border: none;
  border-radius: 6px;
  cursor: pointer;
  width: 100%;
  transition: background 0.15s;
}
.user-menu__model-item:hover:not(:disabled) {
  background: var(--color-bg-elevated);
}
.user-menu__model-item.active {
  color: var(--color-primary);
  font-weight: 500;
}
.user-menu__model-item > i {
  font-size: 13px;
  flex-shrink: 0;
}
.user-menu__model-item.active > i {
  color: var(--color-primary);
}
.user-menu__model-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.user-menu__model-item:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.user-menu__hint {
  padding: 6px 12px 10px;
  font-size: 12px;
  color: var(--color-text-muted);
}

.user-menu__item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  font-size: 13px;
  color: var(--color-text-primary);
  background: transparent;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  width: 100%;
  text-align: left;
  transition: background 0.15s, color 0.15s;
}
.user-menu__item:hover {
  background: var(--color-bg-elevated);
}
.user-menu__item--danger {
  color: var(--color-text-muted);
}
.user-menu__item--danger:hover {
  background: color-mix(in srgb, #ef4444 10%, transparent);
  color: #ef4444;
}

/* 未登录的登录按钮 */
.user-menu__login {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  font-size: 13px;
  color: var(--color-primary);
  background: color-mix(in srgb, var(--color-primary) 10%, transparent);
  border-radius: 8px;
  text-decoration: none;
  transition: background 0.15s;
}
.user-menu__login:hover {
  background: color-mix(in srgb, var(--color-primary) 18%, transparent);
}
</style>
