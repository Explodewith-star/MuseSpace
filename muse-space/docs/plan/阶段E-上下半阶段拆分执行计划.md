# 阶段E：上下半阶段拆分执行计划

> 定位：在阶段 C+ 生成质量治理和阶段 D Agent 主干稳定后，将原阶段 E 拆成“长期记忆网络 MVP”和“高级检索与工业化扩展”两段推进。
>
> 核心判断：阶段 E 不应作为一次大重构启动。上半阶段先用 PostgreSQL / pgvector 落地可信、可追溯、可参与生成的长期记忆网络；下半阶段再根据评估结果和真实负载决定是否引入 GraphRAG、Neo4j、Redis、对象存储和独立 Worker。

---

## 1. 拆分目标

### E-上：长期记忆网络 MVP

目标：用现有技术栈把长期记忆、事实、事件、建议、大纲和伏笔组织起来，让系统获得“可信、可追溯、可控注入”的长期记忆能力。

本阶段默认不引入：

- Neo4j
- 正式 GraphRAG
- Redis
- 对象存储
- 独立 Worker
- Multi-Agent 编排

### E-下：高级检索与工业化扩展

目标：在 E-上验证有效后，再用评估集驱动 Hybrid / rerank / GraphRAG-lite / Neo4j 试点，并按真实压力引入工业化组件。

---

## 2. 总体原则

1. **先可信，再聪明**
   长期记忆先解决来源、作用域、状态、可见性和回溯，不急着追求复杂推理。

2. **先 PostgreSQL，后 Neo4j**
   第一版实体关系和记忆关系继续落在 PostgreSQL。只有多跳查询、图遍历和关系维护成本明显失控时，才评估 Neo4j。

3. **先 GraphRAG-lite，后正式 GraphRAG**
   先在现有表上做关系辅助检索。只有评估样例证明收益明显，才进入正式 GraphRAG 流程。

4. **不推翻 C+ 边界**
   `ChapterDraftScope`、来源隔离、`DraftVerifier` 和大纲级模式是后续记忆网络的输入边界，不应被阶段 E 绕开。

5. **所有新能力必须可降级**
   长期记忆、关系检索、高级检索都必须能通过 FeatureFlag 或配置关闭，并回退到现有生成链路。

---

## 3. 阶段 E-上：长期记忆网络 MVP

### E-A0 准入回归

目标：确认进入长期记忆前，主链路不会把脏上下文沉淀成长期资产。

任务：

- 回归原创第一章越界问题。
- 回归跨大纲污染问题。
- 回归原创模式默认不注入原著语义片段。
- 回归保存前验收：坏草稿不能写入 `Chapter.DraftText`。
- 确认 `StoryContextBuilder` 只读取当前大纲和当前章节之前可见的信息。
- 确认后端单元测试和前端构建通过。

验收：

- C+ 生产问题样例不再复现。
- `ChapterDraftScopeBuilder` / `DraftVerifier` 相关测试通过。
- 长期记忆建设可以基于现有事实、事件、建议和大纲继续推进。

#### 2026-05-11 回归进度记录

状态：部分通过，Live UI / API 样例待数据库 SSH 隧道恢复后续跑。

已完成：

- 已用 Chrome DevTools MCP 打开本地前端：`http://127.0.0.1:5173/projects`。
- 已确认本地后端端口可达：`https://localhost:7126` / `http://localhost:5142`。
- 已确认 `/api/llm-provider` 返回 200，前端代理到后端链路可用。
- 已复核 E-A0 关键单测：
  - `ChapterDraftScopeBuilderTests`
  - `DraftVerifierTests`
  - `StoryContextBuilderTests`
- 命令：`dotnet test muse-space/tests/MuseSpace.UnitTests/MuseSpace.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ChapterDraftScopeBuilderTests|FullyQualifiedName~DraftVerifierTests|FullyQualifiedName~StoryContextBuilderTests"`
- 结果：15 passed / 0 failed / 0 skipped。
- 已复核前端构建：`npm run build` 通过。

