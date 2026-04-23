<script setup lang="ts">
interface Option {
  value: string
  label: string
}

interface Props {
  modelValue?: string
  options: Option[]
  label?: string
  placeholder?: string
  disabled?: boolean
}

defineProps<Props>()
defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div class="app-select-wrap">
    <label v-if="label" class="app-select-label">{{ label }}</label>
    <div class="app-select-box">
      <select
        class="app-select"
        :value="modelValue"
        :disabled="disabled"
        @change="$emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
      >
        <option v-if="placeholder && !modelValue" value="" disabled selected>{{ placeholder }}</option>
        <option v-for="opt in options" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </option>
      </select>
      <i class="i-lucide-chevron-down app-select-arrow" />
    </div>
  </div>
</template>

<style scoped>
.app-select-wrap {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.app-select-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-primary);
}

.app-select-box {
  position: relative;
  display: flex;
  align-items: center;
}

.app-select {
  width: 100%;
  appearance: none;
  padding: 7px 32px 7px 10px;
  font-size: 13px;
  color: var(--color-text-primary);
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  cursor: pointer;
  transition: border-color 0.15s;
  outline: none;
}

.app-select:focus {
  border-color: var(--color-primary);
}

.app-select:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.app-select-arrow {
  position: absolute;
  right: 8px;
  font-size: 14px;
  color: var(--color-text-muted);
  pointer-events: none;
}
</style>
