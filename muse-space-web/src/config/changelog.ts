/**
 * 更新日志配置
 *
 * 使用规范：
 *   - 每次需要告知用户新功能/变更时，修改下方 items 内容
 *   - 版本号由构建系统自动从 git 获取（本文件最后一次 commit 的 hash）
 *   - 只要本文件内容发生变化并 push，用户下次打开就会看到更新提示
 *   - 无需手动维护版本号
 *
 * 条目类型：
 *   new    — 新功能
 *   fix    — 修复
 *   tip    — 使用建议 / 注意事项
 *   change — 变更 / 调整
 */

declare const __CHANGELOG_VERSION__: string

export const CHANGELOG_VERSION: string =
  typeof __CHANGELOG_VERSION__ !== 'undefined' ? __CHANGELOG_VERSION__ : 'dev'

export type ChangelogItemType = 'new' | 'fix' | 'tip' | 'change'

export interface ChangelogItem {
  type: ChangelogItemType
  text: string
}

export interface Changelog {
  title: string
  subtitle?: string
  items: ChangelogItem[]
}

// ─────────────────────────────────────────────────────────────
//  每次更新只需修改这里
// ─────────────────────────────────────────────────────────────
export const changelog: Changelog = {
  title: '功能更新',
  subtitle: 'AI 模型全局切换 · Agent 架构升级',
  items: [
    {
      type: 'new',
      text: '新增全局 AI 模型切换功能，在「个人设置」中选择渠道（OpenRouter / DeepSeek）和模型后，全站所有生成功能均使用你的偏好配置',
    },
    {
      type: 'change',
      text: '草稿生成页 AI 配置区域调整为只读展示，显示当前正在使用的渠道与模型，如需切换请前往「个人设置」',
    },
    {
      type: 'new',
      text: '后端引入 Agent 运行时架构，角色提取等 AI 功能迁移至 Agent 框架，具备完整的运行记录与可观测能力',
    },
    {
      type: 'tip',
      text: '建议在「个人设置」中优先选择免费模型（GLM-4.5 Air、GPT-OSS 等）体验，如效果不理想再切换到 DeepSeek',
    },
  ],
}
