import request from './http'
import type { OutlineChainResponse, CreateOutlineChainRequest } from '@/types/models'

export function getOutlineChains(projectId: string): Promise<OutlineChainResponse[]> {
  return request.get(`/projects/${projectId}/chains`)
}

export function createOutlineChain(
  projectId: string,
  data: CreateOutlineChainRequest,
): Promise<OutlineChainResponse> {
  return request.post(`/projects/${projectId}/chains`, data)
}

export function deleteOutlineChain(projectId: string, chainId: string): Promise<boolean> {
  return request.delete(`/projects/${projectId}/chains/${chainId}`)
}
