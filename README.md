# MuseSpace

**MuseSpace** 是一个面向小说创作者的本地 AI 工作流系统。后端基于 .NET 10 构建创作编排引擎，前端提供可视化创作管理界面，通过接入本地或云端大语言模型（LLM）实现场景生成、角色管理、世界观维护等核心创作能力。

> 一句话概括：**.NET 负责系统与流程，LLM 负责生成。**

---

## ✨ 特点

- **工作流优先于训练**：通过预定义的 Prompt 模板和 Skill 工作流约束生成，无需微调模型
- **结构化创作上下文**：基于角色卡、章节摘要、世界规则、文风配置等结构化数据组织上下文，而非依赖无限长上下文窗口
- **模型灵活接入**：支持对接 OpenRouter（云端）或本地部署的 gpt-oss 等开源模型
- **Skill 模块化**：每种创作能力（场景草稿、改稿、一致性检查）封装为独立 Skill，便于扩展
- **分阶段演进**：项目采用 6 阶段开发路线，从框架骨架逐步演进为完整创作平台

---

## 🗂️ 项目结构

```
MuseSpace/
├── muse-space/              # 后端：.NET 10 创作工作流引擎
│   ├── src/
│   │   ├── MuseSpace.Api/           # HTTP 入口层
│   │   ├── MuseSpace.Application/   # 应用层（业务编排、Skill、接口定义）
│   │   ├── MuseSpace.Domain/        # 领域层（实体与枚举）
│   │   ├── MuseSpace.Infrastructure/# 基础设施层（LLM 客户端、Prompt 加载、日志）
│   │   └── MuseSpace.Contracts/     # 契约层（请求/响应 DTO）
│   ├── tests/
│   │   └── MuseSpace.UnitTests/     # 单元测试
│   ├── prompts/                     # Prompt 模板文件（Markdown 格式）
│   └── docs/                        # 详细技术文档与开发规划
└── muse-space-web/          # 前端：Vue 3 创作管理界面
    └── src/
        ├── views/           # 页面（首页、项目、章节、角色、世界规则等）
        ├── api/             # 接口请求封装
        ├── store/           # Pinia 状态管理
        └── components/      # 公共组件
```

---

## 🛠️ 技术栈

### 后端

| 技术 | 说明 |
|------|------|
| .NET 10 / ASP.NET Core | 主框架 |
| 分层架构（DDD） | Api / Application / Domain / Infrastructure |
| OpenRouter / gpt-oss | LLM 接入（可切换） |
| Serilog | 结构化日志 |
| Scrutor | DI 程序集扫描 |
| Mapster | 对象映射 |

### 前端

| 技术 | 说明 |
|------|------|
| Vue 3 + TypeScript | 主框架 |
| Vite | 构建工具 |
| Pinia | 状态管理 |
| Vue Router 4 | 路由 |
| UnoCSS | 原子化 CSS |
| Axios | HTTP 请求 |
| SignalR | 实时通信 |

---

## 🚀 快速开始

### 环境要求

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Node.js 18+
- 可用的 LLM 服务（OpenRouter API Key 或本地部署的 gpt-oss）

### 后端启动

**1. 配置 LLM 接入**

在 `muse-space/src/MuseSpace.Api/` 下新建 `appsettings.Development.json`（不会提交到仓库）：

```json
{
  "LLM": {
    "BaseUrl": "https://openrouter.ai/api/v1",
    "ApiKey": "your-api-key-here",
    "Model": "your-model-name"
  }
}
```

**2. 启动 API 服务**

```bash
cd muse-space/src/MuseSpace.Api
dotnet run
```

API 默认运行在 `http://localhost:5000`。

**3. 测试场景生成接口**

```bash
curl -X POST http://localhost:5000/api/draft/scene \
  -H "Content-Type: application/json" \
  -d '{
    "storyProjectId": "project-001",
    "sceneGoal": "主角第一次进入禁忌森林，感受到异样的气息",
    "conflict": "森林边缘的护林人试图阻止他",
    "emotionCurve": "好奇→紧张→决然"
  }'
```

### 前端启动

```bash
cd muse-space-web
npm install
npm run dev
```

前端默认运行在 `http://localhost:5173`。

---

## 🔑 核心概念

### Skill 体系

每种创作能力封装为一个 Skill，由 `ISkillOrchestrator` 统一调度：

| Skill | 功能 |
|-------|------|
| `SceneDraftSkill` | 根据场景目标、冲突设计、情感曲线生成场景草稿 |
| `RevisionSkill` | 对已有内容进行改稿优化（Phase 4） |
| `ConsistencyCheckSkill` | 检查内容与世界观、角色设定的一致性（Phase 4） |

### 创作上下文（Story Context）

生成时自动组装以下结构化数据，注入 Prompt：

- 项目背景摘要
- 最近章节摘要（最多 3 章）
- 参与角色卡（最多 4 人）
- 世界观规则（最多 8 条）
- 文风要求
- 当前场景目标 / 冲突 / 情感曲线

### Prompt 模板

模板存储在 `prompts/` 目录，采用结构化 Markdown 格式：

```
Category: drafting
Version: v1
system      ← 角色设定
instruction ← 任务说明
context     ← 注入的故事上下文
output_format ← 输出格式约束（JSON）
```

---

## 📋 开发路线

| 阶段 | 状态 | 目标 |
|------|------|------|
| Phase 0 | ✅ 已完成 | 规划与设计冻结 |
| Phase 1 | ✅ 已完成 | 项目框架搭建，分层骨架与接口定义 |
| Phase 2 | ✅ 已完成 | 最小生成链路跑通（真实 LLM 调用） |
| Phase 3 | 🔄 进行中 | 创作资料与上下文能力补齐（角色卡、章节摘要、世界规则） |
| Phase 4 | 📅 规划中 | 创作工作流扩展（改稿、一致性检查） |
| Phase 5 | 📅 规划中 | 工程化与长期演进（数据库、评测、版本管理） |

---

## 📚 文档

| 文档 | 说明 |
|------|------|
| [开发规划](./muse-space/docs/DevelopmentPlan.md) | 完整 6 阶段开发路线图与设计决策 |
| [新手入门](./muse-space/docs/GettingStarted.md) | 项目结构、调用链、如何新增 Skill |
| [Prompt 规范](./muse-space/docs/PromptConvention.md) | Prompt 模板编写规范 |
| [Phase 2 详设](./muse-space/docs/Phase2-Plan.md) | LLM 接入与最小生成链路实现 |
| [Phase 3 详设](./muse-space/docs/Phase3-Plan.md) | 创作资料与数据持久化方案 |

---

## 📄 许可证

本项目仅供学习交流使用。
