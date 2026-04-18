<script setup lang="ts">
import { useToast } from '@/composables/useToast'
import type { ToastItem } from '@/composables/useToast'

const { toasts, remove } = useToast()

const iconMap: Record<ToastItem['type'], string> = {
  success: 'i-lucide-circle-check',
  error: 'i-lucide-circle-x',
  warning: 'i-lucide-triangle-alert',
  info: 'i-lucide-info',
}
</script>

<template>
  <Teleport to="body">
    <div class="app-toast-container">
      <TransitionGroup name="toast">
        <div
          v-for="item in toasts"
          :key="item.id"
          :class="['app-toast', `app-toast--${item.type}`]"
          @click="remove(item.id)"
        >
          <i :class="['app-toast__icon', iconMap[item.type]]" />
          <span class="app-toast__message">{{ item.message }}</span>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<style scoped>
.app-toast-container {
  position: fixed;
  top: 20px;
  right: 20px;
  z-index: 2000;
  display: flex;
  flex-direction: column;
  gap: 8px;
  pointer-events: none;
}

.app-toast {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  pointer-events: all;
  cursor: pointer;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
  min-width: 240px;
  max-width: 380px;
  border: 1px solid transparent;
}

.app-toast--success {
  background-color: color-mix(in srgb, var(--color-success) 12%, var(--color-bg-surface));
  color: var(--color-success);
  border-color: color-mix(in srgb, var(--color-success) 30%, transparent);
}

.app-toast--error {
  background-color: color-mix(in srgb, var(--color-danger) 12%, var(--color-bg-surface));
  color: var(--color-danger);
  border-color: color-mix(in srgb, var(--color-danger) 30%, transparent);
}

.app-toast--warning {
  background-color: color-mix(in srgb, var(--color-accent) 12%, var(--color-bg-surface));
  color: var(--color-accent);
  border-color: color-mix(in srgb, var(--color-accent) 30%, transparent);
}

.app-toast--info {
  background-color: color-mix(in srgb, var(--color-primary) 12%, var(--color-bg-surface));
  color: var(--color-primary);
  border-color: color-mix(in srgb, var(--color-primary) 30%, transparent);
}

.app-toast__icon {
  font-size: 16px;
  flex-shrink: 0;
}

.app-toast__message {
  flex: 1;
}

/* Transition */
.toast-enter-active,
.toast-leave-active {
  transition:
    opacity 0.25s,
    transform 0.25s;
}
.toast-enter-from {
  opacity: 0;
  transform: translateX(20px);
}
.toast-leave-to {
  opacity: 0;
  transform: translateX(20px);
}
</style>
