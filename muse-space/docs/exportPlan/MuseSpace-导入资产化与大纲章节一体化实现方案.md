# MuseSpace：导入资产化 + 大纲规划 + 章节生成一体化实现方案

> 适用仓库：`Explodewith-star/MuseSpace`  
> 当前日期：2026-04-28  
> 文档目标：在 MuseSpace 现有能力基础上，设计一条“原著导入 → 前置资产提取 → 大纲规划 → 章节配置 → 草稿生成”的完整实现路径，并与现有 `docs/plan` 阶段计划保持一致。

---

## 1. 背景与目标

MuseSpace 当前已经具备以下基础能力：

- `SceneDraftSkill` 场景草稿生成主链路
- `StoryContextBuilder` 上下文构建能力
- `Character`、`WorldRule`、`StyleProfile`、`Chapter` 等核心业务实体
- 原著导入、切片、向量化、检索能力
- Hangfire 后台任务能力
- SignalR 任务状态推送能力
- Agent 运行时基础设施（阶段 D1 已完成）

当前问题不在于“不能生成内容”，而在于生成链路仍偏单点：

1. 原著导入后，系统只能做检索增强，不能自动沉淀“角色卡 / 世界观 / 文风 / 章节摘要”等前置资产。
2. 大纲功能尚未形成稳定的结构化规划能力，无法真正承接“导入原著后继续创作”的核心需求。
3. 草稿生成页面仍以手工输入 `sceneGoal / conflict / emotionCurve` 为主，没有与大纲、章节树、角色资产形成闭环。
4. 各类 Agent 能力虽然已有规划，但还缺少统一的“任务中心 + 建议审核层”作为结果承接基础设施。

### 本文目标

在现有仓库架构基础上，设计并实现以下能力链路：

```text
导入小说
→ 自动提取候选资产（角色 / 世界观 / 文风 / 章节摘要）
→ 用户审核确认
→ 基于已确认资产 + 原著记忆生成结构化大纲
→ 用户审核 / 局部重做 / 导入章节树
→ 在章节页编辑本章配置
→ 生成本章草稿
→ 异步执行一致性检查
```

---

## 2. 与现有计划文档的对应关系

本方案不是脱离现有路线的新设计，而是对已有 `docs/plan` 的具体落地细化。

### 2.1 对应阶段

- **阶段 B：原著导入与基础记忆流水线**
  - 当前状态：基本完成
  - 本方案复用其导入、切片、embedding、检索能力，不重复造轮子

- **阶段 C：记忆接入生成主链路**
  - 当前状态：主干已完成
  - 本方案继续复用 `StoryContextBuilder` 和原著检索注入能力

- **阶段 D：Agent 化增强接入**
  - 本方案重点落在以下子项：
    - D1-4 统一任务中心与建议审核层
    - D3-1 大纲规划 Agent
    - D3-1.1 大纲功能最小产品边界
    - D3-2 菜单 Agent 化与统一入口
    - D3-3 原著导入后的自动提取流水线

### 2.2 不在当前阶段优先做的内容

以下内容不作为本期主线范围：

- GraphRAG
- Multi-Agent 协作
- 复杂长期版本树
- Redis / 独立 Worker 工业化拆分
- 面向所有原著 chunk 的自动摘要沉淀
- 高复杂度关系图谱

原因：当前最重要的是先打通“资产化 → 规划 → 章节创作”的主价值链路。

---

## 3. 当前系统能力盘点

### 3.1 已有草稿生成主链路

当前后端已经具备如下调用链：

```text
DraftController
→ GenerateSceneDraftAppService
→ ISkillOrchestrator
→ SceneDraftSkill
→ IStoryContextBuilder
→ PromptTemplate
→ ILlmClient
```

当前 `StoryContext` 已支持：

- 项目摘要
- 最近章节摘要
- 角色卡
- 世界规则
- 文风要求
- 场景目标
- 冲突
- 情绪弧线
- 原著检索片段

这说明“上下文注入能力”已经有稳定底座，后续只需扩展输入来源和资产来源，而不是推翻重做。

### 3.2 已有原著导入与检索能力

根据当前代码与计划文档：

