# MuseSpace 使用工作流指南

> 本文档面向想要使用 MuseSpace 进行 AI 辅助小说创作的用户，介绍完整的使用流程。
> 随项目阶段性迭代持续更新。当前版本对应 Phase 3。

---

## 系统能做什么？

MuseSpace 是一个本地 AI 创作工作流系统。你提供：

- 故事世界的背景资料（角色、规则、风格）
- 当前场景的目标描述

系统负责：

- 把你的资料组织成结构化上下文
- 渲染成 Prompt 传给语言模型
- 返回符合你世界观的场景草稿

**核心价值：** 不是替你写作，而是让 AI 了解你的故事世界，在一致的框架下协助生成。

---

## 快速开始

### 环境要求

- .NET 10 SDK
- OpenRouter API Key（在 [openrouter.ai](https://openrouter.ai) 免费注册）

### 启动步骤

1. 克隆项目并进入目录

2. 配置 API Key：在 `src/MuseSpace.Api/appsettings.Development.json` 中填入：
   ```json
   {
     "Llm": {
       "ApiKey": "你的 OpenRouter API Key"
     }
   }
   ```

3. 运行项目：
   ```bash
   dotnet run --project src/MuseSpace.Api
   ```

4. 打开 Scalar API 界面：
   ```
   https://localhost:{port}/scalar/v1
   ```

---

## 推荐工作流

### 第一步：创建故事项目

在 Scalar 中调用：

```
POST /api/projects
```

```json
{
  "name": "星海传说",
  "description": "一个科幻架空世界，人类与人工智能共存的近未来",
  "genre": "科幻",
  "narrativePerspective": "第三人称有限视角"
}
```

记录返回的 `id`，后续所有操作都需要它。

---

### 第二步：添加角色卡

```
POST /api/projects/{projectId}/characters
```

```json
{
  "name": "林宇",
  "age": 28,
  "role": "主角",
  "personalitySummary": "理性、谨慎，表面冷漠但内心重情义",
  "motivation": "寻找失踪的妹妹",
  "speakingStyle": "简短克制，很少主动开口",
  "forbiddenBehaviors": "不会轻易相信陌生人，不会表露情绪",
  "currentState": "刚刚抵达边境城市，身上只剩半个月的盘缠"
}
```

**建议：** 为每个在当前故事阶段活跃的角色都创建一张卡片。

---

### 第三步：添加世界规则

```
POST /api/projects/{projectId}/world-rules
```

```json
{
  "title": "人工智能不能自主行动",
  "description": "根据星海协议，所有 AI 必须在人类指令下运作，私自行动是违法行为",
  "category": "法律",
  "priority": 10,
  "isHardConstraint": true
}
```

**建议：**
- `isHardConstraint: true` 表示模型必须遵守的硬规则（如世界物理法则）
- `priority` 越高，上下文中排序越靠前（最多取 8 条）

---

### 第四步：配置写作风格

```
PUT /api/projects/{projectId}/style-profile
```

```json
{
  "name": "林宇视角风格",
  "tone": "压抑、克制，偶尔爆发",
  "sentenceLengthPreference": "短句为主，节奏紧凑",
  "dialogueRatio": "对话少，内心描写多",
  "descriptionDensity": "环境描写简洁，聚焦人物反应",
  "forbiddenExpressions": "不用感叹号，不用'突然'这个词"
}
```

---

### 第五步：记录章节摘要（可选但推荐）

每写完一章，添加摘要帮助模型了解已发生的事件：

```
POST /api/projects/{projectId}/chapters
```

```json
{
  "number": 1,
  "title": "边境城市",
  "summary": "林宇抵达边境城市瞭望台，在查票时与女警探苏晴发生冲突，险些暴露身份。最终用假证件蒙混过关，躲进廉价旅馆。"
}
```

**上下文预算：** 系统最多取最近 3 章的摘要，优先取章节号最大的。

---

### 第六步：生成场景草稿

```
POST /api/draft/scene
```

```json
{
  "storyProjectId": "你的项目ID",
  "sceneGoal": "林宇在旅馆房间整理线索，接到一个来自未知号码的神秘电话",
  "conflict": "电话里的人声称知道妹妹的下落，但要求他明天独自前往废弃工厂",
  "emotionCurve": "疲惫→震惊→怀疑→决然"
}
```

---

### 理解输出

系统会返回：

```json
{
  "requestId": "a1b2c3d4e5f6",
  "generatedText": "...(模型生成的场景正文)...",
  "durationMs": 15000
}
```

生成后检查：
1. **是否符合角色性格？** 如果模型的角色行为不对，检查角色卡描述是否足够具体
2. **是否遵守了世界规则？** 如果有违反，尝试把规则设为 `isHardConstraint: true`
3. **风格是否正确？** 检查 StyleProfile 的描述是否足够清晰

---

## 迭代改进 Prompt

场景生成效果主要取决于两个因素：

### 1. 资料质量
- 角色描述越具体，模型越容易保持一致性
- 世界规则越清晰，模型越不容易出戏
- 章节摘要越准确，模型越了解当前故事状态

### 2. Prompt 模板
Prompt 模板位于 `prompts/drafting/scene-v1.md`，可以直接编辑。

如果想要不同风格的生成策略，可以新建 `scene-v2.md` 进行 A/B 比较。

---

## 查看生成日志

每次生成都会在 `src/MuseSpace.Api/bin/Debug/net10.0/logs/generations/` 下生成一个 JSON 文件，包含：

- 请求 ID 和项目 ID
- 使用的 Skill 和 Prompt 版本
- 耗时
- 输入和输出预览

用于回溯和调试。

---

## 常见问题

**Q: 生成的内容不符合我的世界观怎么办？**  
A: 先检查世界规则是否添加够了，再检查 Prompt 模板的 system 角色描述是否限制了模型的输出。

**Q: 生成结果里角色说话方式不对？**  
A: 在角色卡的 `speakingStyle` 和 `forbiddenBehaviors` 字段里加更多具体描述。

**Q: 生成速度很慢？**  
A: 免费模型（`gpt-oss-120b:free`）速度受限制，可以在 `appsettings.json` 中换用其他 OpenRouter 支持的模型。

**Q: 如何切换模型？**  
A: 在草稿生成页面底部的「AI 配置」区域，点击渠道按钮即可切换。选择 OpenRouter 后会显示可选模型的下拉列表，支持模糊搜索。

**Q: 免费模型和 DeepSeek 有什么区别？**  
A: 免费模型（如 GLM-4.5 Air、Qwen3 Coder）速度快、无成本，适合快速出草稿。DeepSeek 是付费渠道，生成质量更稳定，适合正式创作。建议先用免费模型，不满意再切 DeepSeek。

**Q: 怎么知道这次更新了什么功能？**  
A: 每次部署新版本后，用户首次打开页面时会自动弹出更新提示，点击「知道了」后记录已读，下次不再显示。

---

## 更新提示机制（开发者说明）

MuseSpace 内置了一个轻量的更新通知系统，每次部署后自动告知用户新增了什么。

### 工作原理

1. `src/config/changelog.ts` 是唯一需要维护的文件，里面写更新内容
2. Vite 构建时自动执行 `git log --format=%h -1 -- src/config/changelog.ts`，获取该文件最后一次修改的 commit hash
3. 这个 hash 被注入到打包产物中，作为「当前版本号」
4. 用户打开页面时，前端比较这个 hash 和 `localStorage` 里存的已读版本
5. 不一致 → 弹出更新提示弹窗；点「知道了」后写入 localStorage，下次不再弹

### 如何发布新的更新提示

只需修改 `src/config/changelog.ts` 的 `items` 内容，然后正常 commit + push：

```ts
// src/config/changelog.ts
export const changelog: Changelog = {
  title: '功能更新',
  subtitle: '本次更新说明',
  items: [
    { type: 'new', text: '新增了某某功能' },
    { type: 'tip', text: '建议先尝试免费模型' },
    { type: 'fix', text: '修复了某某问题' },
  ],
}
```

**条目类型：**
| 类型 | 图标颜色 | 用途 |
|------|---------|------|
| `new` | 紫色 | 新功能 |
| `change` | 灰色 | 行为变更 |
| `fix` | 绿色 | 问题修复 |
| `tip` | 黄色 | 使用建议 |

> **注意**：只要 `changelog.ts` 文件内容没有变化，即使其他代码 push 了多次，也不会触发弹窗。版本号完全自动，无需手动维护。

---

## 当前阶段限制（Phase 3）

- 暂不支持编辑已有角色/规则（需删除后重建）
- 暂不支持向量检索（上下文取前 N 条，不按相关性）
- 暂不支持改稿和一致性检查（Phase 4 功能）
- 数据存储为本地 JSON 文件，暂不支持数据库
