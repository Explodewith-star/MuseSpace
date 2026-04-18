import { ref } from 'vue'
import { applyTheme, saveTheme, getSavedTheme } from '@/theme'
import type { ThemeMode } from '@/theme'

const currentTheme = ref<ThemeMode>(getSavedTheme())

export function useTheme() {
  function toggle(): void {
    const next: ThemeMode = currentTheme.value === 'light' ? 'dark' : 'light'
    currentTheme.value = next
    applyTheme(next)
    saveTheme(next)
  }

  function setTheme(mode: ThemeMode): void {
    currentTheme.value = mode
    applyTheme(mode)
    saveTheme(mode)
  }

  return {
    currentTheme,
    isDark: () => currentTheme.value === 'dark',
    toggle,
    setTheme,
  }
}
