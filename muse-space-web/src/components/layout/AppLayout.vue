<script setup lang="ts">
import AppHeader from './AppHeader.vue'

interface Props {
  showSidebar?: boolean
  title?: string
}

withDefaults(defineProps<Props>(), {
  showSidebar: false,
})
</script>

<template>
  <div class="app-layout">
    <AppHeader :title="title">
      <template v-if="$slots['header-left']" #left>
        <slot name="header-left" />
      </template>
      <template v-if="$slots['header-center']" #center>
        <slot name="header-center" />
      </template>
      <template v-if="$slots['header-right']" #right>
        <slot name="header-right" />
      </template>
    </AppHeader>

    <div class="app-layout__body">
      <slot name="sidebar" />
      <main class="app-layout__main">
        <slot />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-layout {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background-color: var(--color-bg-base);
  overflow: hidden;
}

.app-layout__body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.app-layout__main {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
}
</style>
