# MuseSpace Prompt 模板规范

> 本文档定义了 MuseSpace 项目中 Prompt 模板文件的统一格式、命名规范和变量约定，所有 Prompt 文件都应遵循本规范。

---

## 1. 文件存储位置

所有 Prompt 模板存放在项目根目录的 `prompts/` 下，按**任务类型**分类存放：

```text
prompts/
├── drafting/           # 场景草稿类
│   └── scene-v1.md
├── revision/           # 改稿类（Phase 4）
│   └── scene-v1.md
└── consistency/        # 一致性检查类（Phase 4）
    └── character-v1.md
```

---

## 2. 文件命名规范

```
{task-name}-v{n}.md
```

- `task-name`：任务名称，使用小写字母和连字符，语义清晰
- `v{n}`：版本号，从 `v1` 开始，每次需要不兼容修改时递增

**示例：**

| 文件名 | 含义 |
|---|---|
| `scene-v1.md` | 场景草稿，第 1 版 |
| `scene-v2.md` | 场景草稿，第 2 版（有较大改动时新建） |
| `character-v1.md` | 角色一致性检查，第 1 版 |

**规则：**
- 不删除旧版本，保留历史以便回退对比
- 小幅调整直接修改当前版本文件
- 较大结构变更时新建下一个版本号

---

## 3. 文件结构规范

每个 Prompt 模板文件包含**固定的 4 个 Section**，使用 `## section名` 作为分隔符：

```markdown
# {Prompt 标题}
# Category: {category}
# Version: {version}

## system

{系统角色设定，告诉模型它是谁、有哪些行为规则}

## instruction

{任务指令，包含变量占位符，描述具体要做什么}

## context

{可选的补充上下文，通常也包含变量}

## output_format

{要求模型按照什么格式输出，优先使用 JSON}
```

### Section 说明

| Section | 必填 | 说明 |
|---|---|---|
| `## system` | ✅ | 模型角色定义和全局行为约束 |
| `## instruction` | ✅ | 核心任务描述，包含主要变量 |
| `## context` | 可选 | 补充背景信息，当 instruction 太长时拆分到这里 |
| `## output_format` | ✅ | 输出格式要求，必须明确且可解析 |

---

## 4. 变量占位符规范

变量使用双花括号语法：`{{variable_name}}`

- 变量名使用**小写字母和下划线**
- 变量名应语义清晰，一眼能看出内容
- 同一 Prompt 文件中的变量名不重复

**示例：**

```markdown
**Project Summary:**
{{project_summary}}

**Scene Goal:**
{{scene_goal}}
```

### 当前 scene-v1.md 使用的变量清单

| 变量名 | 来源 | 说明 |
|---|---|---|
| `{{project_summary}}` | `StoryContext.ProjectSummary` | 故事项目背景摘要 |
| `{{recent_summaries}}` | `StoryContext.RecentChapterSummaries` | 最近章节摘要（最多 3 章） |
| `{{character_cards}}` | `StoryContext.InvolvedCharacterCards` | 涉及角色卡片（最多 4 人） |
| `{{world_rules}}` | `StoryContext.WorldRules` | 世界规则（最多 8 条） |
| `{{style_requirement}}` | `StoryContext.StyleRequirement` | 风格要求 |
| `{{scene_goal}}` | `SkillRequest.Parameters["SceneGoal"]` | 本场景目标（必填） |
| `{{conflict}}` | `SkillRequest.Parameters["Conflict"]` | 场景冲突 |
| `{{emotion_curve}}` | `SkillRequest.Parameters["EmotionCurve"]` | 情绪曲线 |

---

## 5. 输出格式规范

**原则：优先要求模型输出 JSON。**

`## output_format` Section 中应明确定义 JSON 结构，并说明每个字段的含义：

```markdown
## output_format

Respond with a valid JSON object in the following format. Do not include any text outside the JSON block.

{
  "scene_text": "完整的场景正文",
  "word_count": 500,
  "characters_appeared": ["角色A", "角色B"],
  "summary": "一句话场景摘要，用于后续章节上下文"
}
```

**规则：**
- 必须注明"不要在 JSON 以外输出任何内容"
- 字段名使用下划线风格（`snake_case`）
- 每个字段附带简短说明（写在 Prompt 里，作为模型的参考）
- 数组字段明确类型（字符串数组、对象数组等）

---

## 6. 代码中如何加载和渲染 Prompt

### 加载模板

`IPromptTemplateProvider.GetTemplateAsync` 接受 `category` 和 `templateName` 两个参数，对应 `prompts/{category}/{templateName}.md`：

```csharp
// 加载 prompts/drafting/scene-v1.md
var template = await _promptProvider.GetTemplateAsync("drafting", "scene-v1", cancellationToken);
```

### 渲染变量

`IPromptTemplateRenderer.Render` 将模板中的 `{{变量名}}` 替换为实际值：

```csharp
var variables = new Dictionary<string, string>
{
    ["scene_goal"] = "主角进入禁忌森林",
    ["project_summary"] = "...",
    // ...其他变量
};

string renderedPrompt = _promptRenderer.Render(template, variables);
```

渲染后的字符串直接传给 `ILlmClient.ChatAsync`。

---

## 7. 完整示例：scene-v1.md 结构

```markdown
# Scene Draft Prompt Template
# Category: drafting
# Version: v1

## system

You are a professional novel writer. You write vivid, immersive scene drafts based on the provided context.

Rules:
- Stay consistent with character personalities and world rules
- Follow the specified style requirements
- Do not contradict established facts from previous chapters

## instruction

Write a scene draft based on the following information.

**Project Summary:**
{{project_summary}}

**Involved Characters:**
{{character_cards}}

**Scene Goal:**
{{scene_goal}}

**Conflict:**
{{conflict}}

**Emotion Curve:**
{{emotion_curve}}

## context

**Recent Chapter Summaries:**
{{recent_summaries}}

**World Rules:**
{{world_rules}}

**Style Requirement:**
{{style_requirement}}

## output_format

Respond with a valid JSON object. Do not include any text outside the JSON.

{
  "scene_text": "完整场景正文",
  "word_count": 500,
  "characters_appeared": ["角色A"],
  "summary": "一句话摘要"
}
```

---

## 8. 注意事项

- Prompt 文件使用 **UTF-8 编码**，不带 BOM
- Section 标题 `##` 与文字之间有一个空格（`## system`，不是`##system`）
- 变量如果在上下文中没有值，对应位置会传入空字符串，Prompt 应能容错
- 不在 Prompt 文件中写硬编码的具体故事内容，所有内容通过变量注入
