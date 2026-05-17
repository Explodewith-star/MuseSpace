<script setup lang="ts">
import { ref, watch } from 'vue'

interface Props {
  modelValue: boolean
  title?: string
  width?: string
  clearHandler?: () => void
  openTrigger?: number
}

const props = withDefaults(defineProps<Props>(), {
  width: '480px',
})

const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const minimized = ref(false)

function close() {
  emit('update:modelValue', false)
}

function minimize() {
  minimized.value = true
}

function restore() {
  minimized.value = false
}

// 外部关闭时重置 minimized
watch(
  () => props.modelValue,
  (val) => {
    if (!val) minimized.value = false
  },
  { flush: 'sync' },
)

// openTrigger 自增时恢复最小化（父组件调用 openXxx 时触发）
watch(
  () => props.openTrigger,
  () => {
    minimized.value = false
  },
)
</script>

<template>
  <Teleport to="body">
    <!-- 遮罩：非最小化时显示，点击触发最小化 -->
    <Transition name="overlay-fade">
      <div
        v-if="modelValue && !minimized"
        class="drawer-backdrop"
        @click="minimize"
      />
    </Transition>

    <!-- 抽屉面板：modelValue 控制挂载，CSS class 控制最小化动效 -->
    <Transition name="drawer">
      <div
        v-if="modelValue"
        class="drawer-panel"
        :class="{ 'drawer-panel--minimized': minimized }"
        :style="{ width }"
      >
        <div class="drawer-header">
          <span class="drawer-title">{{ title }}</span>
          <button class="drawer-close" @click="close">
            <i class="i-lucide-x" />
          </button>
        </div>
        <div class="drawer-body">
          <slot />
        </div>
        <div v-if="$slots.footer || clearHandler" class="drawer-footer">
          <button v-if="clearHandler" class="btn-clear" @click="clearHandler">
            <i class="i-lucide-rotate-ccw" />
            清空
          </button>
          <slot name="footer" />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<!-- Non-scoped：Transition 动画需要穿透 scoped -->
<style>
/* 遮罩淡入淡出 */
.overlay-fade-enter-active,
.overlay-fade-leave-active {
  transition: opacity 0.2s ease;
}
.overlay-fade-enter-from,
.overlay-fade-leave-to {
  opacity: 0;
}

/* 面板从右侧滑入/滑出 */
.drawer-enter-active,
.drawer-leave-active {
  transition: transform 0.25s ease;
}
.drawer-enter-from,
.drawer-leave-to {
  transform: translateX(100%);
}
</style>

<style scoped>
.drawer-backdrop {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.4);
  z-index: 1000;
}

.drawer-panel {
  position: fixed;
  top: 0;
  right: 0;
  height: 100%;
  background-color: var(--color-bg-surface);
  border-left: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.12);
  z-index: 1001;
  /* 最小化时平滑滑出 */
  transition: transform 0.25s ease;
}

.drawer-panel--minimized {
  transform: translateX(100%);
  pointer-events: none;
}

.drawer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-border);
  flex-shrink: 0;
}

.drawer-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.drawer-close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 16px;
  transition:
    background-color 0.15s,
    color 0.15s;
}

.drawer-close:hover {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.drawer-body {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

.drawer-footer {
  padding: 14px 20px;
  border-top: 1px solid var(--color-border);
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  flex-shrink: 0;
}

/* 清空按钮：靠左对齐 */
.btn-clear {
  display: flex;
  align-items: center;
  gap: 5px;
  margin-right: auto;
  padding: 6px 10px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 13px;
  transition:
    background-color 0.15s,
    color 0.15s,
    border-color 0.15s;
}

.btn-clear:hover {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
  border-color: var(--color-border-strong, var(--color-border));
}

.btn-clear .i-lucide-rotate-ccw {
  font-size: 13px;
}
</style>
