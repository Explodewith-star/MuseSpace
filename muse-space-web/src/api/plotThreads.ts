import request from './http'

export type PlotThreadStatus = 'Introduced' | 'Active' | 'PaidOff' | 'Abandoned'
export type PlotThreadVisibility = 'ThisOutline' | 'Chain' | 'Project'

export interface PlotThreadResponse {
  id: string
  storyProjectId: string
  title: string
  description?: string
  importance?: string
  status: PlotThreadStatus
  plantedInChapterId?: string
  resolvedInChapterId?: string
  relatedCharacterIds?: string[]
  /** 预期回收于第几章。当当前最新章号 > 本值且状态为 Introduced/Active 时视为过期。 */
  expectedResolveByChapterNumber?: number
  tags?: string
  outlineId?: string
  chainId?: string
  visibility: PlotThreadVisibility
  createdAt: string
  updatedAt: string
}

export interface UpsertPlotThreadRequest {
  title: string
  description?: string
  importance?: string
  status?: PlotThreadStatus
  plantedInChapterId?: string
  resolvedInChapterId?: string
  relatedCharacterIds?: string[]
  expectedResolveByChapterNumber?: number
  tags?: string
  /** 可见性作用域：ThisOutline（番外局部）/ Chain（同链追踪，默认）/ Project（全书谜题）。 */
  visibility?: PlotThreadVisibility
}

export function getPlotThreads(projectId: string): Promise<PlotThreadResponse[]> {
  return request.get(`/projects/${projectId}/plot-threads`)
}

export function createPlotThread(
  projectId: string,
  data: UpsertPlotThreadRequest,
): Promise<PlotThreadResponse> {
  return request.post(`/projects/${projectId}/plot-threads`, data)
}

export function updatePlotThread(
  projectId: string,
  id: string,
  data: UpsertPlotThreadRequest,
): Promise<PlotThreadResponse> {
  return request.put(`/projects/${projectId}/plot-threads/${id}`, data)
}

export function deletePlotThread(projectId: string, id: string): Promise<void> {
  return request.delete(`/projects/${projectId}/plot-threads/${id}`)
}
