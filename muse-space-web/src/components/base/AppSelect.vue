<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'

export interface SelectOption {
  value: string
  label: string
  disabled?: boolean
}

interface Props {
  modelValue?: string
  options: SelectOption[]
  label?: string
  placeholder?: string
  disabled?: boolean
  /** 是否开启模糊搜索，默认 true */
  searchable?: boolean
  searchPlaceholder?: string
  emptyText?: string
  /** 下拉面板最大高度（px），默认 260 */
  maxHeight?: number
}

const props = withDefaults(defineProps<Props>(), {
  placeholder: '请选择',
  searchable: true,
  searchPlaceholder: '搜索...',
  emptyText: '无匹配结果',
  maxHeight: 260,
})

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const open = ref(false)
const openUpward = ref(false)
const searchQuery = ref('')
const rootRef = ref<HTMLElement>()
const triggerRef = ref<HTMLButtonElement>()
const searchRef = ref<HTMLInputElement>()
const panelStyle = ref<Record<string, string>>({})

const selectedLabel = computed(() => props.options.find(o => o.value === props.modelValue)?.label)
const displayLabel = computed(() => selectedLabel.value ?? props.placeholder)
const hasValue = computed(() => !!props.modelValue)

const filteredOptions = computed(() => {
  const q = searchQuery.value.trim().toLowerCase()
  if (!q) return props.options
  return props.options.filter(o => o.label.toLowerCase().includes(q))
})

async function toggle() {
  if (props.disabled) return
  if (open.value) {
    close()
  } else {
    open.value = true
    searchQuery.value = ''
    await nextTick()
    calcPosition()
    if (props.searchable) searchRef.value?.focus()
  }
}

function close() {
  open.value = false
  searchQuery.value = ''
}

function select(opt: SelectOption) {
  if (opt.disabled) return
  emit('update:modelValue', opt.value)
  close()
}

function calcPosition() {
  const el = triggerRef.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  const vh = window.innerHeight
  const spaceBelow = vh - rect.bottom - 8
  const spaceAbove = rect.top - 8
  const maxH = props.maxHeight

  if (spaceBelow >= Math.min(maxH, 120) || spaceBelow >= spaceAbove) {
    // 向下展开
    openUpward.value = false
    panelStyle.value = {
      top: `${rect.bottom + 4}px`,
      left: `${rect.left}px`,
      width: `${rect.width}px`,
      maxHeight: `${Math.min(maxH, Math.max(spaceBelow, 80))}px`,
    }
  } else {
    // 向上展开：bottom 定位，面板底部贴触发器顶部
    openUpward.value = true
    panelStyle.value = {
      bottom: `${vh - rect.top + 4}px`,
      left: `${rect.left}px`,
      width: `${rect.width}px`,
      maxHeight: `${Math.min(maxH, Math.max(spaceAbove, 80))}px`,
    }
  }
}

function onOutsideClick(e: MouseEvent) {
  const target = e.target as Element
  if (rootRef.value?.contains(target as Node)) return
  if (target.closest?.('.app-select-panel')) return
  close()
}

function onScrollOrResize() {
  if (open.value) calcPosition()
}

onMounted(() => {
  document.addEventListener('mousedown', onOutsideClick)
  window.addEventListener('scroll', onScrollOrResize, true)
  window.addEventListener('resize', onScrollOrResize)
})
onUnmounted(() => {
  document.removeEventListener('mousedown', onOutsideClick)
  window.removeEventListener('scroll', onScrollOrResize, true)
  window.removeEventListener('resize', onScrollOrResize)
})
</script>

<template>
  <div ref="rootRef" class="app-select">
    <label v-if="label" class="app-select__label">{{ label }}</label>

    <!-- 触发器 -->
    <button
      ref="triggerRef"
      type="button"
      :class="['app-select__trigger', { 'is-open': open, 'is-disabled': disabled, 'has-value': hasValue }]"
      :disabled="disabled"
      @click="toggle"
    >
      <span class="app-select__value">{{ displayLabel }}</span>
      <i :class="['i-lucide-chevron-down app-select__arrow', { rotated: open }]" />
    </button>

    <!-- 下拉面板：Teleport 到 body，完全脱离父容器 overflow 裁剪 -->
    <Teleport to="body">
      <Transition :name="openUpward ? 'aselect-up' : 'aselect-down'">
        <div
          v-if="open"
          class="app-select-panel"
          :style="panelStyle"
        >
          <!-- 搜索框 -->
          <div v-if="searchable" class="app-select-panel__search-wrap">
            <i class="i-lucide-search app-select-panel__search-icon" />
            <input
              ref="searchRef"
              v-model="searchQuery"
              class="app-select-panel__search"
              :placeholder="searchPlaceholder"
              @keydown.esc="close"
            />
            <button
              v-if="searchQuery"
              type="button"
              class="app-select-panel__search-clear"
              @click.stop="searchQuery = ''"
            >
              <i class="i-lucide-x" />
            </button>
          </div>

          <!-- 选项列表（可滚动） -->
          <div class="app-select-panel__list">
            <button
              v-for="opt in filteredOptions"
              :key="opt.value"
              type="button"
              :class="['app-select-panel__option', {
                'is-selected': opt.value === modelValue,
                'is-disabled': opt.disabled,
              }]"
              @click="select(opt)"
            >
              <span class="app-select-panel__option-label">{{ opt.label }}</span>
              <i v-if="opt.value === modelValue" class="i-lucide-check app-select-panel__check" />
            </button>

            <div v-if="filteredOptions.length === 0" class="app-select-panel__empty">
              <i class="i-lucide-search-x app-select-panel__empty-icon" />
              <span>{{ emptyText }}</span>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.app-select {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.app-select__label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.app-select__trigger {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 7px 10px;
  font-size: 13px;
  text-align: left;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  cursor: pointer;
  gap: 6px;
  transition: border-color 0.15s, box-shadow 0.15s;
}

.app-select__trigger:hover:not(.is-disabled) {
  border-color: var(--color-primary);
}

.app-select__trigger.is-open {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary) 15%, transparent);
}

