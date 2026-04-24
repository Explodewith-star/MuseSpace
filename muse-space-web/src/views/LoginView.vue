<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/store/modules/auth'
import { useToast } from '@/composables/useToast'
import AppInput from '@/components/base/AppInput.vue'
import AppButton from '@/components/base/AppButton.vue'

const router = useRouter()
const authStore = useAuthStore()
const toast = useToast()

const phoneNumber = ref('')
const password = ref('')
const showAdminLogin = ref(false)
const loading = ref(false)
const phoneError = ref('')
const passwordError = ref('')

const isAdminPhone = computed(() => phoneNumber.value.trim() === '15236282685')

async function handleLogin() {
  phoneError.value = ''
  passwordError.value = ''

  const phone = phoneNumber.value.trim()
  if (!phone) {
    phoneError.value = '请输入手机号'
    return
  }

  // 管理员手机号第一次点击：展示密码框
  if (isAdminPhone.value && !showAdminLogin.value) {
    showAdminLogin.value = true
    return
  }

  loading.value = true
  try {
    if (isAdminPhone.value) {
      if (!password.value) {
        passwordError.value = '请输入管理员密码'
        return
      }
      await authStore.loginAsAdmin({ phoneNumber: phone, password: password.value })
      toast.success('管理员登录成功')
    } else {
      await authStore.loginAsUser({ phoneNumber: phone })
      toast.success('登录成功')
    }
    router.push('/projects')
  } catch (err: unknown) {
    // 提取后端返回的错误信息，在表单字段中展示
    type AxiosLike = { response?: { data?: { errorMessage?: string } }; message?: string }
    const axiosErr = err as AxiosLike
    const msg = axiosErr?.response?.data?.errorMessage
      ?? (err instanceof Error && err.message ? err.message : null)
      ?? '登录失败，请稍后重试'
    if (isAdminPhone.value) {
      passwordError.value = msg
    } else {
      phoneError.value = msg
    }
  } finally {
    loading.value = false
  }
}

function continueAsGuest() {
  router.push('/projects')
}
</script>

<template>
  <div class="login-page">
    <!-- 背景装饰 -->
    <div class="login-bg-circle login-bg-circle--1" />
    <div class="login-bg-circle login-bg-circle--2" />

    <div class="login-container">
      <!-- Logo -->
      <div class="login-logo">
        <i class="i-lucide-feather login-logo__icon" />
        <span class="login-logo__text">MuseSpace</span>
      </div>
      <p class="login-subtitle">AI 辅助创作平台</p>

      <!-- Card -->
      <div class="login-card">
        <h2 class="login-card__title">欢迎回来</h2>
        <p class="login-card__desc">登录后即可拥有私有项目与个人设置</p>

        <div class="login-form">
          <AppInput
            v-model="phoneNumber"
            label="手机号"
            placeholder="请输入手机号"
            prefixIcon="i-lucide-smartphone"
            :error="phoneError"
            @keyup.enter="handleLogin"
          />

          <Transition name="slide-down">
            <AppInput
              v-if="showAdminLogin"
              v-model="password"
              type="password"
              label="管理员密码"
              placeholder="请输入管理员密码"
              prefixIcon="i-lucide-lock"
              :error="passwordError"
              @keyup.enter="handleLogin"
            />
          </Transition>

          <AppButton
            class="login-btn"
            size="lg"
            :loading="loading"
            @click="handleLogin"
          >
            <template v-if="!loading">
              <i v-if="isAdminPhone && !showAdminLogin" class="i-lucide-shield" />
              <i v-else class="i-lucide-log-in" />
            </template>
            <span v-if="loading">登录中...</span>
            <span v-else-if="isAdminPhone && !showAdminLogin">以管理员身份继续</span>
            <span v-else>登录</span>
          </AppButton>
        </div>

        <div class="login-divider">
          <span>或者</span>
        </div>

        <AppButton variant="secondary" class="login-guest-btn" @click="continueAsGuest">
          <i class="i-lucide-user" />
          以游客身份继续使用
        </AppButton>

        <p class="login-hint">游客数据为公共共享区，登录后可拥有私有项目</p>
      </div>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: var(--color-bg-base);
  position: relative;
  overflow: hidden;
}

/* 背景光晕 */
.login-bg-circle {
  position: absolute;
  border-radius: 50%;
  filter: blur(80px);
  opacity: 0.12;
  pointer-events: none;
}
.login-bg-circle--1 {
  width: 400px;
  height: 400px;
  background: var(--color-primary);
  top: -100px;
  right: -80px;
}
.login-bg-circle--2 {
  width: 300px;
  height: 300px;
  background: var(--color-primary);
  bottom: -80px;
  left: -60px;
  opacity: 0.08;
}

.login-container {
  position: relative;
  width: 100%;
  max-width: 400px;
  padding: 0 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.login-logo {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 6px;
}

.login-logo__icon {
  font-size: 32px;
  color: var(--color-primary);
}

.login-logo__text {
  font-size: 32px;
  font-weight: 800;
  color: var(--color-primary);
  letter-spacing: -0.5px;
}

.login-subtitle {
  font-size: 14px;
  color: var(--color-text-muted);
  margin-bottom: 28px;
}

/* Card */
.login-card {
  width: 100%;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  padding: 32px;
  box-shadow: 0 8px 40px rgba(0, 0, 0, 0.08);
}

.login-card__title {
  font-size: 20px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 4px;
}

.login-card__desc {
  font-size: 13px;
  color: var(--color-text-muted);
  margin-bottom: 24px;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

/* 主登录按钮：仅 layout，样式由 AppButton 主导 */
.login-btn {
  width: 100%;
  margin-top: 4px;
}

/* 分割线 */
.login-divider {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 20px 0 16px;
  color: var(--color-text-muted);
  font-size: 12px;
}
.login-divider::before,
.login-divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--color-border);
}

/* 游客按钮：仅宽度 */
.login-guest-btn {
  width: 100%;
}

.login-hint {
  font-size: 12px;
  color: var(--color-text-muted);
  text-align: center;
  margin-top: 14px;
}

/* 密码框展开动画 */
.slide-down-enter-active,
.slide-down-leave-active {
  transition: all 0.25s ease;
  overflow: hidden;
}
.slide-down-enter-from,
.slide-down-leave-to {
  opacity: 0;
  max-height: 0;
  transform: translateY(-6px);
}
.slide-down-enter-to,
.slide-down-leave-from {
  opacity: 1;
  max-height: 100px;
  transform: translateY(0);
}
</style>