- 已支持 TXT / Markdown 导入
- 已支持切片与 embedding
- 已支持按 `sceneGoal` 检索相关原著片段
- 已支持进度显示与失败状态回传

这意味着“从原著中自动提取结构化资产”具备实现前提。

### 3.3 已有 Agent 运行时

根据阶段 D 计划：

- `IAgentRunner`
- `AgentDefinition`
- `IAgentTool`
- `AgentRun` 可观测与开关控制

D1 已完成，说明系统已经具备承载“提取型 Agent / 规划型 Agent / 守护型 Agent”的基础。

---

## 4. 总体设计原则

## 4.1 不做单一超级 Agent

不建议实现“一个 Agent 完成原著理解、角色提取、世界观提取、文风归纳、大纲规划、章节生成”的超级流程。

原因：

- 输出不可控
- 无法逐步审核
- 无法沉淀为正式业务资产
- 难以复用已有 Skill / Agent 能力边界
- 与 `docs/plan` 中“建议审核层”“统一任务中心”的方向不一致

本方案采用：

- **提取型 Agent**：生成候选资产
- **规划型 Agent**：生成候选大纲、候选章节计划
- **执行型 Skill**：生成章节草稿
- **守护型 Agent**：一致性检查、风格审查

## 4.2 先候选、后确认、再入正式表

所有从原著中提取或由 Agent 自动生成的资产，默认都应进入**建议审核层**，而不是直接写入正式业务表。

适用对象：

- 角色卡候选
- 世界观规则候选
- 文风画像候选
- 章节摘要候选
- 大纲候选
- 章节计划候选

## 4.3 结构化优先，文本展示其次

大纲、章节计划、提取资产的核心输出均应以 JSON / DTO 为主，文本展示只是视图层加工结果。

原则：

- 先保证“可落库、可编辑、可导入”
- 再追求“展示很像人写的文档”

---

## 5. 目标产品链路

### 5.1 用户流程总览

```text
创建项目
→ 导入原著
→ 系统自动切片/向量化/索引
→ 系统自动发起资产提取任务
→ 任务中心中出现候选角色 / 设定 / 文风 / 章节摘要
→ 用户审核并应用
→ 用户发起大纲规划
→ 系统生成结构化大纲建议
→ 用户审核、编辑、局部重做
→ 导入章节树
→ 用户进入某一章
→ 自动带出本章配置
→ 生成本章草稿
→ 生成后异步一致性检查
```

### 5.2 核心菜单入口建议

建议形成以下主要入口：

- 项目概览页
  - 原著导入状态
  - 最近任务
  - 待审核建议
  - 快速发起大纲规划

- 原著导入页
  - 文件列表
  - 状态进度
  - 导入后自动提取任务状态
  - 提取建议入口

- 建议任务中心
  - 全部 Agent 任务统一查看
  - 全部建议统一审核

- 大纲页
  - 发起新作 / 续写 / 番外规划
  - 审核大纲
  - 局部重做
  - 导入章节树

- 章节页
  - 查看章节基础信息
  - 编辑章节计划
  - 生成草稿
  - 查看一致性检查结果

---

## 6. 模块拆分设计

### 6.1 提取型 Agent

负责将原著内容转成“候选资产”。

#### 包含能力

1. 角色提取 Agent
2. 世界观提取 Agent
3. 文风画像 Agent
4. 章节摘要提取 Agent（可第二期补）

#### 输出原则

- 不直接写 `Character` / `WorldRule` / `StyleProfile`
- 统一写入 `AgentSuggestion`

---

### 6.2 规划型 Agent

负责生产可审阅、可导入的结构化规划结果。

#### 包含能力

1. 大纲规划 Agent
2. 章节计划 Agent

---

### 6.3 执行型 Skill

负责将已经明确的上下文执行成文本生成。

#### 当前保留

- `SceneDraftSkill`

#### 后续建议新增

- `ChapterDraftSkill`

短期可复用 `SceneDraftSkill`，长期建议将“章节草稿生成”与“场景片段生成”分开。

---

### 6.4 守护型 Agent

负责生成后审查。

#### 建议能力

1. 世界观一致性检查
2. 角色一致性检查
3. 文风一致性审查