已覆盖的回归点：

- 原创第一章越界：`ChapterDraftScopeBuilderTests.Build_InfersForeshadowForFirstChapterAndAllowsManualOverride` / `Build_InfersForeshadowForOminousFirstChapter` 覆盖首章默认只允许伏笔级揭示。
- 跨大纲污染：`StoryContextBuilderTests.BuildAsync_FiltersFactsAndEventsToCurrentOutline` 覆盖事实与事件只读当前大纲。
- 原创模式默认不注入原著语义片段：`StoryContextBuilderTests.BuildAsync_OriginalDraft_DoesNotRetrieveNovelSnippetsByDefault` 覆盖 `IncludeNovelContext=false` 时不检索原著片段。
- 当前章节只读前序可见信息：`StoryContextBuilderTests.BuildAsync_FiltersSummariesEventsAndFactsToPriorChaptersOnly` 与 `BuildAsync_UsesMostRecentPriorEventChaptersInsteadOfProjectLatest` 覆盖未来章节摘要、事件、事实不进入上下文。
- 坏草稿保存前验收：`DraftVerifierTests` 覆盖未来章节泄露、揭示等级超限、原创来源策略违规；`ChapterDraftJob` 代码路径已确认二次验收失败时直接 fail，不写入 `Chapter.DraftText`。

#### 2026-05-11 Live 回归续跑（完成）

状态：**全部通过**，E-A0 验收完成。

环境：

- 远端数据库直连 `152.136.11.140:6286` 可达，后端以 `https` profile 启动（7126 + 5142）。
- 前端 `http://127.0.0.1:5173` 正常，代理 7126 链路通畅，`/api/projects` 返回 200。

Live 回归过程：

- 创建临时项目 `[E-A0] 原创回归测试`（Mode=Original，无导入原著）。
- 创建大纲 `E-A0测试大纲`（Mode=Original），创建第1章和第2章。
- 为第2章录入 ChapterEvent（未来事件）和 CanonFact（SourceChapterId=第2章）。
- API 验证：
  - `/api/.../chapters/{ch1}/events` 返回 0 条（第2章事件未泄露至第1章）✓
  - `/api/.../canon-facts` 的 SourceChapterId 正确指向第2章 ✓
  - 第1章 DraftText 为空、Status=0（草稿未写入）✓
- 前端验证（`http://127.0.0.1:5173` Chrome DevTools MCP）：
  - 第1章详情页加载无报错 ✓
  - 揭示等级显示"自动推断"（ChapterDraftScopeBuilder 首章默认边界生效）✓
  - 所属大纲显示"原创主线"（无原著片段注入路径）✓
  - 草稿区显示"章节计划已就绪，点击生成草稿"（无脏草稿写入）✓
  - 章节事件区显示"暂无事件"（第2章事件未泄露）✓
- 临时项目已清理（DELETE 返回 success=true）。

已覆盖的回归点（Live 层）：

| 回归点 | 状态 |
|---|---|
| 原创首章边界（揭示等级自动推断） | ✓ Live 确认 |
| 跨大纲污染（第2章事件/事实不泄露至第1章） | ✓ Live 确认 |
| 原创模式默认不注入原著语义片段 | ✓ 大纲 Mode=Original，无原著导入路径 |
| 保存前验收失败不落草稿 | ✓ DraftText=空，未有脏草稿写入（单测已覆盖写入路径）|
| `/api/projects` 主链路正常 | ✓ 200 |
| 前端构建通过 | ✓ 已于首轮确认 |

结论：E-A0 验收全部通过，可继续推进 E-A1 MemoryItem 最小模型。

### E-A1 MemoryItem 最小模型

目标：建立长期记忆的最小数据模型。

建议实体：

```text
memory_items
- Id
- StoryProjectId
- StoryOutlineId?
- SourceType
- SourceId?
- MemoryType
- Title
- Content
- Summary
- Importance
- Confidence
- Status
- VisibilityScope
- EffectiveFromChapterNumber?
- EffectiveToChapterNumber?
- CreatedBy
- CreatedAt
- UpdatedAt
```

