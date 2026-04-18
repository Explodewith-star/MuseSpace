

# MuseSpace

MuseSpace 是一个基于本地大语言模型的 AI 小说创作辅助系统，旨在帮助写作者高效地进行小说创作、情节规划和内容生成。

## 项目简介

MuseSpace 采用模块化架构设计，结合领域驱动开发（DDD）思想构建。系统通过 Skill 编排机制调用本地 LLM 模型，生成小说场景草稿、进行一致性检查等创作任务。核心设计理念包括：

- **工作流优先于训练**：通过预定义的 Prompt 和 Skill 工作流来指导生成，而非依赖模型微调
- **结构化记忆**：基于结构化的故事上下文（Story Context）而非长上下文窗口
- **本地部署**：支持对接本地部署的 GPT-OSS 等开源大模型

## 技术栈

- **.NET 10.0** - 主框架
- **ASP.NET Core** - Web API
- **依赖注入** - Microsoft.Extensions.DependencyInjection
- **日志** - Microsoft.Extensions.Logging

## 项目结构

```
src/
├── MuseSpace.Domain/           # 领域层 - 实体与枚举定义
│   └── Entities/
│       ├── StoryProject.cs    # 故事项目
│       ├── Chapter.cs          # 章节
│       ├── Scene.cs          # 场景
│       ├── Character.cs       # 角色
│       ├── WorldRule.cs      # 世界观规则
│       ├── StyleProfile.cs   # 文风配置
│       └── GenerationRecord.cs # 生成记录
├── MuseSpace.Contracts/       # 契约层 - API 请求/响应定义
├── MuseSpace.Application/     # 应用层 - 业务服务与抽象接口
│   └── Abstractions/
│       ├── Llm/             # LLM 客户端接口
│       ├── Prompt/           # Prompt 模板接口
│       ├── Skills/           # Skill 接口
│       └── Story/            # 故事上下文接口
├── MuseSpace.Infrastructure/  # 基础设施层 - 具体实现
│   ├── Llm/                 # 本地模型客户端
│   ├── Logging/              # 生成日志服务
│   ├── Prompt/               # 文件系统 Prompt 提供者
│   └── Story/                # 故事上下文构建器
└── MuseSpace.Api/            # API 层 - Web 接口
tests/
└── MuseSpace.UnitTests/      # 单元测试
```

## 核心概念

### Skill 体系

Skill 是系统的核心执行单元，每个 Skill 对应一个特定的创作任务：

- **SceneDraftSkill**：场景草稿生成技能
- **SkillOrchestrator**：Skill 编排器，负责调度执行

### Prompt 模板

Prompt 模板存储在 `prompts/` 目录下，采用结构化格式定义：

```
Category: {category}
Version: {version}
system
instruction
context
output_format
```

### Story Context

StoryContext 是构建生成上下文的结构化数据，包含：

- 项目概要
- 近章节概要
- 参与角色卡
- 世界观规则
- 文风要求
- 场景目标
- 冲突设计
- 情感曲线

## 快速开始

### 环境要求

- .NET 10.0 SDK
- 本地部署的 LLM 服务（如 GPT-OSS）

### 配置

在 `appsettings.json` 中配置本地模型端点：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "LLM": {
    "BaseUrl": "http://localhost:8080"
  }
}
```

### 运行

```bash
cd src/MuseSpace.Api
dotnet run
```

### API 调用示例

生成场景草稿：

```bash
curl -X POST http://localhost:5000/api/draft/scene \
  -H "Content-Type: application/json" \
  -d '{
    "storyProjectId": "uuid-here",
    "sceneGoal": "主角与反派首次对峙",
    "conflict": "双方为争夺神秘文物展开激烈交锋",
    "emotionCurve": "紧张对抗 -> 意外转折",
    "involvedCharacterIds": ["uuid1", "uuid2"]
  }'
```

## 当前阶段

项目目前处于 **Phase 1：项目框架搭建阶段**。已完成的模块包括：

- 解决方案与项目结构
- Domain 层最小模型
- Application 层抽象接口
- Skill 骨架
- Prompt 骨架
- Context Builder 骨架
- API 骨架

## 文档

更多详细文档请参考：

- [技术方案](./Plan.md)
- [开发规划](./docs/DevelopmentPlan.md)
- [新手入门指南](./docs/GettingStarted.md)
- [Prompt 模板规范](./docs/PromptConvention.md)

## 许可证

本项目仅供学习交流使用。