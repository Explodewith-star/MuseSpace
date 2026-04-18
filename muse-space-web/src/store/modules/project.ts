import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { StoryProjectResponse } from '@/types/models'
import { getProject } from '@/api/projects'

export const useProjectStore = defineStore('project', () => {
  const current = ref<StoryProjectResponse | null>(null)
  const loading = ref(false)

  async function load(id: string): Promise<void> {
    if (current.value?.id === id) return
    loading.value = true
    try {
      current.value = await getProject(id)
    } catch {
      // error toast shown by http layer
    } finally {
      loading.value = false
    }
  }

  function clear(): void {
    current.value = null
  }

  return { current, loading, load, clear }
})