第一版 `MemoryType`：

```text
CanonFact
ChapterEvent
AppliedSuggestion
Outline
Character
WorldRule
PlotThread
NovelSnippet
Manual
```

第一版 `Status`：

```text
Candidate
Confirmed
Disabled
Deprecated
```

第一版 `VisibilityScope`：

```text
Project
Outline
BeforeChapter
ReviewOnly
```

任务：

- 新增 Domain 实体。
- 新增 EF 配置和迁移。
- 新增 Repository 接口与实现。
- 新增最小 API：列表、详情、创建、更新、禁用/废弃、删除。
- 保证项目级、大纲级、类型、状态可过滤。

验收：

- 可以保存和查询一条长期记忆。
- 每条长期记忆都能追溯来源。
- 可以按项目、大纲、类型、状态过滤。

### E-A2 现有资产回填长期记忆

目标：先从“相对可信”的既有资产生成长期记忆，不做全量 chunk 自动摘要。

优先来源：

- `CanonFact`
- `ChapterEvent`
- 已应用的 `AgentSuggestion`
- 已应用的大纲章节
- 手动确认的角色、世界观、文风设定
- `PlotThread`

任务：

- 新增 `BuildMemoryItemsJob` 或等效后台任务。
- 支持按项目回填。
- 支持按来源类型回填。
- 设计幂等键，避免重复执行无限创建重复记忆。
- 被忽略、废弃、未应用的建议默认不进入长期记忆。
- 自动生成的记忆默认可设为 `Candidate`，人工确认后变为 `Confirmed`。

验收：

- 能一键为某个项目生成第一批长期记忆。
- 重复执行不会创建重复项。
- 已废弃或忽略的建议不会进入长期记忆。
- 记忆项能指回原始事实、事件、建议、大纲或线索。

### E-A3 MemoryLink 轻量关系层

目标：在 MemoryItem 之间建立可解释关系，为后续实体关系和 GraphRAG-lite 做铺垫。

建议实体：

```text
memory_links
- Id
- StoryProjectId
- FromMemoryItemId
- ToMemoryItemId
- LinkType
- Strength
- Note
- CreatedAt
```

第一版 `LinkType`：

```text
Supports
Contradicts
DerivedFrom
RelatedTo
Foreshadows
Resolves
Updates
```

任务：

- 新增 Domain 实体。
- 新增 EF 配置和迁移。
- 新增 Repository 接口与实现。
- 支持手动创建关系。
- 回填时自动创建部分确定性关系，例如事实 derived from 事件、伏笔 resolves 回收事件。

验收：

- 一个伏笔记忆可以关联到回收事件。
- 一个事实可以关联到来源章节事件。
- 一个建议可以关联到应用后的大纲、角色卡、世界观规则或风格设定。

### E-A4 长期记忆检索服务

目标：新增 `IMemorySearchService`，让生成链路可以安全、受预算地读取长期记忆。

第一版能力：

- 按项目检索。
- 按大纲过滤。
- 按章节可见性过滤。
- 按类型过滤。
- 按状态过滤，默认只取 `Confirmed`，必要时可取 `Candidate`。
- 按关键词检索。
- 可选接入 embedding，但不替代现有 `NovelMemorySearchService`。

生成注入建议：

```text
## 长期记忆参考
```

预算建议：

- 第一版最多注入 5 条。
- 单条不超过 300 字。
- 总预算不超过 1500 字。
- 永远不能绕过 `ChapterDraftScope`。

任务：

- 新增 `IMemorySearchService`。
- 在 `StoryContextBuilder` 中新增长期记忆段。
- 长期记忆注入受 FeatureFlag 控制。
- 记录检索日志：召回数、注入数、预算、过滤条件。

验收：

- 当前章节看不到未来章节才产生的记忆。
- 原创模式不会因为长期记忆绕过原著来源策略。
- 关闭 FeatureFlag 后恢复现有生成链路。
- 生成记录能追溯本次注入了哪些长期记忆。

