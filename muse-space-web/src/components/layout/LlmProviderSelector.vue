<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { getLlmProvider, setLlmProvider } from '@/api/llmProvider'
import type { LlmProviderType } from '@/types/models'

const PROVIDERS: { value: LlmProviderType; label: string }[] = [
  { value: 'OpenRouter', label: 'OpenRouter' },
  { value: 'DeepSeek', label: 'DeepSeek' },
]

const active = ref<LlmProviderType>('OpenRouter')
const loading = ref(false)

onMounted(async () => {
  try {
    const status = await getLlmProvider()
    active.value = status.active
  } catch {
    // 静默失败
  }
})

async function onChange(e: Event) {
  const value = (e.target as HTMLSelectElement).value as LlmProviderType
  if (value === active.value || loading.value) return
  loading.value = true
  try {
    const status = await setLlmProvider(value)
    active.value = status.active
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="llm-selector">
    <i class="i-lucide-cpu llm-selector__icon" />
    <select
      class="llm-selector__select"
      :value="active"
      :disabled="loading"
      @change="onChange"
    >
      <option v-for="p in PROVIDERS" :key="p.value" :value="p.value">
        {{ p.label }}
      </option>
    </select>
  </div>
</template>

<style scoped>
.llm-selector {
  display: flex;
  align-items: center;
  gap: 6px;
  height: 34px;
  padding: 0 8px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-muted);
  font-size: 13px;
  transition: background-color 0.15s;
}
.llm-selector:focus-within {
  background-color: var(--color-bg-hover);
  color: var(--color-text);
}
.llm-selector__icon {
  font-size: 14px;
  flex-shrink: 0;
}
.llm-selector__select {
  background: transparent;
  border: none;
  outline: none;
  font-size: 12px;
  font-weight: 500;
  color: inherit;
  cursor: pointer;
  padding: 0;
}
.llm-selector__select:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
