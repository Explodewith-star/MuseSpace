<script setup lang="ts">
interface Props {
  id?: string
  name?: string
  modelValue?: boolean
  checked?: boolean
  label?: string
  hint?: string
  disabled?: boolean
  size?: 'sm' | 'md'
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: undefined,
  checked: undefined,
  disabled: false,
  size: 'md',
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  change: [value: boolean]
}>()

const generatedId = `app-checkbox-${Math.random().toString(36).slice(2, 10)}`

function isChecked(): boolean {
  return props.modelValue ?? props.checked ?? false
}

function onChange(event: Event) {
  const value = (event.target as HTMLInputElement).checked
  emit('update:modelValue', value)
  emit('change', value)
}
</script>

<template>
  <label :class="['app-checkbox', `app-checkbox--${size}`, { 'is-disabled': disabled }]">
    <input
      :id="id ?? generatedId"
      :name="name ?? id ?? generatedId"
      class="app-checkbox__native"
      type="checkbox"
      :checked="isChecked()"
      :disabled="disabled"
      @change="onChange"
    />
    <span class="app-checkbox__box" />
    <span v-if="label || hint || $slots.default" class="app-checkbox__content">
      <span class="app-checkbox__label">
        <slot>{{ label }}</slot>
      </span>
      <span v-if="hint" class="app-checkbox__hint">{{ hint }}</span>
    </span>
  </label>
</template>

<style scoped>
.app-checkbox {
  display: inline-flex;
  align-items: flex-start;
  gap: 8px;
  color: var(--color-text-primary);
  cursor: pointer;
  user-select: none;
}

.app-checkbox--sm {
  gap: 6px;
  font-size: 12px;
}

.app-checkbox--md {
  font-size: 13px;
}

.app-checkbox.is-disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.app-checkbox__native {
  position: absolute;
  opacity: 0;
  width: 1px;
  height: 1px;
  pointer-events: none;
}

.app-checkbox__box {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 16px;
  height: 16px;
  margin-top: 1px;
  border-radius: 5px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-surface);
  color: #fff;
  transition:
    background-color 0.15s,
    border-color 0.15s,
    box-shadow 0.15s;
}

.app-checkbox--sm .app-checkbox__box {
  width: 14px;
  height: 14px;
  border-radius: 4px;
}

.app-checkbox__native:checked + .app-checkbox__box {
  border-color: var(--color-primary);
  background: var(--color-primary);
}

.app-checkbox__box::after {
  width: 4px;
  height: 8px;
  border: solid #fff;
  border-width: 0 2px 2px 0;
  content: '';
  opacity: 0;
  transform: rotate(45deg) scale(0.7);
  transition:
    opacity 0.12s,
    transform 0.12s;
}

.app-checkbox__native:checked + .app-checkbox__box::after {
  opacity: 1;
  transform: rotate(45deg) scale(1);
}

.app-checkbox__native:focus-visible + .app-checkbox__box {
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-primary) 18%, transparent);
}

.app-checkbox:not(.is-disabled):hover .app-checkbox__box {
  border-color: var(--color-primary);
}

.app-checkbox__content {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  line-height: 1.35;
}

.app-checkbox__label {
  color: var(--color-text-primary);
}

.app-checkbox__hint {
  font-size: 12px;
  color: var(--color-text-muted);
}
</style>
