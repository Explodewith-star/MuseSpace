# 业务过渡 Agent 实现计划

> 文档目的：在现有 `Skill` 单步调用基础上，逐步引入「Agent（多步推理 / 工具调用 / 自治校验）」能力，沉淀通用运行时与可观测体系，把现有数据资产（角色卡、世界观、风格档案、章节）真正"激活"到生成质量与作者体验上。
>
> 适用读者：MuseSpace 开发者、产品规划。
>
> 命名约定：本文档中「Agent」指**带工具调用、可多步推理、有终止条件**的 LLM 任务编排单元；区别于现有的 `ISkill`（单次 prompt → 单次输出）。

---

## 0. 现状盘点（截至 2026-04-24）

### 0.1 已落地的 LLM 基础设施

| 模块 | 位置 | 说明 |
|---|---|---|
| Provider 抽象 | `MuseSpace.Application/Abstractions/Llm/` | `LlmProviderSelector`（Scoped）、`ILlmClient` |
| Provider 实现 | `MuseSpace.Infrastructure/Llm/` | `OpenRouterLlmClient`、`DeepSeekLlmClient`、`RoutingLlmClient`、`SiliconFlowEmbeddingClient` |
| 用户级偏好 | `Domain/Entities/UserLlmPreference.cs` + `Api/Middleware/LlmPreferenceMiddleware.cs` | 每请求按 JWT 用户加载偏好 → Selector |
| Skill 抽象 | `Application/Abstractions/Skills/ISkill.cs` 等 | 单步技能契约：`TaskType` + `ExecuteAsync(SkillRequest)` |
| Skill 编排 | `Application/Services/SkillOrchestrator.cs` | DI 自动收集所有 `ISkill`，按 `TaskType` 路由 |
| 已实现 Skill | `Application/Services/Drafting/SceneDraftSkill.cs` | 场景草稿生成 |
| 提示词管理 | `Application/Abstractions/Prompt/` + `Infrastructure/Prompt/` | 模板加载（已支持 `prompts/drafting/scene-v1.md`） |
| 任务调度 | `MuseSpace.Api/Hangfire/` | Hangfire 已就绪，可承载长跑 Agent |
| 实时通知 | `MuseSpace.Api/Hubs/` | SignalR 已就绪，可推送 Agent 进度 |
| 记忆/向量 | `Infrastructure/Memory/`、pgvector | 可作为 RAG 工具底座 |
| 日志 | Serilog + `logs/` | 结构化日志已落，Agent 运行轨迹可直接挂 Sink |

### 0.2 已有的领域数据（可被 Agent 复用）

| 实体 | 现状 | Agent 视角的复用方式 |
|---|---|---|
| `StoryProject` | CRUD 完成 | 项目级上下文容器（聚合根） |
| `Novel` / `Chapter` | CRUD 完成 | 长文本主体 + 章节树 |
| `Scene` | 草稿生成已用 | 最小生成单元 |
| `Character` | 角色卡 Agent 已能提取 | **复用 Schema 反向校验** |
| `WorldRule` | 已存数据，未介入生成 | **一致性 Agent 的核心输入** |
| `StyleProfile` | Prompt 注入式使用 | 升级为「reviewer Agent」校验输出 |
| `NovelChunk` | 切片入库（向量） | 长文检索工具 |
| `GenerationRecord` | 已存历史 | Agent 运行可追溯 / 审计 |

### 0.3 已有的"准 Agent"实现：角色卡提取

> 用户提到的"角色卡 agent 辅助提取"是当前唯一的 Agent 雏形。其能力：从用户粘贴的小说片段中识别人物 → 输出结构化 `Character` JSON。
>
> **它的不足**（也是本规划要解决的）：
> 1. 逻辑硬编码在某个 Service 中，**无法复用**到其他 Agent
> 2. 没有显式的 tool / step 概念，无法做"读取已有角色去重"等工具调用
> 3. 没有运行轨迹记录，Token / 成本 / 失败率不可观测
> 4. 没有 feature flag，无法灰度 / 紧急下线

---

## 1. 设计原则

1. **不重复造轮子**：Agent 运行时完全复用现有 `ILlmClient` / `LlmProviderSelector` / `Hangfire` / `SignalR` / Serilog，不自己再封装一套。
2. **Skill 与 Agent 共存**：单轮任务（命名、扩写一段）继续用 `ISkill`；多步 / 工具调用 / 自校验场景才用 `IAgent`。判断标准 = **是否需要 tool_call 或多次 LLM 往返**。
3. **配置优于代码**：Agent 的 system prompt、可用工具、最大步数尽量做成可声明的（C# record 或 YAML 都可，先 record）。
4. **可观测 first**：先有 `AgentRun` 表 / 日志 Sink，再开发第一个 Agent。否则上线后无法定位问题。
5. **降级路径**：每个 Agent 必须支持 `feature flag` 关闭 + "失败不阻塞主流程"（如一致性校验失败 ≠ 生成失败）。
6. **成本意识**：默认走便宜模型，允许用户在 UI 显式升级到旗舰模型（复用刚做完的用户级偏好，未来扩展为「按 Agent 维度」偏好）。

