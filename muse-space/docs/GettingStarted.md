# MuseSpace 新手入门指南

> 本文档面向第一次接触 MuseSpace 项目的开发者，目标是帮助你快速理解项目结构、各层职责，以及如何扩展新能力。

---

## 1. 项目定位

MuseSpace 是一个本地小说创作 AI 工作流系统。

- 通过 **API** 发起创作请求（：生成场景草稿）
- 后端负责**编排上下文、加载 Prompt、调用本地模型、记录日志**
- 本地 LLM（如 gpt-oss）负责**生成文本**

项目采用 .NET 10 + 分层架构，当前处于 Phase 1（框架搭建阶段），核心链路已就绪，模型调用为 stub 占位实现。

---

## 2. 项目结构一览

```text
MuseSpace/
├── src/
│   ├── MuseSpace.Api/              # HTTP 入口层（Controllers、DI注册、Program.cs）
│   ├── MuseSpace.Application/      # 应用层（接口定义、业务服务、Skill编排）
│   ├── MuseSpace.Domain/           # 领域层（实体、枚举，不依赖任何框架）
│   ├── MuseSpace.Infrastructure/   # 基础设施层（接口的具体实现）
│   └── MuseSpace.Contracts/        # 契约层（请求/响应 DTO，供 API 和 Application 共用）
├── tests/
│   └── MuseSpace.UnitTests/        # 单元测试
├── prompts/
│   └── drafting/
│       └── scene-v1.md             # Prompt 模板文件
└── docs/
    ├── DevelopmentPlan.md          # 完整开发规划（Phase 0–5）
    ├── GettingStarted.md           # 本文档
    └── PromptConvention.md         # Prompt 模板规范
```

---

## 3. 分层说明

### 3.1 MuseSpace.Domain（领域层）

**职责：** 定义核心业务概念，不依赖任何框架或库。

包含：
- `Entities/` — 业务实体（`StoryProject`、`Chapter`、`Scene`、`Character`、`WorldRule`、`StyleProfile`、`GenerationRecord`）
- `Enums/` — 枚举类型（`ChapterStatus`、`ForeshadowingStatus`）

**规则：** 这一层不能引用其他层，不能有框架依赖。

---

### 3.2 MuseSpace.Contracts（契约层）

**职责：** 定义 API 的输入/输出数据结构（DTO），供 Api 层和 Application 层共用。

包含：
- `Common/ApiResponse<T>` — 统一 API 返回包装
- `Draft/GenerateSceneDraftRequest` — 生成场景草稿的请求体
- `Draft/GenerateSceneDraftResponse` — 生成场景草稿的响应体

---

### 3.3 MuseSpace.Application（应用层）

**职责：** 定义业务流程的**抽象接口**和**编排逻辑**，不包含具体技术实现。

```text
Application/
├── Abstractions/
│   ├── Llm/            ILlmClient              # 调用语言模型的接口
│   ├── Prompt/         IPromptTemplateProvider  # 加载 Prompt 模板
│   │                   IPromptTemplateRenderer  # 渲染 Prompt 变量
│   │                   PromptTemplate           # 模板数据结构
│   ├── Skills/         ISkill                   # 单个 Skill 的接口
│   │                   ISkillOrchestrator       # 根据 TaskType 调度 Skill
│   │                   SkillRequest / SkillResult
│   ├── Story/          IStoryContextBuilder     # 构建创作上下文
│   │                   StoryContext / StoryContextRequest
│   └── Logging/        IGenerationLogService    # 写入生成日志
└── Services/
    ├── SkillOrchestrator.cs                     # ISkillOrchestrator 的实现
    └── Drafting/
        ├── SceneDraftSkill.cs                   # 场景草稿 Skill
        └── GenerateSceneDraftAppService.cs      # 编排整条生成链路的 App Service
```

**关键设计：**
- 所有业务逻辑只依赖接口（`ILlmClient`、`IPromptTemplateProvider` 等），不直接依赖具体实现
- `ISkillOrchestrator` 根据 `TaskType` 字符串找到对应的 `ISkill` 并执行

---

### 3.4 MuseSpace.Infrastructure（基础设施层）

**职责：** 提供接口的具体技术实现。Application 层定义"做什么"，Infrastructure 层定义"怎么做"。

| 接口 | 实现类 | 说明 |
|---|---|---|
| `ILlmClient` | `LocalModelClient` | Phase 1 为 stub，Phase 2 接真实 HTTP 调用 |
| `IPromptTemplateProvider` | `FileSystemPromptTemplateProvider` | 从 `prompts/` 目录读取 `.md` 文件 |
| `IPromptTemplateRenderer` | `PromptTemplateRenderer` | 替换 `{{variable}}` 占位符 |
| `IStoryContextBuilder` | `StoryContextBuilder` | Phase 1 为 stub，Phase 3 补充真实逻辑 |
| `IGenerationLogService` | `GenerationLogService` | 将生成记录写入本地 JSON 文件 |

---

### 3.5 MuseSpace.Api（API 层）

**职责：** 处理 HTTP 请求，调用 Application Service，返回标准化响应。

