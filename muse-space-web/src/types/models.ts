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
  conflict?: string
  emotionCurve?: string
  keyCharacterIds: string[]
  mustIncludePoints: string[]
  sourceSuggestionId?: string
}

export interface CreateChapterRequest {
  number: number
  title?: string
  summary?: string
  goal?: string
  conflict?: string
  emotionCurve?: string
  keyCharacterIds?: string[]
  mustIncludePoints?: string[]
}

export interface UpdateChapterRequest {
  title?: string
  summary?: string
  goal?: string
  draftText?: string
  finalText?: string
  status?: number
  conflict?: string
  emotionCurve?: string
  keyCharacterIds?: string[]
  mustIncludePoints?: string[]
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

export interface ExtractCharacterResponse {
  name: string
  age?: number
  role?: string
  personalitySummary?: string
  motivation?: string
  speakingStyle?: string
  forbiddenBehaviors?: string
  currentState?: string
  sourceChunkCount: number
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
  sampleReferenceText?: string
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

// ---------- LLM Provider ----------
export type LlmProviderType = 'OpenRouter' | 'DeepSeek' | 'Venice'

export interface LlmModelOption {
  id: string
  label: string
}

export interface LlmProviderStatus {
  active: LlmProviderType
  currentModel: string
  availableModels: LlmModelOption[]
}

// ---------- Novel ----------
export interface NovelResponse {
  id: string
  storyProjectId: string
  title: string
  fileName: string
  fileSize: number
  status: 'Pending' | 'Chunking' | 'Embedding' | 'Indexed' | 'Failed'
  totalChunks: number
  progressDone: number
  progressTotal: number
  lastError?: string | null
  startedAt?: string | null
  finishedAt?: string | null
  createdAt: string
}

// ---------- AgentSuggestion ----------
export type SuggestionStatus = 'Pending' | 'Accepted' | 'Applied' | 'Ignored'

export interface AgentSuggestionResponse {
  id: string
  agentRunId: string
  storyProjectId: string
  category: string
  title: string
  contentJson: string
  status: SuggestionStatus
  targetEntityId?: string
  createdAt: string
  resolvedAt?: string
}

/** ConsistencyCheckJob 写入的 ContentJson 结构 */
export interface ConsistencyContentJson {
  ruleName?: string
  severity?: 'high' | 'medium' | 'low'
  conflictSnippet?: string
  explanation?: string
  suggestion?: string
}

/** CharacterConsistencyCheckJob 写入的 ContentJson 结构 */
export interface CharacterConflictContentJson {
  characterName?: string
  conflictType?: string
  severity?: 'high' | 'medium' | 'low'
  conflictSnippet?: string
  explanation?: string
  suggestion?: string
}

export interface ConsistencyCheckRequest {
  draftText: string
}

export interface BatchResolveSuggestionsRequest {
  ids: string[]
  action: 'Accept' | 'Ignore' | 'QuickApply' | 'ReApply' | 'Delete'
}

// ---------- Outline Plan ----------
export interface OutlinePlanRequest {
  goal: string
  chapterCount: number
  mode: 'new' | 'continue' | 'extra'
}

/** 大纲建议 ContentJson 中的单章结构 */
export interface OutlineChapterItem {
  number: number
  title: string
  goal: string
  summary: string
}

/** 大纲建议 ContentJson 中的卷结构 */
export interface OutlineVolumeItem {
  number: number
  title: string
  theme: string
  chapters: OutlineChapterItem[]
}

/** 大纲建议 ContentJson 的顶层结构（分卷结构） */
export interface OutlinePayload {
  volumes: OutlineVolumeItem[]
}

/** 单卷重做请求 */
export interface RegenerateOutlineVolumeRequest {
  extraInstruction?: string
}

export interface ImportOutlineRequest {
  chapters: ImportOutlineChapter[]
}

export interface ImportOutlineChapter {
  number: number
  title: string
  goal?: string
  summary?: string
}
