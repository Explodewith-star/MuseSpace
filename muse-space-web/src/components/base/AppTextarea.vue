<script setup lang="ts">
interface Props {
  modelValue?: string
  placeholder?: string
  disabled?: boolean
  readonly?: boolean
  error?: string
  label?: string
  rows?: number
  resize?: 'none' | 'vertical' | 'horizontal' | 'both'
}

withDefaults(defineProps<Props>(), {
  rows: 4,
  resize: 'vertical',
})

defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div class="app-textarea-wrap">
    <label v-if="label" class="app-textarea-label">{{ label }}</label>
    <textarea
      :class="['app-textarea', { 'app-textarea--error': error }]"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :readonly="readonly"
      :rows="rows"
      :style="{ resize }"
      @input="$emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
    />
    <p v-if="error" class="app-textarea-error">{{ error }}</p>
  </div>
</template>

<style scoped>
.app-textarea-wrap {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.app-textarea-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.app-textarea {
  padding: 10px 12px;
  font-size: 14px;
  color: var(--color-text-primary);
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  outline: none;
  font-family: inherit;
  line-height: 1.6;
  transition: border-color 0.15s;
  width: 100%;
  box-sizing: border-box;
}

.app-textarea:focus {
  border-color: var(--color-primary);
}

.app-textarea--error {
  border-color: var(--color-danger);
}

.app-textarea::placeholder {
  color: var(--color-text-muted);
}

.app-textarea:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.app-textarea-error {
  font-size: 12px;
  color: var(--color-danger);
  margin: 0;
}
</style>
