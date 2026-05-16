export interface CreateCharacterForm {
  name: string
  age: string
  /** 身份定位：主角/配角/反派/龙套/其他 */
  role: string
  personalitySummary: string
  motivation: string
  speakingStyle: string
  forbiddenBehaviors: string
  publicSecrets: string
  privateSecrets: string
  currentState: string
  tags: string
}