---

## 7. 数据模型设计

## 7.1 统一建议模型：AgentSuggestion

建议新增统一建议实体，而不是为每一类建议单独建一套表。

### 建议字段

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Guid | 主键 |
| StoryProjectId | Guid | 所属项目 |
| AgentRunId | Guid? | 来源 Agent 运行记录 |
| SuggestionType | string | 建议类型 |
| SourceType | string | 来源类型 |
| SourceRefId | Guid? | 来源引用，如导入任务 ID |
| Status | string | 建议状态 |
| Summary | string | 简要说明 |
| PayloadJson | jsonb | 结构化内容 |
| Confidence | decimal? | 置信度 |
| CreatedAtUtc | datetime | 创建时间 |
| ReviewedAtUtc | datetime? | 审核时间 |
| ReviewedByUserId | Guid? | 审核人 |
| AppliedEntityId | Guid? | 应用后对应正式实体 ID |

### SuggestionType 枚举建议

- `character`
- `world_rule`
- `style_profile`
- `chapter_summary`
- `outline`
- `chapter_plan`
- `consistency_issue`

### Status 枚举建议

- `suggested`
- `pending_review`
- `accepted`
- `rejected`
- `partially_applied`

---

## 7.2 统一任务模型：AgentTask（可选，但推荐）

如果当前 `AgentRun` 偏底层执行记录，建议在业务层补一个更面向前端展示的任务实体。

### 作用

- 展示任务中心列表
- 关联多个 Suggestion
- 统一状态聚合
- 区分“导入触发任务”“手动触发任务”“生成后守护任务”

### 字段建议

| 字段 | 类型 | 说明 |
|---|---|---|
| Id | Guid | 主键 |
| StoryProjectId | Guid | 项目 |
| TaskType | string | 任务类型 |
| TriggerSource | string | 触发来源 |
| Status | string | 状态 |
| AgentRunId | Guid? | 关联底层 AgentRun |
| Title | string | 展示标题 |
| StartedAtUtc | datetime | 开始时间 |
| FinishedAtUtc | datetime? | 完成时间 |
| ErrorMessage | string? | 错误信息 |

---

## 7.3 大纲模型建议

当前 `ImportOutlineRequest` 过轻，仅适合作为“审核后导入”的最终接口，不适合作为完整大纲承载模型。

建议新增：

### OutlineSuggestionDto

| 字段 | 类型 | 说明 |
|---|---|---|
| Mode | string | `new_story` / `continuation` / `side_story` |
| Summary | string | 大纲摘要 |
| Volumes | List\<OutlineVolumeDto\> | 卷级结构 |
| Threads | List\<OutlineThreadDto\> | 线索草案 |
| Constraints | List\<OutlineConstraintDto\> | 约束草案 |

### OutlineVolumeDto

| 字段 | 类型 | 说明 |
|---|---|---|
| Number | int | 卷序号 |
| Title | string | 卷标题 |
| Summary | string | 卷摘要 |
| MainConflict | string? | 主冲突 |
| Chapters | List\<OutlineChapterDto\> | 章节列表 |

### OutlineChapterDto

| 字段 | 类型 | 说明 |
|---|---|---|
| Number | int | 章节号 |
| Title | string | 标题 |
| Goal | string? | 本章目标 |
| Summary | string? | 摘要 |
| Conflict | string? | 章节冲突 |
| EmotionCurve | string? | 情绪弧线 |
| KeyCharacters | List\<string\> | 关键角色 |
| MustIncludePoints | List\<string\> | 必须推进点 |
| Foreshadowing | List\<string\> | 伏笔埋设/回收 |
| DependsOnChapterNumbers | List\<int\> | 依赖章节 |

### OutlineThreadDto

| 字段 | 类型 | 说明 |
|---|---|---|
| Name | string | 线索名 |
| Description | string | 说明 |
| StartChapter | int? | 起始章 |
| ExpectedPayoffChapter | int? | 预计回收章 |

### OutlineConstraintDto

| 字段 | 类型 | 说明 |
|---|---|---|
| Type | string | 约束类型 |
| Description | string | 约束描述 |
| Source | string | 来源：原著/角色卡/规则/用户输入 |

