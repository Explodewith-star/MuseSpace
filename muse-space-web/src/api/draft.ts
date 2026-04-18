import request from './http'
import type { GenerateSceneDraftRequest, GenerateSceneDraftResponse } from '@/types/models'

export function generateSceneDraft(
  data: GenerateSceneDraftRequest,
): Promise<GenerateSceneDraftResponse> {
  return request.post('/draft/scene', data)
}