.app-select__trigger.is-disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.app-select__value {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--color-text-primary);
}

.app-select__trigger:not(.has-value) .app-select__value {
  color: var(--color-text-muted);
}

.app-select__arrow {
  font-size: 14px;
  color: var(--color-text-muted);
  flex-shrink: 0;
  transition: transform 0.2s;
}

.app-select__arrow.rotated {
  transform: rotate(180deg);
}
</style>

<!-- 面板样式不能 scoped：Teleport 后脱离组件 DOM 作用域 -->
<style>
.app-select-panel {
  position: fixed;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  box-shadow: 0 8px 28px rgba(0, 0, 0, 0.13);
  overflow: hidden;
}

/* 搜索栏 */
.app-select-panel__search-wrap {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 10px;
  border-bottom: 1px solid var(--color-border);
  flex-shrink: 0;
}

.app-select-panel__search-icon {
  font-size: 13px;
  color: var(--color-text-muted);
  flex-shrink: 0;
}

.app-select-panel__search {
  flex: 1;
  min-width: 0;
  border: none;
  outline: none;
  background: transparent;
  font-size: 13px;
  color: var(--color-text-primary);
}

.app-select-panel__search::placeholder {
  color: var(--color-text-muted);
}

.app-select-panel__search-clear {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  padding: 0;
  border: none;
  border-radius: 50%;
  background-color: var(--color-bg-elevated);
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 11px;
  flex-shrink: 0;
  transition: background-color 0.1s;
}

.app-select-panel__search-clear:hover {
  background-color: var(--color-border);
}

/* 选项列表（可滚动） */
.app-select-panel__list {
  overflow-y: auto;
  padding: 4px;
  display: flex;
  flex-direction: column;
  gap: 1px;
  scrollbar-width: thin;
  scrollbar-color: var(--color-border) transparent;
}

.app-select-panel__list::-webkit-scrollbar {
  width: 4px;
}

.app-select-panel__list::-webkit-scrollbar-track {
  background: transparent;
}

.app-select-panel__list::-webkit-scrollbar-thumb {
  background-color: var(--color-border);
  border-radius: 2px;
}

/* 单个选项 */
.app-select-panel__option {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 8px 10px;
  font-size: 13px;
  text-align: left;
  background: transparent;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  color: var(--color-text-primary);
  gap: 8px;
  transition: background-color 0.1s;
}

.app-select-panel__option:hover:not(.is-disabled) {
  background-color: var(--color-bg-elevated);
}

.app-select-panel__option.is-selected {
  background-color: color-mix(in srgb, var(--color-primary) 10%, transparent);
  color: var(--color-primary);
  font-weight: 500;
}

.app-select-panel__option.is-disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.app-select-panel__option-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.app-select-panel__check {
  font-size: 14px;
  flex-shrink: 0;
}

/* 空状态 */
.app-select-panel__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 16px 10px;
  color: var(--color-text-muted);
  font-size: 13px;
}

.app-select-panel__empty-icon {
  font-size: 15px;
}

/* 向下展开动画（从顶部缩放） */
.aselect-down-enter-active,
.aselect-down-leave-active {
  transition: opacity 0.15s, transform 0.15s;
  transform-origin: top center;
}

.aselect-down-enter-from,
.aselect-down-leave-to {
  opacity: 0;
  transform: scaleY(0.92) translateY(-6px);
}

/* 向上展开动画（从底部缩放） */
.aselect-up-enter-active,
.aselect-up-leave-active {
  transition: opacity 0.15s, transform 0.15s;
  transform-origin: bottom center;
}

.aselect-up-enter-from,
.aselect-up-leave-to {
  opacity: 0;
  transform: scaleY(0.92) translateY(6px);
}
</style>