---

## 7.4 章节计划模型建议

建议引入独立章节计划实体或至少独立 DTO：

### ChapterPlanDto

| 字段 | 类型 | 说明 |
|---|---|---|
| ChapterId | Guid? | 对应章节 |
| Title | string | 标题 |
| Goal | string | 本章叙事目标 |
| Summary | string? | 本章摘要 |
| Conflict | string? | 核心冲突 |
| EmotionCurve | string? | 情绪弧线 |
| KeyCharacterIds | List\<Guid\> | 关键角色 |
| RequiredWorldRuleIds | List\<Guid\> | 强约束规则 |
| StyleOverrides | string? | 风格覆盖说明 |
| MustIncludePoints | List\<string\> | 必须包含信息 |
| MustAvoidPoints | List\<string\> | 必须规避内容 |
| PreviousChapterSummary | string? | 前章摘要 |
| RelatedNovelSnippets | List\<string\> | 关联原著片段 |
| ForeshadowingSetup | List\<string\> | 需埋设伏笔 |
| ForeshadowingPayoff | List\<string\> | 需回收伏笔 |

---

## 8. 导入后自动提取流水线设计

## 8.1 触发时机

在原著导入状态进入“已完成 / Indexed”后触发，不阻塞导入主流程。

### 任务链建议

```text
Novel Indexed
→ Enqueue CharacterExtractionAgent
→ Enqueue WorldRuleExtractionAgent
→ Enqueue StyleProfileExtractionAgent
→ Enqueue ChapterSummaryExtractionAgent（可后置）
```

### 说明

- 可以串行，也可以并行
- 第一版建议角色 / 世界观 / 文风并行
- 章节摘要提取可以第二阶段再补

---

## 8.2 角色提取策略

### 两段式策略

#### 第一阶段：角色识别

输出候选字段：

- 角色名
- 别名 / 称呼
- 出现频率
- 初始身份描述
- 关联人物
- 代表片段引用

#### 第二阶段：角色卡归纳

基于识别出的角色聚合原文片段后，归纳：

- PersonalitySummary
- Motivation
- SpeakingStyle
- ForbiddenBehaviors
- CurrentState

### 原因

先做“识别”，再做“归纳”，比直接从整本书输出最终角色卡更稳。

---

## 8.3 世界观提取策略

建议区分两类输出：

1. `setting_fact`
   - 世界设定事实
   - 例如时代背景、社会结构、组织信息、能力体系存在性

2. `world_rule`
   - 对生成具约束性的规则
   - 例如能力不能越界、法律限制、世界物理法则、阵营行为边界

### 为什么要区分

不是所有“设定描述”都应该直接进入 `WorldRule`。  
真正进入正式规则表的内容，应优先是可约束生成的规则。

---

## 8.4 文风画像提取策略

建议映射到已有 `StyleProfile` 模型，并适当扩展：

### 第一版字段建议

- Name
- Tone
- SentenceLengthPreference
- DialogueRatio
- DescriptionDensity
- ForbiddenExpressions

### 第二版可扩展字段

- NarrativePerspective
- Rhythm
- CommonRhetoricPatterns
- EmotionalExpressionLevel

### 输出原则

- 给出字段值
- 给出依据片段摘要
- 给出置信度
- 不直接覆盖现有用户手工风格档案

---

## 8.5 章节摘要提取策略（第二阶段建议）

如果原著本身有明显章标题，可先提章节清单。  
如果没有明确章节边界，则可先不强行自动结构化，只做“摘要候选”。

建议第二期再做，避免首期复杂度过高。

---

## 9. 大纲规划设计

## 9.1 模式设计

大纲功能第一版支持三类主模式：

1. **新作规划**
2. **续写规划**
3. **番外规划**

局部补全不作为独立主模式，而是作为以上三种模式的局部操作。

---

## 9.2 输入设计

### 通用输入

- 规划模式
- 一句话创作目标
- 目标篇幅 / 预计章节数
- 风格偏好
- 强约束说明

### 新作规划额外输入

- 题材
- 核心设定
- 主角简介
- 主冲突

### 续写规划额外输入