---

## 2. 阶段划分（按业务价值排序）

> 每个阶段的"完成定义"都包括：✅ 代码合并 + ✅ 至少 1 个 e2e 测试 + ✅ 文档更新（本文）。

### 🟦 P0 — Agent 运行时基础设施（2~3 天）

**目标**：抽出可复用的 `IAgentRunner`，把现有"角色卡提取"重构为第一个标准 Agent，作为后续所有 Agent 的样板。

**新增模块**：

```
MuseSpace.Application/
  Abstractions/
    Agents/
      IAgentRunner.cs          // Run(name, input, ctx, ct) → AgentRunResult / IAsyncEnumerable
      AgentDefinition.cs       // record: Name, SystemPrompt, Tools, MaxSteps, DefaultModel
      IAgentTool.cs            // Name, JsonSchema, ExecuteAsync(args, ctx)
      AgentRunContext.cs       // UserId, ProjectId, CancellationToken, 累计 token/cost
      AgentRunResult.cs        // Steps[], FinalOutput, TokensUsed, Cost, Status
  Services/
    Agents/
      AgentRunner.cs           // 默认实现：循环 LLM → tool_call → 工具执行 → 回填 → ...
      Tools/
        GetCharacterCardTool.cs
        GetWorldRulesTool.cs
        ListChaptersTool.cs

MuseSpace.Domain/
  Entities/
    AgentRun.cs                // Id, AgentName, UserId, ProjectId, InputHash,
                               //   Status, StepCount, TokensInput, TokensOutput,
                               //   CostUsd, StartedAt, FinishedAt, ErrorMessage

MuseSpace.Infrastructure/
  Persistence/
    Configurations/AgentRunConfiguration.cs
    Migrations/202604xx_AddAgentRun.cs
```

**前端**：暂无需要。

**验收**：
- [ ] 新 `IAgentRunner` 可执行最小 echo agent（system: "回复 hi" + 0 工具）
- [ ] 把现有「角色卡提取」迁移成 `CharacterExtractAgent`（最少破坏改造）
- [ ] DB 中能看到 `AgentRun` 行
- [ ] 有 `Agents:Enabled` feature flag（appsettings）

**风险**：迁移角色卡功能时不能改变对外 API；先并行运行（旧实现 + 新 Agent），灰度切换。

---

### 🟦 P1 — 一致性守护（一周）

**目标**：让"已存在但沉默"的 `WorldRule` 和 `Character` 真正介入生成，提升用户对 AI 输出的信任。

#### P1.1 世界观一致性 Agent（worldrule-guardian）

- **触发**：草稿生成完成后异步触发（Hangfire 任务）
- **工具**：`GetWorldRulesTool(projectId)`、`HighlightConflictTool(spanText, ruleId, suggestion)`
- **输出**：`{ conflicts: [{ ruleId, span, severity, suggestion }] }` 写入 `Scene.Annotations`（新字段或新表 `SceneAnnotation`）
- **前端**：草稿结果区下方"一致性检查"折叠面板，红色高亮冲突段落
- **降级**：若 Agent 失败，前端展示"本次未运行"，不影响已生成草稿

#### P1.2 角色一致性 Agent（character-consistency）

- **触发**：同上 + 角色卡更新时**反查**最近 N 章
- **工具**：`GetCharacterCardTool(characterId)`、`SearchChapterByCharacterTool`
- **输出**：相同结构的 annotation
- **复用**：Schema 与 P1.1 共享，UI 共用一个面板

**验收**：
- [ ] 一篇带 3 条故意冲突的测试文本能被正确识别 ≥2 条
- [ ] 误报率人工评估 < 30%（首版可接受）
- [ ] Agent 失败不阻断草稿生成主流程
- [ ] 用户可在项目设置里关闭

---

### 🟦 P2 — 创作起点辅助（一周）

#### P2.1 大纲规划 Agent（outline-planner）

- **触发**：新建 `Novel` 后向导式调用
- **输入**：题材、主角、核心冲突、目标字数、主要参考作品
- **流程**：三幕 → 卷 → 章纲（多步生成 + 自审）
- **输出**：JSON 结构 → 一键写入 `Chapters` 表（带"草稿"状态，用户可编辑）
- **工具**：`SaveChaptersTool(novelId, chapters[])`、`GetReferenceStyleTool(profileId)`

