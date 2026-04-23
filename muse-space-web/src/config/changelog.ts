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
  subtitle: '草稿生成页 · AI 配置升级',
  items: [
    {
      type: 'new',
      text: '草稿生成页新增 AI 渠道切换功能，支持在 OpenRouter 和 DeepSeek 之间一键切换',
    },
    {
      type: 'new',
      text: 'OpenRouter 渠道现在可以选择具体模型，支持模糊搜索快速定位',
    },
    {
      type: 'tip',
      text: '建议先使用免费模型（GLM-4.5 Air、GPT-OSS 等），速度快、零成本，适合快速出草稿',
    },
    {
      type: 'tip',
      text: '如果免费模型生成效果不稳定或质量不满意，切换到 DeepSeek 渠道 —— DeepSeek 是目前最稳定的选择，推荐用于正式创作',
    },
  ],
}
