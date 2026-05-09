# 阶段 C+：一致性补强与创作闭环任务清单

> 阶段定位：阶段 C / D 主干已完成、阶段 E 工业化扩展尚不必启动的中间过渡。
>
> 阶段目标：
>
> 1. 解决"AI 生成草稿越界 / 编造大纲外事件"的前后不一致问题（不引入知识图谱）。
> 2. 补齐"草稿 → 定稿 → 导出"的创作闭环，让用户不再依赖手动复制粘贴和外部工具。
>
> 当前状态（2026-05-09）：生产止血与工程化根治主干已落地，已从 Prompt 边界硬化升级为“来源隔离 + 章节范围契约 + 保存前验收 + 大纲级模式 + 事实/事件大纲隔离”的治理闭环。本清单后续作为回归、优化和阶段 E 衔接依据。

## 0.1 2026-05-09 生产问题复盘与方向修正

### 0.1.1 问题背景

用户反馈章节草稿与章节规划严重不符：第一章规划只允许宿舍/校园日常与异常前兆，但生成内容提前进入后续阶段，出现门后世界、地下室、鬼域、祭坛、实体鬼、死亡/战斗等信息量。

复盘后确认有两类根因：

- **来源污染**：原创章节生成时，原著语义片段、角色末态、后续章节摘要等上下文可能被模型当作当前章剧情来源。
- **章节边界缺少保存前验收**：即使 Prompt 中声明“只写本章”，模型越界后仍会写入 `Chapter.DraftText`，进而污染后续章节的历史上下文。

### 0.1.2 已完成止血

- 默认原创草稿生成不再注入项目级原著语义片段，除非用户明确启用 `IncludeNovelContext`。
- `StoryContextBuilder` 在知道当前章节时，只取编号小于当前章的章节摘要、事件与事实。
- `SceneDraftSkill` 返回渲染后的 Prompt，`GenerationRecord.InputPreview` 记录实际输入，便于追溯污染来源。
- 第一版章节边界守卫已接入 `ChapterDraftJob`：生成后检测明显越界内容，失败自动重试 1 次，仍失败则不保存坏草稿。

### 0.1.3 方向修正

止血守卫只作为生产兜底，长期方案不依赖指定小说的提示词或硬编码禁词。后续改为：

```text
生成前：构建 ChapterDraftScope，隔离来源可见性
生成中：只给模型当前章允许使用的内容
生成后：DraftVerifier 结构化验收，通过才保存
失败时：自动修订 1 次；仍失败则记录原因，不写入草稿
```

同时，原创 / 续写 / 支线番外 / 扩写改写不再视为“单章生成选项”的长期设计，而应升级为“大纲/作品分支模式”。章节继承所属大纲的模式、来源策略与范围边界。

### 0.1.4 已完成工程化根治主干

- `StoryOutline` 已落库，支持项目下多个大纲分支；现有章节迁移到默认“原创主线”。
- `Chapter.StoryOutlineId` 与 `Chapter.AllowedRevealLevel` 已落库，章节编号、查询、导入、批量生成、导出均按大纲边界执行。
- `ChapterDraftScopeBuilder` 已接入 `ChapterDraftJob`，生成前统一推断揭示等级、来源策略、当前章计划与后续保留项。
- `DraftVerifier` 已作为保存闸门接入，失败自动重试 1 次，仍失败则任务失败且不保存坏草稿，不触发后续事实/事件抽取。
- `StoryContextBuilder` 已按大纲过滤已发生章节、事件与事实；后续章节只进入验证边界，不作为当前章已发生上下文。
- `ChapterEvent` 与 `CanonFact` 已补充 `StoryOutlineId`，重复事件检测、Canon 冲突检测、事实抽取、手动维护 API 均按大纲隔离。
- 前端已新增大纲选择、创建与导入目标选择；章节详情展示所属大纲，并支持章节揭示等级覆盖。

---

## 0. 阶段范围一览

| 模块 | 子任务 | 工期估算 | 依赖 |
|---|---|---|---|
| **C+0 生产止血与追溯** | C+0.1 ~ C+0.5 | 已完成 / 回归中 | 无 |
| **C+1 Prompt 边界硬化** | C+1.1 ~ C+1.5 | 已部分完成，后续保留为兜底 | C+0 |
| **C+2 章节事件时间线** | C+2.1 ~ C+2.5 | 3-5 天 | C+1 |
| **C+3 草稿越界反向校验** | C+3.1 ~ C+3.4 | 1-2 天 | C+1 |
| **C+7 章节范围契约与来源隔离** | C+7.1 ~ C+7.7 | 已完成 / 回归中 | C+0 |
| **C+8 保存前结构化验收** | C+8.1 ~ C+8.7 | 已完成 / 回归中 | C+7 |
| **C+9 大纲级创作模式重构** | C+9.1 ~ C+9.8 | 已完成 / 回归中 | C+7 / C+8 |
| **C+4 草稿一键采用为定稿** | C+4.1 ~ C+4.3 | 0.5 天 | 无 |
| **C+5 批量章节草稿生成** | C+5.1 ~ C+5.6 | 2-3 天 | C+1 建议先做 |
| **C+6 定稿一键导出** | C+6.1 ~ C+6.5 | 1-2 天 | 无 |

每个模块都可独立上线、独立验证。2026-05-09 后 C+7 / C+8 / C+9 主干已完成，后续优先级调整为：生产样例回归 → UI 细节优化 → 评估样例沉淀 → 再考虑 C+5 批量生成规模放大。

