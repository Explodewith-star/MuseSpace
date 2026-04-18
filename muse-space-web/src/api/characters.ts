import request from './http'
import type { CharacterResponse, CreateCharacterRequest, UpdateCharacterRequest } from '@/types/models'

export function getCharacters(projectId: string): Promise<CharacterResponse[]> {
  return request.get(`/projects/${projectId}/characters`)
}

export function createCharacter(
  projectId: string,
  data: CreateCharacterRequest,
): Promise<CharacterResponse> {
  return request.post(`/projects/${projectId}/characters`, data)
}

export function updateCharacter(
  projectId: string,
  characterId: string,
  data: UpdateCharacterRequest,
): Promise<CharacterResponse> {
  return request.put(`/projects/${projectId}/characters/${characterId}`, data)
}

export function deleteCharacter(projectId: string, characterId: string): Promise<void> {
  return request.delete(`/projects/${projectId}/characters/${characterId}`)
}
