import { defineStore } from 'pinia'
import { ref } from 'vue'
import { applyTheme, saveTheme, getSavedTheme } from '@/theme'
import type { ThemeMode } from '@/theme'

export const useThemeStore = defineStore('theme', () => {
  const mode = ref<ThemeMode>(getSavedTheme())

  function toggle() {
    const next: ThemeMode = mode.value === 'light' ? 'dark' : 'light'
    mode.value = next
    applyTheme(next)
    saveTheme(next)
  }

  function set(newMode: ThemeMode) {
    mode.value = newMode
    applyTheme(newMode)
    saveTheme(newMode)
  }

  return { mode, toggle, set }
})
