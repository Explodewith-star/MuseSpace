<script setup lang="ts">
import { ref, onMounted } from 'vue'
import AppToast from '@/components/base/AppToast.vue'
import AppChangelog from '@/components/base/AppChangelog.vue'
import TaskPanel from '@/components/base/TaskPanel.vue'
import { changelog, CHANGELOG_VERSION } from '@/config/changelog'

const STORAGE_KEY = 'muse-seen-version'
const showChangelog = ref(false)

onMounted(() => {
  const seen = localStorage.getItem(STORAGE_KEY)
  if (seen !== CHANGELOG_VERSION) {
    showChangelog.value = true
  }
})

function onChangelogConfirm(visible: boolean) {
  showChangelog.value = visible
  if (!visible) {
    localStorage.setItem(STORAGE_KEY, CHANGELOG_VERSION)
  }
}
</script>

<template>
  <router-view />
  <AppToast />
  <TaskPanel />
  <AppChangelog
    :model-value="showChangelog"
    :changelog="changelog"
    @update:model-value="onChangelogConfirm"
  />
</template>
