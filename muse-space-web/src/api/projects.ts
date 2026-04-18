import request from './http'
import type { StoryProjectResponse, CreateStoryProjectRequest } from '@/types/models'

export function getProjects(): Promise<StoryProjectResponse[]> {
  return request.get('/projects')
}

export function getProject(id: string): Promise<StoryProjectResponse> {
  return request.get(`/projects/${id}`)
}

export function createProject(data: CreateStoryProjectRequest): Promise<StoryProjectResponse> {
  return request.post('/projects', data)
}

export function deleteProject(id: string): Promise<void> {
  return request.delete(`/projects/${id}`)
}
