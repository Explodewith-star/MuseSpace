import request from './http'
import type { CharacterResponse, CreateCharacterRequest, UpdateCharacterRequest, ExtractCharacterResponse } from '@/types/models'

export function getCharacters(projectId: string, outlineId: string): Promise<CharacterResponse[]> {
  return request.get(`/projects/${projectId}/outlines/${outlineId}/characters`)
}

export function createCharacter(
  projectId: string,
  outlineId: string,
  data: CreateCharacterRequest,
): Promise<CharacterResponse> {
  return request.post(`/projects/${projectId}/outlines/${outlineId}/characters`, data)
}

export function updateCharacter(
  projectId: string,
  outlineId: string,
  characterId: string,
  data: UpdateCharacterRequest,
): Promise<CharacterResponse> {
  return request.put(`/projects/${projectId}/outlines/${outlineId}/characters/${characterId}`, data)
}

export function deleteCharacter(projectId: string, outlineId: string, characterId: string): Promise<void> {
  return request.delete(`/projects/${projectId}/outlines/${outlineId}/characters/${characterId}`)
}

export function generateCharacter(
  projectId: string,
  outlineId: string,
  description: string,
  fromNovel: boolean = false,
): Promise<ExtractCharacterResponse> {
  return request.post(`/projects/${projectId}/outlines/${outlineId}/characters/generate`, { description, fromNovel })
}

/** 从其他大纲复制角色到当前大纲 */
export function copyCharactersToOutline(
  projectId: string,
  outlineId: string,
  characterIds: string[],
): Promise<CharacterResponse[]> {
  return request.post(`/projects/${projectId}/outlines/${outlineId}/characters/copy`, { characterIds, targetOutlineId: outlineId })
}

// ── 原著角色池 ──────────────────────────────────────────────────────────────

/** 获取项目原著角色池（StoryOutlineId 为 null 的角色） */
export function getCharacterPool(projectId: string): Promise<CharacterResponse[]> {
  return request.get(`/projects/${projectId}/character-pool`)
}

/** 将原著角色池中的角色引入到指定大纲（隔离复制） */
export function importFromPool(
  projectId: string,
  outlineId: string,
  characterIds: string[],
): Promise<CharacterResponse[]> {
  return request.post(`/projects/${projectId}/character-pool/import-to-outline/${outlineId}`, { characterIds })
}

/** 在项目角色池中直接新建角色 */
export function createInPool(projectId: string, data: CreateCharacterRequest): Promise<CharacterResponse> {
  return request.post(`/projects/${projectId}/character-pool`, data)
}

/** 批量删除项目角色池中的角色 */
export function deleteFromPool(projectId: string, characterIds: string[]): Promise<void> {
  return request.delete(`/projects/${projectId}/character-pool`, { data: { characterIds } })
}

// ── 全局角色池 ──────────────────────────────────────────────────────────────

/** 获取全局角色池（当前用户所有项目的池角色） */
export function getGlobalCharacterPool(): Promise<CharacterResponse[]> {
  return request.get('/character-pool')
}

/** 将全局池中的角色批量复制到指定项目的池 */
export function copyPoolCharactersToProject(
  targetProjectId: string,
  characterIds: string[],
): Promise<CharacterResponse[]> {
  return request.post(`/projects/${targetProjectId}/character-pool/copy-from-global`, { characterIds })
}
