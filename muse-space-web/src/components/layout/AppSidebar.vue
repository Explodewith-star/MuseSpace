<script setup lang="ts">
import { ref } from 'vue'

interface NavItem {
  label: string
  icon: string
  to: string
}

interface Props {
  items?: NavItem[]
  collapsed?: boolean
}

withDefaults(defineProps<Props>(), {
  items: () => [],
  collapsed: false,
})

const emit = defineEmits<{ 'update:collapsed': [value: boolean] }>()

const isCollapsed = ref(false)

function toggle() {
  isCollapsed.value = !isCollapsed.value
  emit('update:collapsed', isCollapsed.value)
}
</script>

<template>
  <aside :class="['app-sidebar', { 'app-sidebar--collapsed': isCollapsed }]">
    <nav class="app-sidebar__nav">
      <RouterLink
        v-for="item in items"
        :key="item.to"
        :to="item.to"
        class="app-sidebar__item"
        active-class="app-sidebar__item--active"
      >
        <i :class="['app-sidebar__icon', item.icon]" />
        <span v-if="!isCollapsed" class="app-sidebar__label">{{ item.label }}</span>
      </RouterLink>
    </nav>
    <button class="app-sidebar__collapse-btn" @click="toggle">
      <i :class="isCollapsed ? 'i-lucide-panel-left-open' : 'i-lucide-panel-left-close'" />
    </button>
  </aside>
</template>

<style scoped>
.app-sidebar {
  width: 220px;
  background-color: var(--color-bg-surface);
  border-right: 1px solid var(--color-border);
  display: flex;
  flex-direction: column;
  padding: 12px 8px;
  transition: width 0.2s ease;
  flex-shrink: 0;
}

.app-sidebar--collapsed {
  width: 56px;
}

.app-sidebar__nav {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.app-sidebar__item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 8px;
  text-decoration: none;
  color: var(--color-text-muted);
  font-size: 14px;
  font-weight: 500;
  transition:
    background-color 0.15s,
    color 0.15s;
  white-space: nowrap;
  overflow: hidden;
}

.app-sidebar__item:hover {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
}

.app-sidebar__item--active {
  background-color: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
}

.app-sidebar__icon {
  font-size: 17px;
  flex-shrink: 0;
}

.app-sidebar__label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
}

.app-sidebar__collapse-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 8px;
  border-radius: 8px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 17px;
  transition:
    background-color 0.15s,
    color 0.15s;
  align-self: flex-start;
}

.app-sidebar__collapse-btn:hover {
  background-color: var(--color-bg-elevated);
  color: var(--color-text-primary);
}
</style>