**验收**：
- [ ] 输入"修真 / 男频 / 100 万字"能产出 ≥3 卷 ≥30 章纲要
- [ ] 一键导入后章节树正确呈现
- [ ] 可重新生成单卷而不影响其他卷

---

### 🟦 P3 — 长篇线索追踪（1.5~2 周，含新数据模型）

#### P3.1 伏笔/线索追踪 Agent（foreshadow-tracker）

**新增数据模型**：
```csharp
PlotThread {
  Id, NovelId, Tag, Description,
  PlantedChapterId, PlantedSpan,
  ResolvedChapterId?, ResolvedSpan?,
  Status: Open | Resolved | Abandoned,
  CreatedBy: User | Agent
}
```

**触发**：
- 草稿生成时识别伏笔 → 建议建卡（用户确认）
- 写新章节时主动提醒"你有 N 条未回收"
- 章节标记为完结时扫描全本未回收项

**工具**：`ListOpenThreadsTool`、`CreateThreadTool`、`MarkResolvedTool`、`SearchChapterTextTool`

**前端**：
- 项目侧栏新增"线索板"
- 草稿生成结果页悬浮"识别到 N 条新伏笔"按钮

**验收**：
- [ ] 端到端：埋伏笔 → 自动建卡 → 写到回收章节 → 自动提示可关闭
- [ ] 新数据模型有完整迁移与回滚 SQL

---

### 🟦 P4 — 精修与改写（1 周）

#### P4.1 风格匹配 Reviewer（style-profile-reviewer）
- **流程**：草稿写出 → reviewer 与 `StyleProfile` 对比（句长 / 拟声词 / 对话比） → 给 rewrite 建议 → 用户可一键 apply

#### P4.2 场景改写工具集（scene-reviser）
- **触发**：选中文本 → 右键菜单
- **工具**：`加强氛围` / `增加对白` / `放慢节奏` / `提高爽点` / `精简` 5 个 sub-agent，每个独立 prompt
- **复用**：可作为 P4.1 reviewer 给出建议后的"应用"动作

**验收**：
- [ ] 选中段落 → 5 个改写选项 → 替换 / 撤销均正常

---

### 🟦 P5 — 长期价值（每项 2~5 天，可独立按需启动）

| 子项 | 简述 |
|---|---|
| P5.1 读者视角 Agent | 模拟读者读完一章，输出情绪曲线 / 弃书点 / 期待 |
| P5.2 灵感素材 Agent | `data/files/raw/` 素材自动打标 + RAG 检索（接 pgvector） |
| P5.3 命名 Agent | 人名 / 地名 / 招式名（轻量，不一定走 Agent，可降级为 Skill） |
| P5.4 发布前体检 Agent | 错别字、AABB 重复、的地得、敏感词；多 tool 并行 |
| P5.5 Agent 维度的模型偏好 | `UserLlmPreference` 扩展为「按 AgentName 选模型」 |
| P5.6 用户自定义 Agent | 把 `AgentDefinition` YAML 化，UI 可视化配置（远期） |

---

## 3. 横向能力（贯穿所有阶段）

### 3.1 可观测
- `AgentRun` 表：每次执行落一行（id、name、user、tokens、cost、status、duration）
- Serilog Enrich：`AgentName` / `AgentRunId` 进入每条日志
- 后台管理页（admin）新增「Agent 监控」：成功率、平均 token、Top 失败原因

### 3.2 成本与限流
- 配置 `Agents:CostBudget:DailyUsdPerUser`（普通用户默认 $1/天）
- 超额触发降级：自动切便宜模型 / 排队等明天 / 提示用户升级
- Hangfire 上加 Agent 队列，限制并发

### 3.3 Feature Flag
- `Agents:Enabled`（总开关）
- `Agents:<AgentName>:Enabled`（单 Agent 开关）
- `Agents:<AgentName>:Model`（强制指定模型，用于灰度新模型）

### 3.4 测试策略
- 单元测试：mock `ILlmClient` 验证 tool 调度逻辑
- 录制重放：把真实 LLM 调用录成 fixture，CI 用 fixture 跑 → 零成本回归
- 评估集：每个 Agent 维护 5~10 条人工标注 case，PR 时跑分

---

## 4. 何时**不**用 Agent（反模式）

| 场景 | 应该用 |
|---|---|
| 单轮 prompt → 单轮输出（命名、改一句话） | `ISkill` 即可 |
| 纯规则匹配（敏感词、错别字典） | 普通 Service，不要套 LLM |
| 用户能直接搜的（章节标题搜索） | 普通查询 |
| 需要严格事务（写入计费） | 普通 Service，Agent 只产建议 |

**判断口诀**：能不调 LLM 就不调；能 1 次调用搞定就别多步；多步必须证明"单次不够"。

---

## 5. 当前进度跟踪

