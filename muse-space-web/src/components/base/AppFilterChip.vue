<script setup lang="ts">
interface Props {
  active?: boolean
  disabled?: boolean
  count?: number
  icon?: string
}

withDefaults(defineProps<Props>(), {
  active: false,
  disabled: false,
  count: undefined,
  icon: undefined,
})

defineEmits<{ click: [] }>()
</script>

<template>
  <button
    type="button"
    :class="['app-filter-chip', { 'is-active': active }]"
    :disabled="disabled"
    @click="$emit('click')"
  >
    <i v-if="icon" :class="['app-filter-chip__icon', icon]" />
    <span class="app-filter-chip__label"><slot /></span>
    <span v-if="count !== undefined" class="app-filter-chip__count">{{ count }}</span>
  </button>
</template>

<style scoped>
.app-filter-chip {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 30px;
  padding: 5px 11px;
  border: 1px solid var(--color-border);
  border-radius: 999px;
  background: var(--color-bg-surface);
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition:
    background-color 0.15s,
    border-color 0.15s,
    color 0.15s,
    box-shadow 0.15s;
}

.app-filter-chip:hover:not(:disabled) {
  border-color: color-mix(in srgb, var(--color-primary) 35%, var(--color-border));
  color: var(--color-text-primary);
  background: var(--color-bg-elevated);
}

.app-filter-chip.is-active {
  border-color: color-mix(in srgb, var(--color-primary) 48%, var(--color-border));
  background: color-mix(in srgb, var(--color-primary) 10%, var(--color-bg-surface));
  color: var(--color-primary);
  box-shadow: 0 1px 0 color-mix(in srgb, var(--color-primary) 18%, transparent);
}

.app-filter-chip:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.app-filter-chip__icon {
  font-size: 14px;
  flex-shrink: 0;
}

.app-filter-chip__label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

.app-filter-chip__count {
  min-width: 18px;
  padding: 1px 6px;
  border-radius: 999px;
  background: color-mix(in srgb, currentColor 12%, transparent);
  font-size: 11px;
  line-height: 1.4;
}
</style>