- 原著范围 / 选择导入作品
- 续写起点
- 必须继承内容
- 禁止偏离内容

### 番外规划额外输入

- 关联主线章节
- 视角角色
- 番外主题
- 篇幅目标

---

## 9.3 输出边界

第一版必须稳定覆盖四层：

1. 大纲摘要
2. 卷级结构
3. 章级结构
4. 线索与约束草案

### 明确不追求

- 自动构建复杂 PlotThread 图谱
- 多版本大纲谱系管理
- 复杂实体关系推理
- 自动完美章节树优化

---

## 9.4 生成结果流转

```text
用户提交大纲请求
→ 创建 AgentTask
→ 运行 OutlinePlanningAgent
→ 生成 OutlineSuggestion
→ 写入 AgentSuggestion
→ 前端展示结构化结果
→ 用户可整份接受 / 单卷重做 / 单章编辑
→ 最终导入章节树
```

---

## 10. 章节计划与章节草稿设计

## 10.1 为什么需要章节计划层

当前 `sceneGoal / conflict / emotionCurve` 仅适用于单次草稿生成，不适合长篇章节创作。

缺失问题：

- 无法绑定章节树
- 无法表达关键角色
- 无法表达必须推进点
- 无法表达前后章关系
- 无法承接大纲结果

因此需要引入 `ChapterPlan` 作为章节执行前的结构化配置层。

---

## 10.2 章节页能力设计

建议章节页新增以下区域：

1. 章节基础信息
   - 标题
   - 编号
   - 摘要

2. 章节计划
   - 目标
   - 冲突
   - 情绪弧线
   - 关键角色
   - 必须推进点
   - 风格补充
   - 伏笔安排

3. 原著参考
   - 自动检索片段
   - 可手动选取引用片段

4. 草稿生成
   - 生成按钮
   - 结果展示
   - 二次生成

5. 守护结果
   - 世界观一致性
   - 角色一致性
   - 风格审查

---

## 10.3 章节草稿生成链路

### 方案 A：短期复用 `SceneDraftSkill`

扩展当前请求，使其支持：

- ChapterId
- OutlineChapterId
- InvolvedCharacterIds
- AdditionalConstraints
- MustIncludePoints
- StyleProfileId

优点：

- 改动小
- 快速落地

缺点：

- 语义仍偏“场景”
- 后续会逐渐变复杂

### 方案 B：长期新增 `ChapterDraftSkill`

建议在第二阶段落地：

- `GenerateChapterDraftAppService`
- `ChapterDraftSkill`
- `ChapterContextBuilder`

优点：

- 职责清晰
- 更适合完整章节创作

---

## 11. 后端接口设计建议

## 11.1 建议与任务中心

### 获取任务列表

`GET /api/projects/{projectId}/agent-tasks`

### 获取建议列表

`GET /api/projects/{projectId}/suggestions`

支持筛选：

- type
- status
- sourceType

### 审核建议

`POST /api/projects/{projectId}/suggestions/{suggestionId}/accept`

`POST /api/projects/{projectId}/suggestions/{suggestionId}/reject`

---

## 11.2 原著导入后提取

### 手动重跑提取任务

`POST /api/projects/{projectId}/novels/{novelId}/extract-assets`

### 获取提取结果

`GET /api/projects/{projectId}/novels/{novelId}/asset-suggestions`

---

## 11.3 大纲

### 发起大纲规划

`POST /api/projects/{projectId}/outline/generate`

### 获取大纲建议详情

`GET /api/projects/{projectId}/outline/suggestions/{suggestionId}`

### 局部重做卷

`POST /api/projects/{projectId}/outline/suggestions/{suggestionId}/volumes/{volumeNumber}/regenerate`

### 导入大纲到章节树

`POST /api/projects/{projectId}/outline/suggestions/{suggestionId}/import`

---

## 11.4 章节计划

### 自动生成章节计划候选

`POST /api/projects/{projectId}/chapters/{chapterId}/plan/generate`

### 获取章节计划

`GET /api/projects/{projectId}/chapters/{chapterId}/plan`

### 保存章节计划

`PUT /api/projects/{projectId}/chapters/{chapterId}/plan`

