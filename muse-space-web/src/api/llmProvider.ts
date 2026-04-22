import request from './http'
import type { LlmProviderStatus } from '@/types/models'

export function getLlmProvider(): Promise<LlmProviderStatus> {
  return request.get('/llm-provider')
}

export function setLlmProvider(provider: string): Promise<LlmProviderStatus> {
  return request.put('/llm-provider', { provider })
}
