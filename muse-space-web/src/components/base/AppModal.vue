<script setup lang="ts">
interface Props {
  modelValue: boolean
  title?: string
  width?: string
  closable?: boolean
}

withDefaults(defineProps<Props>(), {
  closable: true,
  width: '520px',
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

function close() {
  emit('update:modelValue', false)
}
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div v-if="modelValue" class="app-modal-overlay" @click.self="closable && close()">
        <div class="app-modal" :style="{ width }">
          <div class="app-modal__header">
            <span class="app-modal__title">{{ title }}</span>
            <button v-if="closable" class="app-modal__close" @click="close">
              <i class="i-lucide-x" />
            </button>
          </div>
          <div class="app-modal__body">
            <slot />
          </div>
          <div v-if="$slots.footer" class="app-modal__footer">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.app-modal-overlay {
  position: fixed;
  inset: 0;
  background-color: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 24px;
}

.app-modal {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 14px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.2);
  display: flex;
  flex-direction: column;
  max-height: 90vh;
  overflow: hidden;
}

.app-modal__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-border);
  flex-shrink: 0;
}

.app-modal__title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.app-modal__close {
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
  transition: background-color 0.15s;
}

.app-modal__close:hover {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.app-modal__body {
  padding: 20px;
  overflow-y: auto;
  flex: 1;
}

.app-modal__footer {
  padding: 14px 20px;
  border-top: 1px solid var(--color-border);
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  flex-shrink: 0;
}

/* Transition */
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.2s;
}
.modal-enter-active .app-modal,
.modal-leave-active .app-modal {
  transition: transform 0.2s;
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
.modal-enter-from .app-modal {
  transform: scale(0.96) translateY(10px);
}
.modal-leave-to .app-modal {
  transform: scale(0.96) translateY(10px);
}
</style>