---

## 11.5 章节草稿

### 生成章节草稿

`POST /api/projects/{projectId}/chapters/{chapterId}/draft`

### 获取一致性结果

`GET /api/projects/{projectId}/chapters/{chapterId}/draft-checks`

---

## 12. 后端实现落点建议

## 12.1 Application 层

建议新增：

- `Services/Agents/`
  - `GenerateOutlineAppService`
  - `ExtractNovelAssetsAppService`
  - `GenerateChapterPlanAppService`
  - `GenerateChapterDraftAppService`

- `Abstractions/Suggestions/`
  - `IAgentSuggestionRepository`
  - `IAgentTaskRepository`

- `Contracts/Outline/`
  - `GenerateOutlineRequest`
  - `OutlineSuggestionDto`
  - `ImportOutlineSuggestionRequest`

- `Contracts/Chapters/`
  - `GenerateChapterPlanRequest`
  - `ChapterPlanDto`
  - `GenerateChapterDraftRequest`

---

## 12.2 Domain 层

建议新增实体：

- `AgentSuggestion`
- `AgentTask`
- `ChapterPlan`（如果决定正式落库）
- `OutlineSnapshot`（可选，若需要保留大纲版本）

---

## 12.3 Infrastructure 层

建议新增：

- `Agents/Definitions/`
  - `OutlinePlanningAgentDefinition`
  - `NovelCharacterExtractionAgentDefinition`
  - `NovelWorldRuleExtractionAgentDefinition`
  - `NovelStyleProfileAgentDefinition`

- `Agents/Tools/`
  - `GetProjectSummaryTool`
  - `GetCharacterCardsTool`
  - `GetWorldRulesTool`
  - `SearchNovelSnippetsTool`
  - `GetRecentChaptersTool`

- `Persistence/Repositories/`
  - `AgentSuggestionRepository`
  - `AgentTaskRepository`
  - `ChapterPlanRepository`

---

## 12.4 Api 层

建议新增控制器：

- `SuggestionsController`
- `AgentTasksController`
- `OutlineController`
- `ChapterPlanController`

保留：

- `DraftController`

后续可考虑让 `DraftController` 只保留兼容接口，章节型草稿转移到 `ChapterDraftController`。

---

## 13. 前端实现设计

## 13.1 新增页面建议

### 1. 任务中心页

路径建议：

- `/projects/:projectId/tasks`

功能：

- 查看所有 Agent 任务
- 查看状态
- 跳转到对应建议详情

### 2. 建议中心页

路径建议：

- `/projects/:projectId/suggestions`

功能：

- 按类型筛选建议
- 批量接受 / 忽略
- 逐条预览详情

### 3. 大纲页

路径建议：

- `/projects/:projectId/outline`

功能：

- 发起大纲生成
- 查看大纲建议
- 单卷重做
- 单章编辑
- 导入章节树

### 4. 章节详情页增强

路径建议：

- `/projects/:projectId/chapters/:chapterId`

功能：

- 编辑章节计划
- 生成本章草稿
- 查看检查结果

---

## 13.2 原草稿页的升级路径

当前页面：`src/views/projects/draft/index.vue`

建议分两步处理：

### 第一步

保留现有页面作为“自由场景草稿页”，不立即删除。

### 第二步

新增“章节草稿页”：

- 以章节为入口
- 自动带出章节计划
- 复用现有生成结果 UI 组件

这样风险更低。

---

## 14. 建议审核流设计

## 14.1 审核动作

每条建议应支持：

- 接受
- 忽略
- 编辑后接受

### 编辑后接受的意义

例如：

- 角色卡字段有偏差，用户手改后导入
- 世界规则措辞不准确，用户修改后保存
- 大纲单章标题调整后再导入

---

## 14.2 应用逻辑

不同建议类型的应用方式不同：

- `character` → 写入 `Character`
- `world_rule` → 写入 `WorldRule`
- `style_profile` → 写入 `StyleProfile`
- `outline` → 导入 `Chapter`
- `chapter_plan` → 写入 `ChapterPlan`

建议通过统一 `SuggestionApplicationService` 做分发。

---

## 15. 一致性守护设计