- `Controllers/DraftController.cs` — 对外暴露 `POST /api/draft/scene`
- `Extensions/ServiceCollectionExtensions.cs` — 所有服务的 DI 注册
- `Program.cs` — 启动入口

---

## 4. 一次完整的请求调用链

以 `POST /api/draft/scene` 为例，调用链如下：

```
HTTP Request
    ↓
DraftController.GenerateSceneDraft(request)
    ↓
GenerateSceneDraftAppService.ExecuteAsync(request)
    ↓  生成 RequestId，构建 SkillRequest
ISkillOrchestrator.ExecuteAsync(skillRequest)
    ↓  按 TaskType="scene-draft" 找到 SceneDraftSkill
SceneDraftSkill.ExecuteAsync(skillRequest)
    ├─ IStoryContextBuilder.BuildAsync(...)   → StoryContext（项目背景、角色卡、章节摘要等）
    ├─ IPromptTemplateProvider.GetTemplateAsync("drafting", "scene-v1") → PromptTemplate
    ├─ IPromptTemplateRenderer.Render(template, variables) → 渲染后的 Prompt 字符串
    └─ ILlmClient.ChatAsync(prompt)           → 模型返回的原始文本
    ↓
SkillResult（含输出文本、耗时）
    ↓
IGenerationLogService.LogAsync(GenerationRecord) → 写入日志文件
    ↓
GenerateSceneDraftResponse
    ↓
ApiResponse<GenerateSceneDraftResponse>
    ↓
HTTP Response (200 OK)
```

---

## 5. 如何调用 API

启动项目后，发送以下请求：

**请求**
```http
POST /api/draft/scene
Content-Type: application/json

{
  "storyProjectId": "project-001",
  "sceneGoal": "主角第一次进入禁忌森林，感受到异样的气息",
  "conflict": "森林边缘的护林人试图阻止他",
  "emotionCurve": "好奇→紧张→决然"
}
```

**响应**
```json
{
  "success": true,
  "data": {
    "requestId": "a1b2c3d4e5f6",
    "content": "...(生成的场景文本)...",
    "taskType": "scene-draft",
    "durationMs": 120,
    "success": true
  },
  "message": null
}
```

---

## 6. 如何新增一个 Skill

以新增"改稿 Skill（RevisionSkill）"为例，只需 4 步：

### Step 1：在 Application 层创建 Skill 类

新建 `src/MuseSpace.Application/Services/Revision/RevisionSkill.cs`：

```csharp
public sealed class RevisionSkill : ISkill
{
    public string Name => "Revision";
    public string TaskType => "revision";  // 唯一的任务类型标识

    public async Task<SkillResult> ExecuteAsync(SkillRequest request, CancellationToken cancellationToken = default)
    {
        // 1. 构建上下文
        // 2. 加载 Prompt 模板
        // 3. 渲染变量
        // 4. 调用 LLM
        // 5. 返回 SkillResult
    }
}
```

### Step 2：新增 Prompt 模板文件

新建 `prompts/revision/scene-v1.md`，按 [Prompt 规范](./PromptConvention.md) 编写。

### Step 3：在 DI 中注册

在 `ServiceCollectionExtensions.cs` 中添加：

```csharp
services.AddScoped<ISkill, RevisionSkill>();
```

`SkillOrchestrator` 会自动通过 `TaskType` 路由到这个新 Skill，无需修改编排器。

### Step 4：暴露 API（可选）

在 `DraftController` 或新建的 Controller 中新增 Action，调用对应的 App Service。

---

## 7. 如何查看生成日志

日志文件写入在运行目录下的 `logs/generations/` 文件夹，每次生成对应一个 JSON 文件：

```text
logs/
└── generations/
    └── 20240115_143022_a1b2c3d4e5f6.json
```

文件内容示例：
```json
{
  "RequestId": "a1b2c3d4e5f6",
  "TaskType": "scene-draft",
  "SkillName": "SceneDraft",
  "PromptName": "scene-v1",
  "PromptVersion": "v1",
  "ModelName": "local-stub",
  "DurationMs": 120,
  "Success": true,
  "ErrorMessage": null,
  "InputPreview": "...",
  "OutputPreview": "...",
  "CreatedAt": "2024-01-15T14:30:22Z"
}
```

---

## 8. 当前阶段说明（Phase 1）

Phase 1 是框架搭建阶段，以下内容为**占位实现（stub）**，Phase 2 开始会逐步替换为真实逻辑：

| 组件 | 当前状态 | Phase 2+ 计划 |
|---|---|---|
| `LocalModelClient` | 返回硬编码 JSON | 替换为真实 HTTP 调用本地 gpt-oss |
| `StoryContextBuilder` | 返回空的上下文占位 | Phase 3 补充角色卡、章节摘要、世界规则读取 |

其他所有部分（接口定义、调用链、日志、Prompt 加载与渲染）均已就绪，可以直接在其基础上扩展。

---

## 9. 下一步

- 了解 Prompt 模板如何编写 → 参考 [PromptConvention.md](./PromptConvention.md)
- 了解完整开发路线 → 参考 [DevelopmentPlan.md](./DevelopmentPlan.md)
- 进入 Phase 2：让 `LocalModelClient` 真正调用本地模型