### E-A5 记忆中心前端

目标：提供一个朴素但可用的长期记忆管理入口。

功能：

- 记忆列表。
- 类型筛选。
- 大纲筛选。
- 状态筛选。
- 来源查看。
- 手动新增。
- 手动编辑。
- 启用 / 禁用。
- 废弃 / 删除。
- 回填任务入口。

暂不做：

- 图谱可视化。
- 拖拽关系编辑器。
- 复杂关系推理解释面板。

验收：

- 用户能看到系统沉淀了哪些长期记忆。
- 用户能判断每条记忆来自哪里。
- 用户能禁用错误记忆，避免进入生成上下文。

### E-A6 PlotThread 深化

目标：不重做现有伏笔追踪系统，而是把它接入长期记忆网络。

任务：

- PlotThread 关联 MemoryItem。
- PlotThread 关联 ChapterEvent。
- PlotThread 关联 CanonFact。
- PlotThread 与大纲章节建立映射。
- 生成上下文能读取当前章相关伏笔。
- 过期伏笔可以进入生成提醒或待办建议。

验收：

- 一条伏笔从埋设到回收能串起章节、事件、事实和长期记忆。
- 当前章生成时可以看到与本章相关、且在可见范围内的伏笔提醒。
- PlotThread 深化不破坏现有看板和 Agent 扫描能力。

---

## 4. 阶段 E-上完成定义

阶段 E-上完成时，系统应具备：

- 长期记忆表可用。
- 现有事实、事件、建议、大纲、伏笔能沉淀为长期记忆。
- 生成上下文可以安全读取长期记忆。
- 用户能在前端管理长期记忆。
- PlotThread 与记忆、事件、事实有基本连接。
- 不引入 Neo4j / GraphRAG / Redis / Worker。

---

## 5. 阶段 E-下：高级检索与工业化扩展

### E-B1 评估集建设

目标：先建立评估，再改检索。

评估维度：

- 复杂人物关系。
- 伏笔跨多章回收。
- 多大纲隔离。
- 原著续写一致性。
- 世界观规则冲突。
- 批量章节生成质量。

任务：

- 扩展现有 C-4 评估样例。
- 新增长期记忆 / 实体关系 / 伏笔追踪评估样例。
- 记录普通检索、长期记忆检索、GraphRAG-lite 的对比结果。

验收：

- 每次检索策略变化，都能用同一批样例对比。
- 能判断新方案是否真的更好，而不是只凭主观感觉推进。

### E-B2 Hybrid / Rerank

目标：在 PostgreSQL 体系内增强检索，不先上图数据库。

候选策略：

```text
向量召回
+ 关键词召回
+ 类型过滤
+ 时间 / 章节窗口
+ 重要度加权
+ rerank 可选
```

任务：

- 扩展 `IMemorySearchService` 排序策略。
- 支持关键词和向量混合召回。
- 支持重要度、可信度、章节距离、来源类型加权。
- 建立调试日志，方便比较不同策略。

验收：

- 相比 E-A 检索，召回更准。
- 注入上下文没有明显变长失控。
- 成本可控。

### E-B3 GraphRAG-lite

目标：先在现有 PostgreSQL 表上做关系辅助检索。

流程：

```text
用户目标 / 当前章节
-> 找相关 MemoryItem
-> 沿 MemoryLink / PlotThread / CanonFact 扩展一跳或两跳
-> 合并排序
-> 注入生成上下文
```

任务：

- 新增关系扩展检索策略。
- 支持一跳扩展。
- 支持可配置的二跳扩展。
- 支持按 LinkType 限制扩展范围。
- 把扩展路径写入调试日志或生成记录。

验收：

- 对复杂伏笔、人物关系、世界观规则的召回优于普通向量检索。
- 有评估数据证明提升。
- 关系扩展不会把未来章节信息泄漏给当前章节。

### E-B4 Neo4j 试点

目标：只有 PostgreSQL 关系查询明显吃力时才做 Neo4j 试点。

适合试点的子域：

