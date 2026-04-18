<script setup lang="ts">
interface Props {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  size?: 'sm' | 'md' | 'lg'
  disabled?: boolean
  loading?: boolean
  type?: 'button' | 'submit' | 'reset'
}

withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
  disabled: false,
  loading: false,
  type: 'button',
})
</script>

<template>
  <button
    :type="type"
    :disabled="disabled || loading"
    :class="[
      'app-btn',
      `app-btn--${variant}`,
      `app-btn--${size}`,
      { 'app-btn--loading': loading },
    ]"
  >
    <span v-if="loading" class="app-btn__spinner i-lucide-loader-circle" />
    <slot />
  </button>
</template>

<style scoped>
.app-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  border: 1px solid transparent;
  border-radius: 8px;
  font-weight: 500;
  cursor: pointer;
  transition:
    background-color 0.15s,
    border-color 0.15s,
    color 0.15s,
    opacity 0.15s;
  white-space: nowrap;
  outline: none;
}

.app-btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

/* Size */
.app-btn--sm {
  padding: 4px 12px;
  font-size: 12px;
  height: 28px;
}
.app-btn--md {
  padding: 6px 16px;
  font-size: 14px;
  height: 36px;
}
.app-btn--lg {
  padding: 8px 22px;
  font-size: 15px;
  height: 44px;
}

/* Variants */
.app-btn--primary {
  background-color: var(--color-primary);
  color: #fff;
}
.app-btn--primary:not(:disabled):hover {
  background-color: var(--color-primary-hover);
}

.app-btn--secondary {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
  border-color: var(--color-border);
}
.app-btn--secondary:not(:disabled):hover {
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.app-btn--ghost {
  background-color: transparent;
  color: var(--color-text-primary);
}
.app-btn--ghost:not(:disabled):hover {
  background-color: var(--color-bg-elevated);
}

.app-btn--danger {
  background-color: var(--color-danger);
  color: #fff;
}
.app-btn--danger:not(:disabled):hover {
  background-color: var(--color-danger-hover);
}

/* Spinner */
.app-btn__spinner {
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}
</style>
