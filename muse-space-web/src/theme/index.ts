import { lightTokens, darkTokens } from './tokens'

export type ThemeMode = 'light' | 'dark'

const STORAGE_KEY = 'muse-theme'

export function applyTheme(mode: ThemeMode): void {
  const tokens = mode === 'dark' ? darkTokens : lightTokens
  const root = document.documentElement

  for (const [key, value] of Object.entries(tokens)) {
    root.style.setProperty(key, value)
  }

  if (mode === 'dark') {
    root.classList.add('dark')
  } else {
    root.classList.remove('dark')
  }
}

export function getSavedTheme(): ThemeMode {
  const saved = localStorage.getItem(STORAGE_KEY)
  if (saved === 'dark' || saved === 'light') return saved
  return 'light'
}

export function saveTheme(mode: ThemeMode): void {
  localStorage.setItem(STORAGE_KEY, mode)
}

export function initTheme(): ThemeMode {
  const mode = getSavedTheme()
  applyTheme(mode)
  return mode
}
