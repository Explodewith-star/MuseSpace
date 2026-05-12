<script setup lang="ts">
interface Props {
  modelValue: boolean
  title?: string
  width?: string
  closeOnOverlay?: boolean
}

withDefaults(defineProps<Props>(), {
  width: '480px',
  closeOnOverlay: false,
})

const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

function close() {
  emit('update:modelValue', false)
}
</script>

<template>
  <Teleport to="body">
    <Transition name="drawer">
      <div v-if="modelValue" class="drawer-overlay" @click.self="closeOnOverlay && close()">
        <div class="drawer-panel" :style="{ width }">
          <div class="drawer-header">
            <span class="drawer-title">{{ title }}</span>
            <button class="drawer-close" @click="close">
              <i class="i-lucide-x" />
            </button>
          </div>
          <div class="drawer-body">
            <slot />
          </div>
          <div v-if="$slots.footer" class="drawer-footer">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<!-- Non-scoped：Transition 内部子元素动画需要穿透 scoped -->
<style>
.drawer-enter-active,
.drawer-leave-active {
  transition: opacity 0.25s ease;
}
.drawer-enter-active .drawer-panel,
.drawer-leave-active .drawer-panel {
  transition: transform 0.25s ease;
}
.drawer-enter-from,
.drawer-leave-to {
  opacity: 0;
}
.drawer-enter-from .drawer-panel,
.drawer-leave-to .drawer-panel {
  transform: translateX(100%);
}
</style>

<style scoped>
.drawer-overlay {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.4);
  z-index: 1000;
  display: flex;
  justify-content: flex-end;
}

.drawer-panel {
  height: 100%;
  background-color: var(--color-bg-surface);
  border-left: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.12);
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
  justify-content: flex-end;
  gap: 8px;
  flex-shrink: 0;
}
</style>
