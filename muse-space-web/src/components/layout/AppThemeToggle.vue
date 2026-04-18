<script setup lang="ts">
import { useTheme } from '@/composables/useTheme'

const { currentTheme, toggle } = useTheme()
</script>

<template>
  <button class="theme-toggle" :title="currentTheme === 'light' ? '切换暗色' : '切换亮色'" @click="toggle">
    <i v-if="currentTheme === 'light'" class="i-lucide-moon" />
    <i v-else class="i-lucide-sun" />
  </button>
</template>

<style scoped>
.theme-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 16px;
  transition:
    background-color 0.15s,
    color 0.15s;
}

.theme-toggle:hover {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
}
</style>
