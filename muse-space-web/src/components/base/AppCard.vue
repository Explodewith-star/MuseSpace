<script setup lang="ts">
interface Props {
  padding?: boolean
  hoverable?: boolean
}

withDefaults(defineProps<Props>(), {
  padding: true,
  hoverable: false,
})
</script>

<template>
  <div :class="['app-card', { 'app-card--padded': padding, 'app-card--hoverable': hoverable }]">
    <slot />
  </div>
</template>

<style scoped>
.app-card {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  transition:
    border-color 0.15s,
    box-shadow 0.15s;
}

.app-card--padded {
  padding: 20px;
}

.app-card--hoverable:hover {
  border-color: var(--color-primary);
  box-shadow: 0 4px 16px rgba(108, 92, 231, 0.1);
}
</style>
