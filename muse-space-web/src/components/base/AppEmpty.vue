<script setup lang="ts">
interface Props {
  icon?: string
  title?: string
  description?: string
}

withDefaults(defineProps<Props>(), {
  icon: 'i-lucide-inbox',
  title: '暂无数据',
})
</script>

<template>
  <div class="app-empty">
    <i :class="['app-empty__icon', icon]" />
    <p class="app-empty__title">{{ title }}</p>
    <p v-if="description" class="app-empty__desc">{{ description }}</p>
    <div v-if="$slots.action" class="app-empty__action">
      <slot name="action" />
    </div>
  </div>
</template>

<style scoped>
.app-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
  gap: 8px;
}

.app-empty__icon {
  font-size: 40px;
  color: var(--color-text-muted);
  opacity: 0.5;
  margin-bottom: 4px;
}

.app-empty__title {
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-muted);
  margin: 0;
}

.app-empty__desc {
  font-size: 13px;
  color: var(--color-text-muted);
  opacity: 0.7;
  margin: 0;
}

.app-empty__action {
  margin-top: 8px;
}
</style>
