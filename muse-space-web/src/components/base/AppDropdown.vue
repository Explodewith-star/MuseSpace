<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

interface Props {
  /** 面板对齐方向 */
  align?: 'left' | 'right'
  /** 触发器与面板的间距，px */
  offset?: number
  /** 禁用 */
  disabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  align: 'right',
  offset: 6,
  disabled: false,
})

const open = ref(false)
const rootRef = ref<HTMLElement>()

function toggle() {
  if (props.disabled) return
  open.value = !open.value
}

function close() {
  open.value = false
}

function onDocClick(e: MouseEvent) {
  if (!rootRef.value) return
  if (!rootRef.value.contains(e.target as Node)) close()
}

function onEsc(e: KeyboardEvent) {
  if (e.key === 'Escape') close()
}

onMounted(() => {
  document.addEventListener('mousedown', onDocClick)
  document.addEventListener('keydown', onEsc)
})
onUnmounted(() => {
  document.removeEventListener('mousedown', onDocClick)
  document.removeEventListener('keydown', onEsc)
})

defineExpose({ open, close, toggle })
</script>

<template>
  <div ref="rootRef" class="app-dropdown">
    <div class="app-dropdown__trigger" @click="toggle">
      <slot name="trigger" :open="open" />
    </div>
    <Transition name="app-dropdown-fade">
      <div
        v-if="open"
        :class="['app-dropdown__panel', `app-dropdown__panel--${align}`]"
        :style="{ marginTop: `${offset}px` }"
        @click="close"
      >
        <slot :close="close" />
      </div>
    </Transition>
  </div>
</template>

<style scoped>
.app-dropdown {
  position: relative;
  display: inline-block;
}

.app-dropdown__trigger {
  display: inline-flex;
  cursor: pointer;
}

.app-dropdown__panel {
  position: absolute;
  top: 100%;
  z-index: 500;
  min-width: 180px;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
  padding: 6px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.app-dropdown__panel--right {
  right: 0;
}
.app-dropdown__panel--left {
  left: 0;
}

.app-dropdown-fade-enter-active,
.app-dropdown-fade-leave-active {
  transition: opacity 0.12s ease, transform 0.12s ease;
}
.app-dropdown-fade-enter-from,
.app-dropdown-fade-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
