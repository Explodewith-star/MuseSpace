// 对齐后端 MuseSpace.Contracts 层所有 DTO

// ---------- StoryProject ----------
export interface StoryProjectResponse {
  id: string
  name: string
  description?: string
  genre?: string
  narrativePerspective?: string
  createdAt: string
}

export interface CreateStoryProjectRequest {
  name: string
  description?: string
  genre?: string
  narrativePerspective?: string
}

// ---------- Chapter ----------
export interface ChapterResponse {
  id: string
  storyProjectId: string
  number: number
  title?: string
  summary?: string
  goal?: string
  status: number
  draftText?: string
  finalText?: string
}

export interface CreateChapterRequest {
  number: number
  title?: string
  summary?: string
  goal?: string
}

export interface UpdateChapterRequest {
  title?: string
  summary?: string
  goal?: string
  draftText?: string
  finalText?: string
  status?: number
}

// ---------- Character ----------
export interface CharacterResponse {
  id: string
  storyProjectId: string
  name: string
  age?: number
  role?: string
  personalitySummary?: string
  motivation?: string
  speakingStyle?: string
  forbiddenBehaviors?: string
  currentState?: string
  tags?: string
}

export interface CreateCharacterRequest {
  name: string
  age?: number
  role?: string
  personalitySummary?: string
  motivation?: string
  speakingStyle?: string
  forbiddenBehaviors?: string
  publicSecrets?: string
  privateSecrets?: string
  currentState?: string
  tags?: string
}

export interface UpdateCharacterRequest {
  name?: string
  age?: number
  role?: string
  personalitySummary?: string
  motivation?: string
  speakingStyle?: string
  forbiddenBehaviors?: string
  publicSecrets?: string
  privateSecrets?: string
  currentState?: string
  tags?: string
}

// ---------- WorldRule ----------
export interface WorldRuleResponse {
  id: string
  storyProjectId: string
  title: string
  description?: string
  category?: string
  priority: number
  isHardConstraint: boolean
}

export interface CreateWorldRuleRequest {
  title: string
  description?: string
  category?: string
  priority?: number
  isHardConstraint?: boolean
}

export interface UpdateWorldRuleRequest {
  title?: string
  description?: string
  category?: string
  priority?: number
  isHardConstraint?: boolean
}

// ---------- StyleProfile ----------
export interface StyleProfileResponse {
  id: string
  storyProjectId: string
  name: string
  tone?: string
  sentenceLengthPreference?: string
  dialogueRatio?: string
  descriptionDensity?: string
  forbiddenExpressions?: string
}

export interface UpsertStyleProfileRequest {
  name: string
  tone?: string
  sentenceLengthPreference?: string
  dialogueRatio?: string
  descriptionDensity?: string
  forbiddenExpressions?: string
  sampleReferenceText?: string
}

// ---------- Draft ----------
export interface GenerateSceneDraftRequest {
  storyProjectId: string
  sceneGoal: string
  conflict?: string
  emotionCurve?: string
}

export interface GenerateSceneDraftResponse {
  requestId: string
  generatedText: string
  skillName?: string
  promptVersion?: string
  durationMs: number
}

// ---------- Chat ----------
export interface ChatRequest {
  question: string
  systemPrompt?: string
}

export interface ChatResponse {
  answer: string
  durationMs: number
}
