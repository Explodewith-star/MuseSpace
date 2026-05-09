import request from './http'
import type {
  CreateStoryOutlineRequest,
  StoryOutlineResponse,
  UpdateStoryOutlineRequest,
} from '@/types/models'

export function getStoryOutlines(projectId: string): Promise<StoryOutlineResponse[]> {
  return request.get(`/projects/${projectId}/outlines`)
}

export function getStoryOutline(
  projectId: string,
  outlineId: string,
): Promise<StoryOutlineResponse> {
  return request.get(`/projects/${projectId}/outlines/${outlineId}`)
}

export function createStoryOutline(
  projectId: string,
  data: CreateStoryOutlineRequest,
): Promise<StoryOutlineResponse> {
  return request.post(`/projects/${projectId}/outlines`, data)
}

export function updateStoryOutline(
  projectId: string,
  outlineId: string,
  data: UpdateStoryOutlineRequest,
): Promise<StoryOutlineResponse> {
  return request.patch(`/projects/${projectId}/outlines/${outlineId}`, data)
}

export function deleteStoryOutline(projectId: string, outlineId: string): Promise<boolean> {
  return request.delete(`/projects/${projectId}/outlines/${outlineId}`)
}