---

## 0.5 C+0 生产止血与追溯（2026-05-09 已完成 / 回归中）

> 定位：先阻断坏草稿继续污染数据库和后续章节，同时为根治方案提供真实输入追溯。

### 0.5.1 任务清单

#### C+0.1 原创模式默认关闭原著语义注入

- `GenerateChapterDraftRequest.IncludeNovelContext` 默认 false。
- `StoryContextBuilder.GetNovelContextSnippetsAsync` 在 `IncludeNovelContext=false` 时直接跳过原著召回。
- 原创模式下，原著内容不能成为剧情来源；只有续写/番外/扩写改写等明确模式才可按来源策略使用。

#### C+0.2 上下文仅注入已发生章节

- 当前章节已知时，`RecentChapterSummaries`、事件时间线、不可重复事实只取 `Number < CurrentChapterNumber`。
- 后续章节不进入生成可见上下文，只能进入验证器作为保留边界。

#### C+0.3 Prompt 与生成记录可追溯

- `SkillResult.RenderedPrompt` 携带渲染后的 system/user prompt。
- `GenerationRecord.InputPreview` 优先保存渲染 Prompt 的截断版本。
- 用于追查“模型到底看到了什么”，避免只凭最终草稿猜测来源。

#### C+0.4 第一版章节边界守卫

- 新增章节边界守卫，在保存前检测当前章规划外的强揭示/后续阶段内容。
- 失败时自动重试 1 次；重试仍失败则任务失败，不保存坏草稿。
- 该守卫仅作为临时兜底，后续由 C+7/C+8 的结构化方案替代主逻辑。

#### C+0.5 前端入口降风险

- 临时隐藏或弱化容易造成来源污染的“当前章节参考片段”入口。
- 后续重新启用时，必须带参考目的、参考强度与来源策略，不允许作为无标签剧情来源直接注入。

### 0.5.2 验收

- 原创第一章生成 Prompt 中不出现未显式启用的原著语义片段。
- 第一章生成时不会把第二/三章摘要作为“已发生”上下文注入。
- 明显越界草稿不会写入 `Chapter.DraftText`。
- 生成记录能看到当次渲染 Prompt 的主要内容。

---

## 1. C+1 Prompt 边界硬化（第一波，立竿见影）

> 解决用户反馈的 "列了 15 章大纲，AI 在生成第 3 章时却写了大纲里没发生的事情" 的主要原因。

### 1.1 任务清单

#### C+1.1 修正 `RecentChapterSummaries` 取章窗口

