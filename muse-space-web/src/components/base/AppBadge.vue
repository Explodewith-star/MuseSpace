<script setup lang="ts">
interface Props {
  variant?: 'default' | 'primary' | 'accent' | 'success' | 'danger' | 'muted'
  size?: 'sm' | 'md'
}

withDefaults(defineProps<Props>(), {
  variant: 'default',
  size: 'md',
})
</script>

<template>
  <span :class="['app-badge', `app-badge--${variant}`, `app-badge--${size}`]">
    <slot />
  </span>
</template>

<style scoped>
.app-badge {
  display: inline-flex;
  align-items: center;
  border-radius: 99px;
  font-weight: 500;
  white-space: nowrap;
}

.app-badge--sm {
  padding: 1px 8px;
  font-size: 11px;
}

.app-badge--md {
  padding: 2px 10px;
  font-size: 12px;
}

.app-badge--default {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-muted);
}

.app-badge--primary {
  background-color: color-mix(in srgb, var(--color-primary) 15%, transparent);
  color: var(--color-primary);
}

.app-badge--accent {
  background-color: color-mix(in srgb, var(--color-accent) 15%, transparent);
  color: var(--color-accent);
}

.app-badge--success {
  background-color: color-mix(in srgb, var(--color-success) 15%, transparent);
  color: var(--color-success);
}

.app-badge--danger {
  background-color: color-mix(in srgb, var(--color-danger) 15%, transparent);
  color: var(--color-danger);
}

.app-badge--muted {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-muted);
}
</style>
