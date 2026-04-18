import { ref } from 'vue'

export interface RequestState<T> {
  data: ReturnType<typeof ref<T | null>>
  loading: ReturnType<typeof ref<boolean>>
  error: ReturnType<typeof ref<string | null>>
  execute: (...args: never[]) => Promise<void>
}

export function useRequest<T, Args extends unknown[]>(
  fn: (...args: Args) => Promise<T>,
  options?: { immediate?: boolean; initialArgs?: Args },
) {
  const data = ref<T | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function execute(...args: Args): Promise<void> {
    loading.value = true
    error.value = null
    try {
      data.value = await fn(...args)
    } catch (e) {
      error.value = e instanceof Error ? e.message : '未知错误'
    } finally {
      loading.value = false
    }
  }

  if (options?.immediate) {
    execute(...((options.initialArgs ?? []) as Args))
  }

  return { data, loading, error, execute }
}
