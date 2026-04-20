# Phase 2：最小生成链路跑通

> 本文档记录 Phase 2 的实施计划，作为开发过程与验收的依据。

---

## 1. 阶段目标

在 Phase 1 的框架基础上，打通一次真实的 SceneDraft 生成链路。

**一句话目标：`POST /api/draft/scene` 能真实调用 OpenRouter 模型并返回生成结果。**

---

## 2. 已确认的设计决策

| 项 | 决策 |
|---|---|
| LLM 客户端命名 | `OpenRouterLlmClient`（替代 Phase 1 的 `LocalModelClient`） |
| 模型输出格式 | 优先 JSON，后端做**宽松解析**（解析失败不中断请求） |
| API Key 管理 | 从 `appsettings.json` 移除密钥，改为 `appsettings.Development.json`（已在 .gitignore 中） |
| 配置结构 | `LLM` 配置项提升为独立根节点（从 `Logging` 下移出来） |
| 架构变化 | **不改调用链形状**，只替换 `ILlmClient` 的具体实现 |

---

## 3. 任务清单

### Task 1：配置正式化

**目标：** 让模型配置可注入、可切换、密钥不进仓库。

- 修复 `appsettings.json`：将 `LLM` 从 `Logging` 节点下移出为独立根节点，移除 API Key
- 新建 `appsettings.Development.json`：存放实际 API Key（已被 .gitignore 排除）
- 新建 `LlmOptions` 配置类（放在 `Application/Abstractions/Llm/`）
- 在 DI 中绑定 `IOptions<LlmOptions>`

**产物：**
- `src/MuseSpace.Application/Abstractions/Llm/LlmOptions.cs`
- `appsettings.json`（结构修正）
- `appsettings.Development.json`（含密钥，不提交）

---

### Task 2：OpenRouter 请求/响应模型

**目标：** 定义与 OpenRouter `/chat/completions` 接口对应的内部 DTO，避免手拼 JSON。

- `ChatCompletionRequest`：包含 model、messages
- `ChatMessage`：包含 role、content
- `ChatCompletionResponse`：包含 choices → message → content

**产物：**
- `src/MuseSpace.Infrastructure/Llm/Models/ChatCompletionRequest.cs`
- `src/MuseSpace.Infrastructure/Llm/Models/ChatMessage.cs`
- `src/MuseSpace.Infrastructure/Llm/Models/ChatCompletionResponse.cs`

---

### Task 3：实现 `OpenRouterLlmClient`

**目标：** 替换 stub，通过 HTTP 真实调用 OpenRouter。

- 注入 `HttpClient`（通过 `IHttpClientFactory`）和 `IOptions<LlmOptions>`
- 实现 `ILlmClient.ChatAsync`：
  - 构建 `ChatCompletionRequest`
  - POST 到 `{BaseUrl}/chat/completions`
  - 设置 `Authorization: Bearer {ApiKey}`
  - 反序列化响应，提取 `choices[0].message.content`
- 基础异常处理：HTTP 错误、空响应等
- 通过 `ILogger` 记录调用状态

**产物：**
- `src/MuseSpace.Infrastructure/Llm/OpenRouterLlmClient.cs`
- 删除 `src/MuseSpace.Infrastructure/Llm/LocalModelClient.cs`

---

### Task 4：更新 DI 注册

**目标：** 将新的配置和客户端接入 DI 容器。

- 绑定 `LlmOptions` 配置节
- 注册 `HttpClient`（通过 `AddHttpClient`）
- 将 `ILlmClient` 的注册从 `LocalModelClient` 改为 `OpenRouterLlmClient`
- 移除旧的 `using` 引用

**产物：**
- `ServiceCollectionExtensions.cs` 更新

---

### Task 5：最小 JSON 输出解析

**目标：** 对模型返回的 JSON 做宽松解析，成功时提取结构化字段，失败时降级保留原文。

- 在 `SceneDraftSkill` 的返回路径中，尝试解析 JSON 输出
- 解析成功：提取 `scene_text`、`summary` 等字段
- 解析失败：保留原始文本作为 `Output`，不中断请求
- 解析结果可以反映在日志中（是否为结构化输出）

**产物：**
- `SceneDraftSkill.cs` 更新（增加解析逻辑）
- 或新建 `SceneDraftOutputParser.cs`（如果逻辑较多）

---

### Task 6：Scalar 端到端验收

**目标：** 用 Scalar UI 完成第一次真实生成闭环。

验收步骤：
1. 启动 API
2. 打开 `https://localhost:{port}/scalar/v1`
3. 调用 `POST /api/draft/scene`，输入一个简单场景
4. 确认返回真实模型生成内容
5. 确认 `logs/generations/` 下生成日志文件
6. 确认日志中 `Success=true`、`DurationMs` 有真实值

**产物：**
- 验收通过的截图或日志样本（可选）
- 如有问题，记录到本文档末尾

---

## 4. 实施顺序

```
Task 1（配置）→ Task 2（DTO）→ Task 3（客户端实现）→ Task 4（DI 更新）→ Task 5（解析）→ Task 6（验收）
```

每个 Task 完成后构建验证，确保不破坏已有结构。

---

## 5. 本阶段边界

### 做
- 配置化模型参数
- 真实 HTTP 调用 OpenRouter
- 最小 JSON 宽松解析
- 真实错误日志
- Scalar 端到端验证

### 不做深
- 复杂重试 / 限流策略
- 多模型切换
- 流式输出（Streaming）
- 数据库存储
- 真实上下文来源（StoryContextBuilder 仍为 stub）
- 严格 JSON Schema 校验

---

## 6. 完成标准

- [  ] 至少一个创作请求可以从 API 到模型调用完整跑通
- [  ] 可以拿到真实的结构化或正文输出
- [  ] 输出失败时有基础日志可定位（HTTP 状态码、错误信息）
- [  ] API Key 不出现在提交的代码中
- [  ] 主链路具备继续扩展的稳定结构（调用链形状未改变）