## 15.1 世界观一致性

当前 `DraftController` 已在草稿生成成功后异步挂 `ConsistencyCheckJob`。  
这是一个很好的起点，但建议后续扩展为结构化结果：

### 输出建议字段

- IssueType
- Severity
- EvidenceText
- ConflictedRule
- Suggestion

---

## 15.2 角色一致性

建议新增角色一致性 Agent：

- 检查行为是否符合角色卡
- 检查身份、关系、状态是否冲突

---

## 15.3 文风一致性

建议作为第二期能力：

- 对照 `StyleProfile`
- 判断句式、语调、表达习惯偏离情况
- 仅给出审查建议，不强行改写

---

## 16. 实施顺序建议

## 第一阶段：基础承接层

目标：先让所有后续 Agent 结果有地方落。

任务：

1. 新增 `AgentSuggestion`
2. 新增 `AgentTask`
3. 新增建议审核 API
4. 新增前端任务中心 / 建议中心基础页面

### 验收

- 至少两类建议可走统一审核流

---

## 第二阶段：导入后自动资产提取

目标：把原著从“可检索文本”升级为“可审核候选资产来源”。

任务：

1. 角色提取 Agent
2. 世界观提取 Agent
3. 文风画像 Agent
4. 导入完成后自动触发提取任务

### 验收

- 导入完成后，用户可以看到角色 / 规则 / 文风候选

---

## 第三阶段：大纲规划第一版

目标：让原著与资产真正参与长篇规划。

任务：

1. 大纲生成接口
2. 三类模式表单
3. 大纲结构化输出
4. 单卷重做
5. 导入章节树

### 验收

- 用户能生成并导入一份可编辑的大纲

---

## 第四阶段：章节计划与章节草稿

目标：打通大纲到正文的执行链。

任务：

1. 章节计划 DTO / 实体
2. 章节计划编辑界面
3. 章节草稿生成接口
4. 章节页生成正文

### 验收

- 从章节页可直接生成本章草稿，而不是回到自由草稿页手填

---

## 第五阶段：一致性守护增强

目标：让生成质量更可控。

任务：

1. 世界观一致性结构化结果
2. 角色一致性检查
3. 风格审查（可后置）

### 验收

- 章节草稿生成后可看到结构化问题提示

---

## 17. 风险与注意事项

### 17.1 不要直接写正式资产表

自动提取结果一定有噪声，必须先进建议层。

### 17.2 不要把大纲做成纯文本

必须先结构化，才能审阅、编辑、导入。

### 17.3 不要让章节草稿仍停留在手工三字段模式

否则大纲与正文之间仍然是断链。

### 17.4 不要过早引入 GraphRAG / Multi-Agent

当前阶段应优先保证流程闭环与可控性。

---

## 18. 建议的最小可交付版本（MVP）

### MVP-1

- 统一建议层
- 角色 / 世界观 / 文风提取
- 用户审核应用

### MVP-2

- 续写模式大纲规划
- 结构化大纲审核
- 导入章节树

### MVP-3

- 章节计划编辑
- 从章节页生成本章草稿
- 世界观一致性异步反馈

---

## 19. 完成定义

本方案可视为落地完成，当满足以下条件：

1. 原著导入完成后可自动生成候选资产
2. 候选资产可统一审核并应用到正式业务表
3. 用户可基于已确认资产发起大纲规划
4. 大纲结果可结构化查看、编辑、局部重做、导入章节树
5. 用户可从章节页而非自由输入页直接生成章节草稿
6. 生成后可得到至少一种一致性守护结果
7. 全链路具备任务状态、失败回退和基础可观测能力

---

## 20. 推荐结论

对于 MuseSpace 当前阶段，最合理的路线不是继续堆“更强的单次生成”，而是尽快把系统从“草稿工具”升级为“创作资产驱动平台”。

最优先要做的不是更复杂的模型玩法，而是：

1. **统一任务中心与建议审核层**
2. **导入后自动资产提取**
3. **结构化大纲规划**
4. **章节计划承接草稿生成**

只要这四步真正跑通，MuseSpace 就会从“能生成一段文本”进入“能持续支撑长篇创作”的阶段。

---