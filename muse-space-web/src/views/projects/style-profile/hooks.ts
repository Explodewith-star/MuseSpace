import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { getStyleProfile, upsertStyleProfile } from '@/api/styleProfile'
import { useToast } from '@/composables/useToast'
import type { StyleProfileForm } from './types'

export function initStyleProfileState() {
  const route = useRoute()
  const toast = useToast()
  const projectId = route.params.id as string

  const loading = ref(false)
  const saveLoading = ref(false)
  const hasProfile = ref(false)

  const form = reactive<StyleProfileForm>({
    name: '',
    tone: '',
    sentenceLengthPreference: '',
    dialogueRatio: '',
    descriptionDensity: '',
    forbiddenExpressions: '',
    sampleReferenceText: '',
  })

  async function loadProfile(): Promise<void> {
    loading.value = true
    try {
      const profile = await getStyleProfile(projectId)
      hasProfile.value = true
      Object.assign(form, {
        name: profile.name ?? '',
        tone: profile.tone ?? '',
        sentenceLengthPreference: profile.sentenceLengthPreference ?? '',
        dialogueRatio: profile.dialogueRatio ?? '',
        descriptionDensity: profile.descriptionDensity ?? '',
        forbiddenExpressions: profile.forbiddenExpressions ?? '',
        sampleReferenceText: '',
      })
    } catch {
      // 404 = 未配置，silent 模式已抑制 toast
      hasProfile.value = false
    } finally {
      loading.value = false
    }
  }

  async function saveProfile(): Promise<void> {
    if (!form.name.trim()) return
    saveLoading.value = true
    try {
      await upsertStyleProfile(projectId, {
        name: form.name,
        tone: form.tone || undefined,
        sentenceLengthPreference: form.sentenceLengthPreference || undefined,
        dialogueRatio: form.dialogueRatio || undefined,
        descriptionDensity: form.descriptionDensity || undefined,
        forbiddenExpressions: form.forbiddenExpressions || undefined,
        sampleReferenceText: form.sampleReferenceText || undefined,
      })
      hasProfile.value = true
      toast.success('文风配置已保存')
    } catch {
      // handled
    } finally {
      saveLoading.value = false
    }
  }

  onMounted(loadProfile)

  return { loading, saveLoading, hasProfile, form, saveProfile }
}
