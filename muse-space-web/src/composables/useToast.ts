import { ref, readonly } from 'vue'

export interface ToastItem {
  id: number
  message: string
  type: 'success' | 'error' | 'info' | 'warning'
  duration: number
}

const toasts = ref<ToastItem[]>([])
let counter = 0

function add(message: string, type: ToastItem['type'], duration = 3000): void {
  const id = ++counter
  toasts.value.push({ id, message, type, duration })
  setTimeout(() => remove(id), duration)
}

function remove(id: number): void {
  const index = toasts.value.findIndex((t) => t.id === id)
  if (index !== -1) toasts.value.splice(index, 1)
}

export function useToast() {
  return {
    toasts: readonly(toasts),
    success: (msg: string, duration?: number) => add(msg, 'success', duration),
    error: (msg: string, duration?: number) => add(msg, 'error', duration),
    info: (msg: string, duration?: number) => add(msg, 'info', duration),
    warning: (msg: string, duration?: number) => add(msg, 'warning', duration),
    remove,
  }
}
