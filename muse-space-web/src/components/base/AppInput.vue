<script setup lang="ts">
interface Props {
  modelValue?: string
  placeholder?: string
  disabled?: boolean
  readonly?: boolean
  error?: string
  label?: string
  prefix?: string
}

defineProps<Props>()
defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div class="app-input-wrap">
    <label v-if="label" class="app-input-label">{{ label }}</label>
    <div :class="['app-input-box', { 'app-input-box--error': error }]">
      <span v-if="prefix" class="app-input-prefix">{{ prefix }}</span>
      <input
        class="app-input"
        :value="modelValue"
        :placeholder="placeholder"
        :disabled="disabled"
        :readonly="readonly"
        @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      />
    </div>
    <p v-if="error" class="app-input-error">{{ error }}</p>
  </div>
</template>

<style scoped>
.app-input-wrap {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.app-input-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.app-input-box {
  display: flex;
  align-items: center;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  background-color: var(--color-bg-surface);
  transition: border-color 0.15s;
  overflow: hidden;
}

.app-input-box:focus-within {
  border-color: var(--color-primary);
}

.app-input-box--error {
  border-color: var(--color-danger);
}

.app-input-prefix {
  padding: 0 10px;
  font-size: 13px;
  color: var(--color-text-muted);
  background-color: var(--color-bg-elevated);
  border-right: 1px solid var(--color-border);
  height: 100%;
  display: flex;
  align-items: center;
  white-space: nowrap;
}

.app-input {
  flex: 1;
  padding: 8px 12px;
  font-size: 14px;
  color: var(--color-text-primary);
  background: transparent;
  border: none;
  outline: none;
  width: 100%;
}

.app-input::placeholder {
  color: var(--color-text-muted);
}

.app-input:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.app-input-error {
  font-size: 12px;
  color: var(--color-danger);
  margin: 0;
}
</style>
