<script setup lang="ts">
interface Props {
  width?: string
  height?: string
  rounded?: boolean
}

withDefaults(defineProps<Props>(), {
  width: '100%',
  height: '16px',
  rounded: true,
})
</script>

<template>
  <div
    class="app-skeleton"
    :style="{ width, height, borderRadius: rounded ? '6px' : '0' }"
  />
</template>

<style scoped>
.app-skeleton {
  background: linear-gradient(
    90deg,
    var(--color-bg-elevated) 25%,
    var(--color-border) 50%,
    var(--color-bg-elevated) 75%
  );
  background-size: 200% 100%;
  animation: skeleton-shimmer 1.4s infinite;
}

@keyframes skeleton-shimmer {
  from {
    background-position: 200% 0;
  }
  to {
    background-position: -200% 0;
  }
}
</style>