- 复杂人物关系。
- 组织 / 阵营关系。
- 伏笔链路。
- 世界观规则依赖。

任务：

- 设计 `IEntityGraphService` 或等效抽象。
- PostgreSQL 实现作为默认实现。
- Neo4j 实现作为可选 adapter。
- 只迁移一个子域做试点，不做全系统迁移。
- 设计失败回退路径。

验收：

- PostgreSQL 与 Neo4j 通过接口隔离。
- Neo4j 失败时系统能回退 PostgreSQL。
- 能证明 Neo4j 的查询维护成本或效果优于现有方案。

### E-B5 正式 GraphRAG

目标：在图层和评估集都稳定后，再做正式 GraphRAG。

完整流程：

```text
实体抽取
-> 关系抽取
-> 社区 / 子图构建
-> 查询时子图召回
-> 上下文压缩
-> 生成
-> 评估
```

任务：

- 明确 GraphRAG 适用的项目类型和任务类型。
- 建立索引任务和成本记录。
- 设计上下文压缩策略。
- 为 GraphRAG 增加 FeatureFlag。
- 只在适合的项目 / 模式开启。

验收：

- 明确知道 GraphRAG 提升了什么。
- 明确知道额外成本是多少。
- 没有评估收益时不进入默认生成链路。

### E-B6 工业化组件

目标：按真实痛点引入 Redis、对象存储、独立 Worker 和成本治理。

触发条件：

- 批量生成明显拖慢 API。
- Embedding / 摘要任务排队严重。
- 文件存储需要跨机器协作。
- 管理后台需要更强审计。
- Agent 成本需要统计、限流、预算控制。

候选组件：

- 独立 Worker：承接 embedding、摘要、批量 Agent。
- Redis：缓存、限流、短期任务状态。
- 对象存储：替代本地文件。
- 成本面板：统计 LLM / embedding 调用。

验收：

- API 进程从重型异步任务中解耦。
- 文件和缓存体系适应多人协作与更大数据量。
- 成本、重试、失败和限流可观测。

---

## 6. 推荐实际开工顺序

1. E-A0：C+ / D 回归确认。
2. E-A1：MemoryItem 模型。
3. E-A2：从 CanonFact / ChapterEvent / AppliedSuggestion 回填。
4. E-A4：长期记忆检索服务。
5. E-A5：记忆中心前端。
6. E-A3：MemoryLink。
7. E-A6：PlotThread 深化。
8. E-B1：评估集。
9. E-B2：Hybrid / rerank。
10. E-B3：GraphRAG-lite。
11. 根据真实痛点决定是否进入 E-B4 / E-B5 / E-B6。

---

## 7. 当前不建议立即做的事

| 项 | 原因 |
|---|---|
| 直接上 Neo4j | 当前还没有证明 PostgreSQL 关系表无法支撑第一版关系网络。 |
| 直接上正式 GraphRAG | 缺少实体关系层、评估集和成本闭环，容易只增加复杂度。 |
| 全量 chunk 自动摘要 | 成本高，且容易把低价值内容沉淀为长期噪声。 |
| Redis / 对象存储 / 独立 Worker | 只有真实负载和协作痛点出现后再引入。 |
| 多 Agent 编排 | 当前单 Agent + 工具链仍能支撑菜单 Agent 化和主流程。 |
| 大改 `StoryContextBuilder` | 阶段 E 应作为新层受控接入，不应推翻 C+ 已稳定的生成边界。 |

---

## 8. 与原阶段 E 文档的关系

原 [`阶段E-记忆网络深化与工业化扩展任务清单.md`](./阶段E-记忆网络深化与工业化扩展任务清单.md) 继续作为阶段 E 的总目标清单。

本文档作为执行拆分版，用于指导实际开发顺序：

- E-上承接原 E-1 / E-3 / E-3.5 的轻量版本。
- E-下承接原 E-2 / E-4 / E-5 中更重的实体关系、高级检索和工业化部分。
- Neo4j / GraphRAG 仅作为 E-下的评估后选项，不作为 E-上的前置依赖。
