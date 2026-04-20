# Phase 3：创作资料与上下文能力补齐

> 本文档记录 Phase 3 的实施计划，作为开发过程与验收的依据。

---

## 1. 阶段目标

让系统从"能生成"进化为"带资料、带上下文地生成"。

**一句话目标：生成时能携带真实的故事背景、角色卡、世界规则和风格要求，而不再是空上下文。**

---

## 2. 已确认的设计决策

| 项 | 决策 |
|---|---|
| 数据持久化 | JSON 文件，与 `prompts/` 同级存放在 `data/` 目录 |
| 数据目录结构 | `data/projects/{projectId}/` 下按实体类型分文件 |
| 管理 API | Phase 3 同时交付增删查接口（增 + 列表 + 详情） |
| StoryProject | Phase 3 包含项目创建接口 |
| 新引入库 | Serilog（日志）、Scrutor（DI 扫描）、Mapster（对象映射） |
| 上下文预算 | 最多 3 章摘要、4 个角色、8 条世界规则 |

---

## 3. 新增库说明

### Serilog（`Serilog.AspNetCore 10.0.0`）
替换基础设施日志 `ILogger` 的后端实现，提供：
- 结构化控制台彩色输出
- 按天滚动写入日志文件（`logs/app-*.log`）
- 请求上下文自动追踪

**注意：** `IGenerationLogService` 的业务生成日志保持不变，Serilog 只替换运维日志通道。

### Scrutor（`Scrutor 7.0.0`）
程序集扫描批量注册 DI，约定：
- `Json*Repository` 类 → 按接口注册，Scoped
- `*AppService` 类 → 注册为自身，Scoped
- `ISkill` 实现 → 按接口注册，Scoped

### Mapster（`Mapster 10.0.7`）
对象映射。使用约定：
- 简单同名字段映射：用 `Adapt<T>()` 内联调用
- 有逻辑的映射：写显式 `ToResponse()` 扩展方法

---

## 4. 数据目录结构

```text
data/
└── projects/
    └── {projectId}/
        ├── project.json         # StoryProject
        ├── characters.json      # List<Character>
        ├── world-rules.json     # List<WorldRule>
        ├── chapters.json        # List<Chapter>（摘要为主）
        └── style-profile.json   # StyleProfile（单个）
```

---

## 5. 任务清单

### 3-A：基础设施 + 数据层

#### Task 1：引入三个新库
- `MuseSpace.Api.csproj` → Serilog.AspNetCore、Serilog.Sinks.File、Scrutor
- `MuseSpace.Application.csproj` → Mapster

#### Task 2：配置 Serilog
- 更新 `Program.cs`（UseSerilog）
- 更新 `appsettings.json`（Serilog + Data 配置节）

#### Task 3：定义仓储接口（Application 层）
- `IStoryProjectRepository`
- `ICharacterRepository`
- `IWorldRuleRepository`
- `IChapterRepository`
- `IStyleProfileRepository`

#### Task 4：JSON 文件仓储实现（Infrastructure 层）
- `DataOptions`（配置 data 路径）
- `JsonRepositoryBase<T>`（通用读写帮助类）
- `JsonStoryProjectRepository`
- `JsonCharacterRepository`
- `JsonWorldRuleRepository`
- `JsonChapterRepository`
- `JsonStyleProfileRepository`

#### Task 5：升级 `StoryContextBuilder`
从 stub 升级为真实拼装：
- 加载项目摘要
- 加载最近 3 章 Summary
- 加载最多 4 个角色（格式化为角色卡字符串）
- 加载最多 8 条世界规则（按 Priority 排序）
- 加载风格要求

#### Task 6：更新 DI（Scrutor）
- 使用程序集扫描替代手动逐条注册

---

### 3-B：管理 API

#### Task 7：Contracts（DTOs）
每种实体：`CreateXxxRequest` + `XxxResponse`

#### Task 8：Application Services
- `StoryProjectAppService`
- `CharacterAppService`
- `WorldRuleAppService`
- `ChapterAppService`
- `StyleProfileAppService`

#### Task 9：Controllers
- `StoryProjectsController` → `GET/POST /api/projects`
- `CharactersController` → `GET/POST/DELETE /api/projects/{id}/characters`
- `WorldRulesController` → `GET/POST/DELETE /api/projects/{id}/world-rules`
- `ChaptersController` → `GET/POST/DELETE /api/projects/{id}/chapters`
- `StyleProfileController` → `GET/PUT /api/projects/{id}/style-profile`

---

## 6. 实施顺序

```
Task 1（包）→ Task 2（日志）→ Task 3（接口）→ Task 4（实现）
→ Task 5（上下文升级）→ Task 6（DI）→ Task 7（DTO）
→ Task 8（服务）→ Task 9（控制器）→ 构建验证 → 端到端验收
```

---

## 7. 本阶段边界

### 做
- JSON 文件存储
- 5 类实体的增删查 API
- 真实上下文拼装
- Serilog 结构化日志
- Scrutor 批量 DI 注册

### 不做
- 数据库
- 编辑（Update）接口（Phase 5 补）
- 向量检索
- 自动摘要回写
- 复杂角色筛选逻辑

---

## 8. 验收标准

- [ ] 可以创建故事项目、角色、世界规则、章节摘要、风格配置
- [ ] 生成场景时，上下文中包含真实的项目摘要、角色卡、世界规则
- [ ] Serilog 控制台输出结构化日志，`logs/app-*.log` 有日志文件
- [ ] `POST /api/draft/scene` 带上真实数据后，生成结果体现资料约束