> 每完成一项，请在此表更新状态与 PR 链接。

| 阶段 | 任务 | 状态 | 备注 / PR |
|---|---|---|---|
| **盘点** | 现有 Skill / Provider / Pref 体系梳理 | ✅ 已完成 | 见第 0 章 |
| **盘点** | 角色卡 Agent 雏形已上线 | ✅ 已完成 | 待 P0 重构归一 |
| P0 | `IAgentRunner` + `AgentDefinition` + `IAgentTool` 抽象 | ✅ 已完成 | 2026-04-24 |
| P0 | `AgentRun` 实体 + 迁移 `20260424090617_AddAgentRun` | ✅ 已完成 | 2026-04-24 |
| P0 | 角色卡 Agent 迁移到新运行时 | ✅ 已完成 | `CharactersController` 改用 `IAgentRunner` |
| P0 | Feature Flag 框架 (`Agents:Enabled`) | ✅ 已完成 | appsettings 配置即可 |
| P1.1 | 世界观一致性 Agent | ⬜ 未开始 | |
| P1.2 | 角色一致性 Agent | ⬜ 未开始 | |
| P1.x | 草稿页一致性面板 UI | ⬜ 未开始 | |
| P2.1 | 大纲规划 Agent | ⬜ 未开始 | |
| P2.x | 新建小说向导 UI | ⬜ 未开始 | |
| P3.1 | `PlotThread` 实体 + 迁移 | ⬜ 未开始 | |
| P3.1 | 伏笔追踪 Agent | ⬜ 未开始 | |
| P3.x | 线索板 UI | ⬜ 未开始 | |
| P4.1 | 风格 Reviewer | ⬜ 未开始 | |
| P4.2 | 场景改写工具集 | ⬜ 未开始 | |
| P5.x | 长期价值各子项 | ⬜ 未开始 | 按需启动 |
| 横向 | Admin Agent 监控页 | ⬜ 未开始 | 建议 P0 完成后立即跟进 |
| 横向 | 录制重放测试基建 | ⬜ 未开始 | |

图例：⬜ 未开始 / 🟨 进行中 / ✅ 已完成 / ⛔ 已废弃

---

## 6. 可复用 / 可参考资源清单

### 6.1 项目内可直接复用
- `LlmProviderSelector`（Scoped 用户级模型偏好） — 所有 Agent 共用
- `SkillOrchestrator` — 借鉴其 DI 自动收集 + 路由表模式
- `SceneDraftSkill` — 标准的"读取 prompt 模板 + 调 LLM + 解析输出"样板
- `LlmPreferenceMiddleware` — 借鉴"按 JWT 用户填充 Scoped 服务"模式
- Hangfire `IBackgroundJobClient` — 长 Agent 异步化
- SignalR Hub — Agent 进度推送
- pgvector + `SiliconFlowEmbeddingClient` — RAG 工具底座
- Serilog + `logs/app-yyyyMMdd.log` — 运行轨迹

### 6.2 项目内可参考但需改造
- 角色卡提取实现 — 重构为 P0 样板 Agent
- `prompts/drafting/scene-v1.md` 提示词管理约定 — Agent 的 system prompt 也走同一目录约定（建议 `prompts/agents/<agent-name>-v1.md`）

### 6.3 外部可参考的设计
- OpenAI / Anthropic tool_use 协议 — `IAgentTool` 的 JSON Schema 直接对齐
- LangGraph / Semantic Kernel 的 step / planner 概念 — 仅作思路参考，**不引入依赖**
- Cline / Cursor 的 "approve-each-step" 模式 — 用户敏感操作（写入数据）的交互范式

---

## 7. 决策记录（ADR 简版）

| 编号 | 决策 | 时间 | 理由 |
|---|---|---|---|
| ADR-001 | 不引入 Semantic Kernel / LangChain.NET | 2026-04-24 | 现有抽象层够用，避免依赖锁定 |
| ADR-002 | Agent 运行时放在 `Application` 层而非新建项目 | 2026-04-24 | 与 Skill 同层，便于复用契约 |
| ADR-003 | `AgentRun` 持久化到主库而非独立库 | 2026-04-24 | 量级可控，避免跨库事务 |
| ADR-004 | 一致性 Agent 失败**不阻塞**主流程 | 2026-04-24 | 副作用功能必须可降级 |

> 后续每个重要决策追加一行，避免重复讨论。

---

## 8. 文档维护

- 每完成一个 PR，更新「第 5 章 进度跟踪」对应行
- 新增 Agent → 在第 6.1 节登记
- 阶段切换时（如 P0 → P1 启动）在文档顶部加变更日志小节

---

**最近更新**：2026-04-24 P0 已完成（Agent 运行时 + 角色卡迁移 + AgentRun 持久化 + Feature Flag）