锚点：[`StoryContextBuilder.cs`](../../src/MuseSpace.Infrastructure/Story/StoryContextBuilder.cs#L66-L73)

- `StoryContextRequest` 新增可选 `int? CurrentChapterNumber`。
- 当前实现按 `Number DESC` 取前 3 章——写中段章节时会把后续未发生章节摘要当成"已发生"塞进 Prompt。
- 改为：当 `CurrentChapterNumber` 有值时，过滤 `Number < CurrentChapterNumber`，再 `OrderByDescending(Number).Take(3)`。

#### C+1.2 `StoryContext` 新增三个边界字段

锚点：[`StoryContext.cs`](../../src/MuseSpace.Application/Abstractions/Story/StoryContext.cs)

新增：

- `string? CurrentChapterPlan`：本章大纲（标题 + 目标 + 摘要 + 必中要点）。
- `string? PreviousChapterRecap`：上一章末尾事件梗概（取 `Chapter.Summary` 即可）。
- `string? NextChapterPreview`：下一章开头事件梗概（保留边界，告诉模型 "这部分留给下一章"）。

#### C+1.3 `ChapterDraftJob` 拆分参数传递

锚点：[`ChapterDraftJob.cs`](../../src/MuseSpace.Infrastructure/Jobs/ChapterDraftJob.cs#L62-L86)

- 不再把章节信息全部塞进 `SceneGoal` 字符串。
- `SceneGoal` 仅保留场景核心目标；`CurrentChapterPlan / PreviousChapterRecap / NextChapterPreview` 通过 `SkillRequest.Parameters` 单独传递。
- `StoryContextBuilder` 装配时优先使用这些显式字段，未传时再回退当前行为。

#### C+1.4 改写 `scene-v1.md` Prompt 模板

锚点：[`scene-v1.md`](../../prompts/drafting/scene-v1.md)

- 在变量区块新增：
  - `## 本章大纲（强制边界）`
  - `## 上一章已发生`
  - `## 下一章保留（不要在本章写）`
- 规则段加入硬指令（用全角符号或加粗强调）：
  > **严格禁止：写入本章大纲范围之外的事件、人物转折、关键情节。本章只能推进"本章大纲"中列出的目标，不得提前写入"下一章保留"中的内容，不得改写"上一章已发生"中的事实。**

#### C+1.5 原著参考片段来源分区标注

锚点：同 `scene-v1.md`、`SceneDraftSkill.cs` 的 `novel_context` 装配位置。

- 把 `## 原著参考片段` 段开头改为：
  > 以下来自**原著小说**，仅作为**风格 / 语境参考**，**并非本作已发生情节**。不得直接借用其中事件作为本章已发生事实。

### 1.2 验收

- 复用 [`C-4-草稿生成评估样例集.md`](./C-4-草稿生成评估样例集.md) 跑一遍样例，对比启用前后越界次数。
- 至少有一个样例能直观看出 AI 不再写"大纲外事件"。

---

## 2. C+2 章节事件时间线（第二波，结构化记忆）

> 让 AI 在生成第 N 章时知道前 N-1 章具体发生过什么事，而不是只看模糊摘要。

### 2.1 任务清单

#### C+2.1 新表 `chapter_events`

字段：`Id / ChapterId / Order / EventText / Actors[] / Location? / TimePoint? / CreatedAt`。

- 复用现有 `SchemaInitializerHostedService` + `SchemaMigrationRunner.RunOnceAsync` 幂等建表模式，**不**走 EF Migrations。
- 仓储 `IChapterEventRepository`，提供按 ChapterId 列表查询、按 ProjectId + Number 范围查询、批量替换某章事件三类方法。

#### C+2.2 章节事件抽取 Job

新增 `ExtractChapterEventsJob`，在 [`ChapterDraftJob`](../../src/MuseSpace.Infrastructure/Jobs/ChapterDraftJob.cs#L107-L114) 现有链式入队尾部追加：

- 输入：刚生成完成的草稿文本 + 章节标题。
- 输出：3-8 条结构化事件（JSON），写入 `chapter_events`。
- 抽取 Agent 用最便宜的模型即可，单次 token 上限 600。

#### C+2.3 `StoryContextBuilder` 注入事件时间线

新增 `EventTimeline` 字段，装配策略：

- 当 `CurrentChapterNumber = N` 时，取所有 `Number < N` 的章节事件。
- 总字符预算 `EventTimelineCharBudget = 2000`。
- 超预算时分两段：**最近 3 章详细事件 + 远期章节摘要式列表**（每章只取 1 条最关键事件）。

#### C+2.4 Prompt 新段 `## 已发生事件时间线`

在 `## 本章大纲` 之上插入，并在规则段补充：
> "已发生事件时间线"中列出的所有事件视为**已固定事实**，不得改写、否认或与之矛盾。

#### C+2.5 章节详情页暴露事件清单

锚点：[`muse-space-web/src/views/projects/chapters/detail/index.vue`](../../../muse-space-web/src/views/projects/chapters/detail/index.vue)

- 章节详情侧栏新增"事件清单"区块，列出从该章抽取的事件。
- 支持手动新增/编辑/删除事件，便于用户修正 AI 抽取错误。
- 对未生成草稿的章节，事件清单为空但允许手动录入。

### 2.2 验收

- 写第 N 章时，Prompt 中能看到 `## 已发生事件时间线` 段且内容正确。
- 评估样例集中"前后矛盾、人物状态错位"类问题数量明显下降。

---

## 3. C+3 草稿越界反向校验（第三波，守护层）

> 即便 Prompt 失控，事后也有 Agent 扫出来给用户看；同时为后续判断"是否真要上 GraphRAG"提供数据依据。

### 3.1 任务清单

#### C+3.1 新增 `DraftOutlineComplianceAgent`

输入：本章草稿 + 本章大纲 + 上一章/下一章梗概。

输出 JSON 数组，每项：

```json
{ "type": "out-of-scope" | "contradicts-prev" | "skips-next-content",
  "excerpt": "草稿原文片段",
  "severity": "low" | "medium" | "high",
  "suggestion": "建议改写方向" }
```

#### C+3.2 链式触发

在 [`ChapterDraftJob`](../../src/MuseSpace.Infrastructure/Jobs/ChapterDraftJob.cs#L107-L114) 现有 `BackgroundJob.Enqueue` 序列尾部追加 `DraftOutlineComplianceJob`，与 StyleConsistency / CharacterConsistency / PlotThread 并列。

#### C+3.3 复用 `OutlineConsistency` 类目

- 结果落 `agent_suggestions`，`Category = OutlineConsistency`，`Source = "草稿越界"` 区分于现有"大纲世界观冲突"。
- 章节详情页与大纲页都能通过现有 `PendingSuggestionPanel` 看到。

#### C+3.4 FeatureFlag 包裹

- 新增 `feature.draft-outline-compliance.enabled`，默认 true。
- 异常或大量误报时可一键关闭。

### 3.2 验收

- 人工构造一段"草稿写了大纲外的事件"，能稳定被检测出来。
- 误报率可控（≤30%），否则需要先调 Prompt 再上线。

---

## 4. C+4 草稿一键采用为定稿（用户体验闭环 1）

> 用户当前痛点：AI 生成草稿后，需要手动选中、复制、粘贴到定稿框，再保存。

### 4.1 任务清单

#### C+4.1 后端接口

锚点：[`ChaptersController.cs`](../../src/MuseSpace.Api/Controllers/ChaptersController.cs)

新增：

```
POST /api/storyprojects/{projectId}/chapters/{chapterId}/adopt-draft
Body: { overrideExisting?: boolean }
```

行为：

- 加载 Chapter；若 `DraftText` 为空 → 400。
- 若 `FinalText` 已有内容且 `overrideExisting != true` → 409 Conflict + 返回现有定稿长度，让前端弹确认框。
- 否则 `FinalText = DraftText`，`Status = Final`（若当前 < Final）；`UpdatedAt` 刷新。
- 不清空 `DraftText`，保留对照能力。

#### C+4.2 前端按钮 + 二次确认

锚点：[`muse-space-web/src/views/projects/chapters/detail/index.vue`](../../../muse-space-web/src/views/projects/chapters/detail/index.vue#L296)

- 草稿卡片头部新增按钮"采用为定稿"。
- 点击后：
  - 若当前定稿为空 → 直接调接口、Toast 成功、刷新章节。
  - 若定稿已有内容 → 弹确认框（显示当前定稿字数 vs 草稿字数），确认后带 `overrideExisting=true` 重试。

#### C+4.3 权限与审计

- 沿用现有章节编辑权限，无需额外 RBAC。
- `agent_runs` 不记录此操作（非 Agent 触发）；如需可扩 `chapter_audit_log`，本期不做。

### 4.2 验收

- 在草稿卡上点一次按钮，定稿区块立即出现相同文本。
- 定稿已有内容时点按钮，必须有二次确认。

---

## 5. C+5 批量章节草稿生成（用户体验闭环 2）

> 用户痛点：列了 15 章大纲后，需要逐章点 "生成草稿"。期望"一键生成第 X 章到第 Y 章的草稿"。

### 5.1 性能与边界判断

**关于"每批最多多少章"，权衡四个维度：**

| 维度 | 单章成本 | 10 章批量 | 30 章批量 | 结论 |
|---|---|---|---|---|
| LLM 输出 token | ~3000-6000 | 30k-60k | 90k-180k | 单次任务费用可控 |
| 单章耗时 | 30-90 秒 | 5-15 分钟 | 15-45 分钟 | 30 章接近半小时，体验差 |
| 链式后处理 Job | 每章 4 个（StyleCC/CharCC/PlotThread/DraftCompliance） | 40 个 | 120 个 | Hangfire 队列压力可见 |
| 数据库 IO | 每章 ~10 次小写 | 100 次 | 300 次 | 不构成瓶颈 |
| 失败回滚成本 | 单章重做 | 部分章节需手动重做 | 很可能中途失败 | 越长越脆 |
| 用户取消空间 | 无意义 | 还合理 | 应当支持 | — |

**推荐策略：**

- **默认上限 5 章 / 单批**，**硬上限 10 章**（FeatureFlag 可调）。
- **顺序执行**（不并发），原因：
  1. 第 N 章生成必须等第 N-1 章 `chapter_events` 已抽取完成（C+2 上线后），否则时间线注入不准。
  2. 避免 LLM 提供商速率限制。
  3. 用户中途看到不满意可立即中止，已生成章节不浪费。
- 单批整体超时 30 分钟，单章超时 5 分钟，超时则标记该批"部分完成"。

#### 5.2 任务清单

#### C+5.1 后端：批量任务实体 `ChapterBatchDraftJobRun`

字段：`Id / ProjectId / UserId / FromNumber / ToNumber / TotalCount / CompletedCount / FailedCount / Status / StartedAt / FinishedAt / CancelRequested / FailedChapterIds[] / ErrorMessage?`。

- 状态枚举：`Pending / Running / Completed / PartiallyFailed / Cancelled`。
- 仓储 + 幂等建表。

#### C+5.2 后端接口

锚点：[`ChaptersController.cs`](../../src/MuseSpace.Api/Controllers/ChaptersController.cs)

```
POST   /api/storyprojects/{projectId}/chapters/batch-generate-draft
Body: { fromNumber: int, toNumber: int, skipChaptersWithDraft?: boolean }
→ 创建 ChapterBatchDraftJobRun，入队 BatchChapterDraftJob，返回 jobRunId

GET    /api/storyprojects/{projectId}/chapter-batch-runs/{jobRunId}
→ 查询进度（用于轮询或 SignalR 替代）

POST   /api/storyprojects/{projectId}/chapter-batch-runs/{jobRunId}/cancel
→ 设置 CancelRequested = true，正在跑的当前章节完成后中止
```

校验：

- `ToNumber - FromNumber + 1 ≤ MaxBatchSize`（FeatureFlag `feature.batch-draft.max-size`，默认 10）。
- 范围内章节必须存在；建议过滤 `Status == Planned` 或允许 `skipChaptersWithDraft`。

#### C+5.3 后端编排 Job：`BatchChapterDraftJob`

伪代码：

```csharp
foreach (chapter in chapters ordered by Number ASC) {
    if (jobRun.CancelRequested) break;
    try {
        await chapterDraftJobInstance.ExecuteAsync(projectId, chapter.Id, userId);
        jobRun.CompletedCount++;
    } catch (Exception ex) {
        jobRun.FailedCount++;
        jobRun.FailedChapterIds.Add(chapter.Id);
        // 不中断后续章节
    }
    await db.SaveChangesAsync();
    await progressNotifier.NotifyBatchProgress(jobRun);
}
```

- **直接复用** `ChapterDraftJob.ExecuteAsync` 而非 `BackgroundJob.Enqueue`，确保串行。
- 链式触发的 StyleCC / CharCC / PlotThread 可继续走 `Enqueue`（异步）。
- C+2 上线后，需在批次内**等待** `ExtractChapterEventsJob` 完成再进入下一章——可以让 `ChapterDraftJob.ExecuteAsync` 同步抽取事件后再返回。

#### C+5.4 SignalR 进度推送

复用现有 `IAgentProgressNotifier`，新增 `NotifyBatchProgressAsync(projectId, jobRunId, completed, total, currentChapter)`。

#### C+5.5 前端入口

锚点：[`muse-space-web/src/views/projects/chapters/index.vue`](../../../muse-space-web/src/views/projects/chapters/index.vue)

- 章节列表顶部新增按钮"批量生成草稿"。
- 弹窗：起止章节号选择、跳过已有草稿勾选、显示"本批将生成 X 章，预计耗时 Y 分钟"。
- 提交后跳转到批量任务进度面板（或在原页面顶部展示进度条 + 当前章节 + 已完成/失败/剩余）。
- 提供"中止"按钮。

#### C+5.6 FeatureFlag

- `feature.batch-draft.enabled`：批量生成总开关。
- `feature.batch-draft.max-size`：单批硬上限。
- `feature.batch-draft.parallel-enabled`：保留位，**默认 false**，未来真要并发时再启用。

### 5.3 验收

- 选 5 章一键生成，能看到逐章进度推送，最终全部章节有 `DraftText`。
- 中途点击"中止"，正在跑的章节完成、剩余章节不再触发。
- 单章生成失败不影响后续章节继续推进。

---

## 6. C+6 定稿一键导出（用户体验闭环 3）

> 用户痛点：写完想看整本小说时，需要逐章复制定稿。

### 6.1 设计取舍

**导出格式选择：**

| 格式 | 优点 | 成本 | v1 是否做 |
|---|---|---|---|
| Markdown (`.md`) | 通用、保留章节标题层次、所有阅读器都支持 | 零依赖，纯字符串拼接 | ✅ |
| 纯文本 (`.txt`) | 任何阅读器、Kindle/手机都能开 | 零依赖 | ✅ |
| Word (`.docx`) | 排版友好 | 需要 OpenXML 或 NPOI 依赖 | ❌ 后续 |
| EPUB (`.epub`) | 真正的"电子书" | 需要专用库 | ❌ 后续 |

**v1 只做 Markdown + TXT**，覆盖 95% 的用户用例，零依赖。

**导出范围：**

- 默认：所有 `Status == Final` 的章节。
- 可选：自定义章节号范围。
- 可选：包含/不包含尚未定稿的章节（带 `[草稿]` 前缀醒目标识）。

**文件命名：**

```
《{ProjectName}》_第{From}-{To}章_{yyyyMMdd-HHmm}.md
```

### 6.2 任务清单

#### C+6.1 后端导出 Service

新增 `IChapterExportService` + `ChapterExportService`：

```csharp
Task<ChapterExportResult> ExportAsync(
    Guid projectId,
    ChapterExportOptions options,
    CancellationToken ct);
```

`ChapterExportOptions`：`Format(md|txt) / FromNumber? / ToNumber? / IncludeDraftFallback / OnlyFinal`。

`ChapterExportResult`：`FileName / ContentType / Content (byte[])`。

模板规则：

- Markdown：`# 《项目名》` 一级标题 + 每章 `## 第 N 章 标题` + 章节正文 + 章节间 `---`。
- TXT：每章 `第 N 章 标题\n\n` 正文，章节间空行。
- 章节正文优先用 `FinalText`；空时根据 `IncludeDraftFallback` 决定降级到 `DraftText`（前缀 `[草稿] `）或跳过。

#### C+6.2 后端接口

锚点：[`ChaptersController.cs`](../../src/MuseSpace.Api/Controllers/ChaptersController.cs) 或新增 `ProjectExportController`。

```
GET /api/storyprojects/{projectId}/export
    ?format=md|txt
    &from=&to=
    &includeDraft=false
    &onlyFinal=true
→ FileStreamResult，Content-Disposition: attachment; filename="..."
```

UTF-8 + BOM（兼容 Windows 记事本中文）。

#### C+6.3 前端入口

锚点：[`muse-space-web/src/views/projects/chapters/index.vue`](../../../muse-space-web/src/views/projects/chapters/index.vue) 与项目概览页。

- 章节列表顶部 "导出" 按钮，弹窗包含：
  - 格式（md / txt）单选。
  - 范围（全部 / 自定义章节号）。
  - 是否包含未定稿章节的草稿。
- 概览页加同款按钮，方便用户从主入口直接导出。

#### C+6.4 大文件导出兜底

- 单次导出超过 10 MB 时给 Toast 警告，但不阻断。
- 章节超过 200 章时建议分批导出（前端提示）。
- 服务端不做异步队列（量级远没到这一步）。

#### C+6.5 FeatureFlag

- `feature.export.enabled`：导出总开关。
- `feature.export.formats`：可选格式列表（CSV，便于以后加 docx/epub 时灰度）。

### 6.3 验收

- 一个有 10 章定稿的项目，导出 Markdown 能在任意 Markdown 阅读器中正确显示章节层次。
- 中文不乱码（带 BOM 验证 Windows 记事本）。
- 选 "包含草稿" 时，未定稿章节带 `[草稿]` 标识。

---

## 7. 实施顺序与里程碑

## 7. C+7 章节范围契约与来源隔离（根治主线 1）

> 目标：把“模型应该写什么、能看什么、不能把什么当剧情来源”从提示词约定升级为工程数据结构。

### 7.1 设计原则

- 章节生成不直接消费整个项目上下文，而是先构建 `ChapterDraftScope`。
- 后续章节、其他大纲、未授权原著片段不进入生成可见区。
- 来源策略由模式决定：原创模式不允许原著作为剧情来源；续写/番外/扩写改写按大纲级策略决定可见内容。
- 第一版无需数据库迁移，`ChapterDraftScope` 可由当前章节、项目、已有章节和请求参数临时构建。

### 7.2 建议模型

```csharp
public sealed class ChapterDraftScope
{
    public Guid ProjectId { get; init; }
    public Guid ChapterId { get; init; }
    public int ChapterNumber { get; init; }
    public string CurrentPlanText { get; init; } = string.Empty;
    public List<string> AllowedCharacters { get; init; } = [];
    public List<string> AllowedLocations { get; init; } = [];
    public List<string> RequiredBeats { get; init; } = [];
    public RevealLevel AllowedRevealLevel { get; init; }
    public ConflictLevel AllowedConflictLevel { get; init; }
    public List<string> PreviousCanonFacts { get; init; } = [];
    public List<string> ReservedFutureBeats { get; init; } = [];
    public DraftSourcePolicy SourcePolicy { get; init; } = DraftSourcePolicy.OriginalDefault;
}
```

`RevealLevel` 建议：

| 等级 | 允许内容 | 禁止内容 |
|---|---|---|
| `DailyOnly` | 日常、人物关系、普通冲突 | 异常、规则解释、死亡、战斗 |
| `ForeshadowOnly` | 前兆、错觉、异响、氛围变化 | 实体登场、空间切换、规则揭示 |
| `DirectAnomaly` | 直接异常、短暂接触 | 死亡升级、完整规则解释、大规模战斗 |
| `Confrontation` | 正面冲突、追逐、攻击 | 未规划的终局揭示或跨阶段反转 |
| `ResolutionOrReveal` | 阶段性真相、规则揭示、收束 | 当前大纲外终局或其他分支内容 |

### 7.3 任务清单

#### C+7.1 新增 `ChapterDraftScopeBuilder`

- 输入：projectId、chapterId、生成请求、当前项目章节列表、角色/世界观/事实层。
- 输出：`ChapterDraftScope`。
- 第一版仅服务 `ChapterDraftJob`，后续再复用于批量生成和大纲级生成。

#### C+7.2 自动推断 `AllowedRevealLevel`

- 从章节标题、目标、概要、冲突、必中点推断。
- 默认自动推断一版；后续 UI 允许用户手动覆盖。
- 推断失败时采取保守策略：章节越靠前默认越低揭示等级，除非计划明确写出战斗、死亡、规则揭示等信号。

#### C+7.3 来源策略建模

定义 `DraftSourcePolicy`：

- `OriginalDefault`：原著不作为剧情来源，可作为用户明确启用的风格参考。
- `ContinueFromOriginal`：读取原著结局摘要、末段锚点和人物末态；禁止重复已有桥段。
- `SideStoryFromOriginal`：指定范围作为设定/关系依据；新增情节围绕番外主题展开，默认不搬主线桥段。
- `ExpandOrRewrite`：用户指定原著范围可以成为剧情来源，但必须限制在范围内。

#### C+7.4 生成可见上下文分层

把上下文分为：

- `GenerationVisible`：当前章计划、此前同大纲事实、涉及人物稳定卡、世界规则、允许的风格/设定参考。
- `VerificationOnly`：后续章节规划、未来保留地点/事件、其他大纲内容、未授权原著桥段。

`SceneDraftSkill` / `StoryContextBuilder` 只消费 `GenerationVisible`，`DraftVerifier` 消费完整 `ChapterDraftScope`。

#### C+7.5 改造 `ChapterDraftJob`

- 生成前先构建 `ChapterDraftScope`。
- 由 scope 生成 `SkillRequest.Parameters`。
- 后续章节不再通过 Prompt 提醒模型“不要写”，而是默认不进入生成可见区；必要时仅把抽象边界写入 Prompt。

#### C+7.6 生成记录追加 scope 摘要

- `GenerationRecord.InputPreview` 或后续扩展字段记录 scope 摘要：章节号、揭示等级、来源策略、可见上下文数量、保留未来项数量。
- 方便线上排查“本章为什么能/不能写某内容”。

#### C+7.7 单元测试

覆盖：

- 第一章只允许前兆时，scope 推断为 `ForeshadowOnly`。
- 中后段明确写“袭击/死亡/规则”时，scope 不误判为前兆章。
- 原创模式下原著片段不进入生成可见区。
- 后续章节进入 `ReservedFutureBeats`，不进入生成上下文。

### 7.4 验收

- 任意章节生成前都能打印/记录清晰的 `ChapterDraftScope` 摘要。
- 换一本小说时，不需要改提示词或禁词列表，也能根据章节计划推断范围。
- 第一章生成 Prompt 中只能看到当前章允许内容，看不到第二/三章具体桥段。

---

## 8. C+8 保存前结构化验收（根治主线 2）

> 目标：即使模型生成失控，也必须在保存前被验收层拦住。

### 8.1 设计原则

- `DraftVerifier` 是保存闸门，不是异步建议。
- 验证失败时最多自动修订 1 次；用户已确认该策略。
- 修订仍失败时，任务失败并记录原因，不保存为草稿，也不进入后续一致性/事实抽取链路。
- 第一版先用规则/轻量抽取保证稳定；后续可加 LLM JSON 分析器作为增强，不让分析器参与创作。

### 8.2 建议模型

```csharp
public sealed class DraftVerificationResult
{
    public bool IsPassed { get; init; }
    public List<DraftViolation> Violations { get; init; } = [];
    public string RevisionInstruction { get; init; } = string.Empty;
}

public sealed class DraftViolation
{
    public DraftViolationType Type { get; init; }
    public DraftViolationSeverity Severity { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public string Expected { get; init; } = string.Empty;
}
```

违规类型建议：

- `FutureBeatLeak`
- `OutOfScopeLocation`
- `OutOfScopeCharacter`
- `RevealLevelExceeded`
- `ConflictLevelExceeded`
- `SourcePolicyViolation`
- `ContradictsPreviousCanon`
- `MissingRequiredBeat`

### 8.3 任务清单

#### C+8.1 新增 `DraftVerifier`

- 输入：`ChapterDraftScope` + 草稿正文。
- 输出：`DraftVerificationResult`。
- 第一版只做阻断类违规：未来内容提前泄漏、揭示等级超限、原著来源策略违规、明显当前章计划外地点/实体/死亡/战斗。

#### C+8.2 草稿信息抽取器

先实现轻量抽取：

- 使用当前章计划、未来保留项、角色名、地点词、冲突强度信号做匹配。
- 不把词表写成某一本小说的设定，而是从章节计划、未来章节、世界规则和来源策略动态生成候选项。

后续增强：

- 可选 `DraftStructureAnalysisSkill`，让 LLM 只输出 JSON：地点、人物、事件、揭示、冲突强度、是否死亡/追逐/战斗/空间切换。
- 分析失败时降级规则验证，不影响保存闸门。

#### C+8.3 接入 `ChapterDraftJob`

流程改为：

```text
BuildScope
→ GenerateDraft
→ VerifyDraft
→ if pass: Save
→ if fail: GenerateRevision once
→ VerifyRevision
→ if pass: Save
→ else: FailTaskWithoutSaving
```

#### C+8.4 修订指令生成

- 修订指令由 `DraftVerificationResult.Violations` 拼出。
- 指令必须结构化说明：哪些片段越界、为什么越界、本章允许写到什么程度。
- 不要求模型“微调局部”，默认要求重写当前章，避免保留污染段落。

#### C+8.5 失败结果可见

- 后台任务失败消息中包含简短失败原因。
- 生成记录或任务记录里保存验证报告摘要。
- 后续 UI 可展示“未保存原因”，帮助用户调整本章计划或手动提高揭示等级。

#### C+8.6 阻断后处理链路

验证失败时不触发：

- `StyleConsistencyCheckJob`
- `CharacterConsistencyCheckJob`
- `PlotThreadTrackingJob`
- `ChapterEventExtractionJob`
- `CanonFactExtractionJob`

避免坏草稿继续污染事实层。

#### C+8.7 单元测试与回归样例

覆盖：

- 第一章前兆计划中写出后续保留地点/事件 → blocker。
- 当前章明确允许战斗/死亡 → 不误拦。
- 原创模式写出未授权原著桥段 → blocker。
- 缺失必须命中点 → 可先作为 warning，后续决定是否阻断。
- 跨大纲事实与事件不会进入当前大纲上下文。

### 8.4 验收

- 验证失败的草稿不会写入 `Chapter.DraftText`。
- 自动修订只执行 1 次。
- 验证报告能指出具体证据和期望边界。
- 生产问题中的“第一章提前进入后续恐怖阶段”不依赖固定禁词也能被拦截。

---

## 9. C+9 大纲级创作模式重构（产品抽象修正）

> 目标：把原创 / 续写 / 支线番外 / 扩写改写从章节级功能上移为大纲/作品分支功能，符合作者真实创作工作流。

### 9.1 问题判断

当前模式放在章节生成入口，会导致：

- 番外/续写被误解为“生成某一章的临时开关”，而不是一条多章节故事线。
- 每章都临时决定来源策略，系统无法稳定判断整条大纲的边界。
- 章节质量验证无法区分“本大纲允许内容”和“其他大纲或后续阶段泄漏”。

因此，模式应属于 `Outline`，章节属于某个 `Outline`。

### 9.2 建议结构

```text
Project
  ├─ Outline A：原创主线
  │   ├─ Chapter 1
  │   ├─ Chapter 2
  │   └─ ...
  ├─ Outline B：支线番外
  │   ├─ Chapter 1
  │   ├─ Chapter 2
  │   └─ ...
  └─ Outline C：原著续写
      ├─ Chapter 1
      └─ ...
```

`Outline` 建议字段：

```csharp
public sealed class StoryOutline
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Name { get; set; } = string.Empty;
    public GenerationMode Mode { get; set; }
    public Guid? SourceNovelId { get; set; }
    public int? SourceRangeStart { get; set; }
    public int? SourceRangeEnd { get; set; }
    public string? BranchTopic { get; set; }
    public string? ContinuationAnchor { get; set; }
    public DivergencePolicy DivergencePolicy { get; set; }
    public int? TargetChapterCount { get; set; }
    public string? OutlineSummary { get; set; }
}
```

### 9.3 来源策略

- 原创大纲：原著不可作为剧情来源，除非用户显式作为风格参考。
- 续写大纲：读取原著结局摘要、末段锚点、人物末态；从结局之后自然接续，不重复已有剧情。
- 支线番外大纲：读取指定范围作为设定/关系依据；新增情节围绕番外主题展开，不自动搬运主线桥段。
- 扩写/改写大纲：用户指定范围可以成为剧情来源，但只限指定范围。

### 9.4 任务清单

#### C+9.1 新增大纲实体与仓储

- 新增 `StoryOutline` 或复用现有 Outline 领域模型并补齐模式字段。
- 新增仓储、Controller、基础 CRUD。
- 迁移期自动创建默认“原创主线大纲”，现有章节归入该大纲。

#### C+9.2 章节增加 `OutlineId`

- `Chapter` 归属于一个大纲。
- 查询章节时默认按当前大纲过滤，避免其他大纲章节混入上下文。
- 批量重排编号改为大纲内重排，而非项目全局重排。

#### C+9.3 大纲级模式配置 UI

- 大纲列表页显示模式、章节数、来源小说、主题/范围摘要。
- 创建大纲时选择：原创 / 续写 / 支线番外 / 扩写改写。
- 支线番外、扩写改写必须选择来源范围或主题；续写必须选择来源小说和锚点。

#### C+9.4 章节生成入口瘦身

- 章节生成不再每次选择 `GenerationMode`。
- 章节继承所属大纲的模式和来源策略。
- 章节级只保留：目标、摘要、必中点、冲突、情绪曲线、`AllowedRevealLevel` 覆盖、当前章参考片段。

#### C+9.5 改造上下文构建

- `StoryContextBuilder` 通过 `OutlineId` 判断“同一大纲已发生章节”。
- 后续同大纲章节进入 `ReservedFutureBeats`。
- 其他大纲章节默认不可见，除非后续明确设计“共享正典事实”。

#### C+9.6 改造大纲规划 Agent

- 大纲规划输出不直接写全项目章节，而是写入某个 `StoryOutline`。
- 新作/续写/番外/扩写改写变成“创建大纲时的规划模式”。
- 批量导入章节时绑定大纲。

#### C+9.7 兼容现有数据

- 第一版迁移策略：
  - 每个项目创建一个默认原创主线大纲。
  - 所有现有章节挂入该大纲。
  - 旧章节生成请求中的模式字段保留兼容，但标记为过渡字段。

#### C+9.8 验收

- 一个项目下可同时存在原创主线、支线番外、原著续写三条大纲。
- 在支线番外大纲中生成第 1 章，不会读取原创主线第 10 章作为已发生上下文。
- 章节生成自动继承大纲模式，用户不需要每章重复配置。
- `DraftVerifier` 能根据大纲来源策略判断合法引用与来源污染。

#### C+9.9 事实层与事件层大纲隔离

- `chapter_events` 与 `canon_facts` 增加 `StoryOutlineId`，迁移时优先按 `SourceChapterId` / `ChapterId` 回填，无法按章节回填时归入默认大纲。
- `StoryContextBuilder`、`DuplicateEventCheckJob`、`CanonConflictCheckJob`、`CanonFactExtractionJob`、`ChapterEventExtractionJob` 均按当前章节所属大纲读写。
- 手动维护事件与事实的 API 返回 `StoryOutlineId`，事实查询支持按 `storyOutlineId` 过滤。
- 单元测试覆盖：当前大纲生成时，不读取其他大纲的事件和事实。

---

## 10. 实施顺序与里程碑

### 10.1 原推荐顺序（2026-04-30）

```
Day 1-2  ── C+1 Prompt 边界硬化         ←  最高 ROI，先做
Day 3    ── C+4 草稿一键采用            ←  超低成本，顺手做
Day 4-5  ── C+6 定稿一键导出            ←  与 C+1 无依赖，可并行交付
─────── 第一阶段成果可上线 ───────
Day 6-9  ── C+2 章节事件时间线          ←  结构化记忆，难度中等
Day 10-11 ── C+3 草稿越界反向校验       ←  C+1 / C+2 数据齐全后做更准
Day 12-14 ── C+5 批量章节草稿生成       ←  最后做，因为依赖 C+1 / C+2 才不会越批量越乱
─────── 整阶段收尾，可决定是否进阶段 E ───────
```

### 10.2 修正后推荐顺序（2026-05-09）

```
Day 1    ── C+0 生产止血回归确认         ←  已完成，继续保留测试
Day 2-5  ── C+7 章节范围契约与来源隔离   ←  已完成主干
Day 6-9  ── C+8 保存前结构化验收         ←  已完成主干
Day 10-16 ─ C+9 大纲级创作模式重构       ←  已完成主干
Day 17   ── C+9.9 事实/事件大纲隔离      ←  已完成主干
并行空档 ── C+4 / C+6                   ←  低风险创作闭环体验项
最后     ── C+5 批量章节草稿生成         ←  等质量闸门稳定后再放大规模
```

---

## 11. 与阶段 E 的衔接

阶段 C+ 完成后，再判断是否启动阶段 E：

- 如果用户反馈一致性问题已基本消失 → 阶段 E 可按文档原计划推进（memory_items / 实体关系 / 工业化）。
- 如果跨多卷复杂关系仍频繁出错 → 才有依据启动 GraphRAG 试点。
- 阶段 C+5 产出的 `ChapterBatchDraftJobRun` 可作为阶段 E-4 "独立 Worker" 的第一个候选迁移对象——届时不必重新设计任务模型。
- C+7/C+8 的 `ChapterDraftScope` 与 `DraftVerificationResult` 是阶段 E 记忆网络和 GraphRAG 的输入边界，不应被阶段 E 推翻。

---

## 12. 不在本阶段做

| 项 | 原因 |
|---|---|
| Hybrid 检索 | 与本阶段问题无关；保留为阶段 C 末尾或阶段 E 前期独立任务 |
| memory_items / 实体关系 | 阶段 E 内容；先验证 C+1/C+2 是否够用 |
| GraphRAG / Neo4j | 阶段 E-5；除非 C+ 完成后仍有跨多卷复杂关系问题 |
| Redis / 独立 Worker | 当前压力不需要；C+5 顺序执行已能满足 |
| docx / epub 导出 | 增加依赖收益不明显，先用 md / txt 验证导出闭环 |
| 章节并发批量生成 | C+5.6 已留 FeatureFlag 位，待真实需求出现再开 |
| 用固定小说禁词作为主方案 | 只能作为兜底；根治方案必须基于章节计划、来源策略和结构化验证 |
