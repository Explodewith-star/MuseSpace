import request from './http'

export type PlotThreadStatus = 'Introduced' | 'Active' | 'PaidOff' | 'Abandoned'

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
  tags?: string
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
  tags?: string
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
