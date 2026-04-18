<script setup lang="ts">
import AppThemeToggle from './AppThemeToggle.vue'

interface Props {
  title?: string
}

defineProps<Props>()
</script>

<template>
  <header class="app-header">
    <div class="app-header__left">
      <slot name="left">
        <span class="app-header__logo">
          <i class="i-lucide-feather app-header__logo-icon" />
          <span class="app-header__logo-text">MuseSpace</span>
        </span>
      </slot>
    </div>
    <div class="app-header__center">
      <slot name="center">
        <span v-if="title" class="app-header__title">{{ title }}</span>
      </slot>
    </div>
    <div class="app-header__right">
      <slot name="right" />
      <AppThemeToggle />
    </div>
  </header>
</template>

<style scoped>
.app-header {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 20px;
  border-bottom: 1px solid var(--color-border);
  background-color: var(--color-bg-surface);
  flex-shrink: 0;
}

.app-header__left,
.app-header__right {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 180px;
}

.app-header__right {
  justify-content: flex-end;
}

.app-header__center {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.app-header__logo {
  display: flex;
  align-items: center;
  gap: 8px;
  text-decoration: none;
}

.app-header__logo-icon {
  font-size: 20px;
  color: var(--color-primary);
}

.app-header__logo-text {
  font-size: 17px;
  font-weight: 700;
  color: var(--color-text-primary);
  letter-spacing: -0.3px;
}

.app-header__title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}
</style>
