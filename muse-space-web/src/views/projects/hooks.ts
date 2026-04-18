import { onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useProjectStore } from '@/store/modules/project'

export function initProjectWorkspace() {
  const route = useRoute()
  const router = useRouter()
  const projectStore = useProjectStore()

  function getProjectId(): string {
    return route.params.id as string
  }

  onMounted(() => projectStore.load(getProjectId()))
  watch(
    () => route.params.id,
    (id) => {
      if (id) projectStore.load(id as string)
    },
  )

  function goBack(): void {
    router.push('/projects')
  }

  return { projectStore, goBack, getProjectId }
}
