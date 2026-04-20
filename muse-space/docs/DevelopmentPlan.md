# MuseSpace 开发规划留档

> 该文档用于沉淀当前已确认的项目开发规划，作为后续实现、拆解任务与阶段验收的依据。

---

## 1. 项目定位

`MuseSpace` 是一个面向小说创作场景的本地 AI 工作流系统。

项目目标是：

- 使用 `.NET10` 搭建创作工作流后端
- 通过本地部署的 `gpt-oss` 作为生成引擎
- 通过 `Prompt`、`Skill`、`Memory`、上下文构建和工具编排完成创作任务
- 支持后续逐步扩展为可维护、可扩展的创作平台

一句话概括：

**`.NET` 负责系统与流程，`gpt-oss` 负责生成。**

---

## 2. 已确认的总体设计原则 

### 2.1 分阶段开发
项目采用**分阶段推进**方式。

原则：

- 先确认阶段目标，再进入实现
- 第一版以简单、可理解、可维护为优先
- 先做骨架，再逐步补能力
- 避免一开始把数据库、向量检索、评测、复杂工作流全部做进来

### 2.2 面向单人开发维护
该项目当前以**单人开发**为前提。

因此第一版设计强调：

- 结构清晰
- 命名直观
- 日志简单可读
- 不引入过重的抽象和基础设施
- 优先保证自己后续容易看懂和迭代

### 2.3 技术方向确认
当前已确认的技术方向如下：

- 使用分层架构：`Api` / `Application` / `Domain` / `Infrastructure`
- `Application` 层采用**普通 Application Service** 方式
- 不使用 `MediatR` 作为第一版基础组织方式
- 通过 `ISkillOrchestrator` 统一调用 Skill，以便解耦
- Prompt 模板优先使用文件系统：`prompts/`
- 模型输出**优先 JSON**
- API 与内部调用链统一使用异步设计
- 数据库先预留扩展口，不在第一阶段重投入

---

## 3. 系统总体方向

### 3.1 核心架构思路
系统采用“创作工作流系统 + 本地模型服务”的结构。

- `.NET` 后端负责业务编排
- 本地模型服务负责文本生成
- Prompt 负责任务约束
- Skill 负责能力模块化
- Memory / Context Builder 负责上下文组织
- 日志负责留痕与问题定位

### 3.2 业务核心判断
项目的业务核心不是模型本身，而是：

- 创作资料管理
- 工作流编排
- 上下文构建
- Skill 模块化
- 输出约束与解析
- 状态沉淀与后续回写

### 3.3 第一性原则
在当前阶段，优先级排序如下：

1. 项目结构正确
2. 抽象边界清楚
3. 调用链清晰
4. 后续可扩展
5. 暂不追求一步跑通全部业务

---

## 4. 完整开发路线图总览

项目按 **6 个阶段** 推进，每个阶段都以“有明确边界、可独立验收”为原则。

### 4.1 阶段总览

| 阶段 | 名称 | 核心目标 |
|---|---|---|
| Phase 0 | 规划与设计冻结 | 把方向、范围、边界、命名和路线先定下来 |
| Phase 1 | 项目框架搭建 | 把分层骨架、接口和主链路结构搭起来，不要求跑通 |
| Phase 2 | 最小生成链路跑通 | 打通一次 `SceneDraft` 的真实调用闭环 |
| Phase 3 | 创作资料与上下文能力补齐 | 补角色卡、章节摘要、世界规则等 Memory 基础能力 |
| Phase 4 | 创作工作流能力扩展 | 增加改稿、一致性检查、更多 Skill |
| Phase 5 | 工程化与长期演进 | 数据、评测、版本管理、可维护性增强 |

### 4.2 阶段推进原则

- 不跨阶段强行提前实现复杂能力
- 每阶段只解决该阶段最核心的问题
- 当前阶段未完成前，不轻易扩大范围
- 先稳定结构，再追求效果
- 先让系统“能演进”，再让系统“很强大”

---

## 5. Phase 0：规划与设计冻结阶段

### 5.1 阶段目标
在正式编码前，把项目的核心路线、边界和关键设计决策留档，避免开发过程中不断返工。

### 5.2 本阶段关注点

- 项目目标与范围确认
- 第一阶段到长期阶段的拆解
- 架构分层确认
- 技术选型确认
- Skill、Prompt、Context Builder 的方向确认
- 第一版简化策略确认

### 5.3 交付物

- `Plan.md`
- `docs/DevelopmentPlan.md`
- 第一阶段边界说明
- 阶段性目标清单

### 5.4 本阶段不包含

- 实际编码实现
- 接模型
- 接数据库
- 接口跑通验证

### 5.5 完成标准

- 项目目标一致
- 阶段目标一致
- 第一阶段边界清楚
- 后续阶段路线可执行

---

## 6. Phase 1：项目框架搭建阶段

### 6.1 阶段目标
**本阶段目标是把项目框架搭出来，不要求完整跑通。**

重点不是业务可用，而是先把结构、接口和调用边界定住。

### 6.2 本阶段要解决的问题

- 分层项目如何组织
- 主链路如何贯通
- Skill 如何被调度
- Prompt 如何被加载和渲染
- Context Builder 如何接入
- 模型客户端和日志如何留扩展口

### 6.3 计划范围

#### 1. 解决方案与项目结构

```text
src/
  MuseSpace.Api/
  MuseSpace.Application/
  MuseSpace.Domain/
  MuseSpace.Infrastructure/
  MuseSpace.Contracts/

docs/

prompts/
  drafting/

tests/
  MuseSpace.UnitTests/
```

#### 2. Domain 层最小模型

- `StoryProject`
- `Chapter`
- `Scene`
- `Character`
- `WorldRule`
- `StyleProfile`
- `GenerationRecord`

#### 3. Application 层抽象接口

- `ISkillOrchestrator`
- `IStoryContextBuilder`
- `IPromptTemplateProvider`
- `IPromptTemplateRenderer`
- `ILlmClient`
- `IGenerationLogService`
- `GenerateSceneDraftAppService`

#### 4. Skill 骨架

- `ISkill`
- `SceneDraftSkill`
- `SkillOrchestrator`

#### 5. Prompt 骨架

- `prompts/drafting/scene-v1.md`
- 文件系统加载
- 模板渲染
- 基础版本标识

#### 6. Context Builder 骨架

默认注入规则：

- 项目背景摘要：1 份
- 最近章节摘要：最多 3 章
- 涉及角色：最多 4 人
- 世界规则：最多 8 条
- 风格要求：1 份
- 当前 Scene 目标：必填

#### 7. API 骨架

- `POST /api/draft/scene`
- 使用异步签名
- 允许暂时为 stub / placeholder 实现

#### 8. 日志骨架

记录最小生成日志：

- `RequestId`
- `TaskType`
- `SkillName`
- `PromptName`
- `PromptVersion`
- `ModelName`
- `DurationMs`
- `Success`
- `ErrorMessage`
- `InputPreview`
- `OutputPreview`
- `CreatedAt`

### 6.4 本阶段交付物

- 分层项目骨架
- 基础目录结构
- 主要接口定义
- `SceneDraft` 调用链骨架
- Prompt 文件入口
- 异步 API 骨架
- 简单日志骨架

### 6.5 本阶段不包含

- 真实模型调用可用性保证
- 数据库正式接入
- Revision
- Consistency Check
- Worker
- 向量检索
- UI

### 6.6 完成标准

- 解决方案和项目结构建立完成
- 各层命名与职责基本稳定
- 核心接口和核心类完成初版定义
- `SceneDraft` 主链路代码骨架存在
- API、Application、Skill、Prompt、LLM Client 之间关系明确
- 即使暂时不能完整运行，也不影响进入下一阶段

---

## 7. Phase 2：最小生成链路跑通阶段

### 7.1 阶段目标
在 Phase 1 的结构基础上，打通一次真实的 `SceneDraft` 生成链路，让系统第一次具备“能生成”的能力。

### 7.2 本阶段要解决的问题

- 如何真实调用本地模型服务
- 如何让 Prompt、Context、Skill 串起来
- 如何接收并解析模型输出
- 如何返回给 API 调用方

### 7.3 计划范围

- `LocalModelClient` 可实际调用本地 `gpt-oss`
- `SceneDraftSkill` 跑通调用
- Prompt 模板实际参与生成
- `StoryContextBuilder` 产出可用上下文
- `GenerateSceneDraftAppService` 完成一次真实编排
- 简单结构化输出解析
- 基础异常处理与失败日志

### 7.4 本阶段交付物

- 可运行的 `SceneDraft` 生成流程
- 一份可工作的 Prompt 模板
- 一套最小请求/响应 DTO
- 一份最小调用示例

### 7.5 本阶段不包含

- 多 Skill 协同
- 数据库存储正式落地
- 完整角色管理后台
- 一致性检查
- 复杂评测

### 7.6 完成标准

- 至少一个创作请求可以从 API 到模型调用完整跑通
- 可以拿到结构化或正文输出
- 输出失败时有基础日志可定位
- 主链路具备继续扩展的稳定结构

---

## 8. Phase 3：创作资料与上下文能力补齐阶段

### 8.1 阶段目标
让系统不只是“能生成”，而是开始具备“带资料、带上下文地生成”的能力。

### 8.2 本阶段要解决的问题

- 角色卡如何参与生成
- 章节摘要如何参与上下文
- 世界规则如何作为约束输入
- Memory 如何先以轻量方式组织

### 8.3 计划范围

- 角色卡基础模型与读取能力
- 章节摘要基础模型与读取能力
- 世界规则基础模型与读取能力
- Context Builder 从静态占位升级为真实拼装
- 继续保持数据库无强依赖，可先文件 / 内存实现
- 输出 JSON 结构进一步规范化

### 8.4 本阶段交付物

- 最小角色资料能力
- 最小章节摘要能力
- 最小世界规则能力
- 可用的上下文拼装逻辑
- 一份上下文注入策略说明

### 8.5 本阶段不包含

- 复杂知识图谱
- 向量检索
- 状态自动回写
- 高级时间线引擎

### 8.6 完成标准

- 生成时可以带入角色卡、摘要、规则等基础资料
- 上下文构建不再只是 stub
- 生成结果开始体现资料约束

---

## 9. Phase 4：创作工作流能力扩展阶段

### 9.1 阶段目标
从单一的 `SceneDraft` 能力，扩展为更接近实际创作工作流的系统。

### 9.2 本阶段要解决的问题

- 如何支持改稿
- 如何做一致性检查
- 如何让 Skill 体系逐渐成型

### 9.3 计划范围

- `RevisionSkill` 初版
- `ConsistencyCheckSkill` 初版
- 统一 Skill 请求/结果结构
- 统一 JSON 输出结构规范
- 改稿与检查相关 API
- 基础 Prompt 分类与版本命名规则

### 9.4 本阶段交付物

- 改稿能力初版
- 一致性检查能力初版
- 至少 3 类核心 Prompt 模板
- 更完整的 Skill 编排骨架

### 9.5 本阶段不包含

- 自动多代理协同
- 高级规则引擎
- 大规模评测平台

### 9.6 完成标准

- 可以对已有文本进行基础改稿
- 可以返回基础一致性检查结果
- Skill 不再只有单一生成能力

---

## 10. Phase 5：工程化与长期演进阶段

### 10.1 阶段目标
在功能逐渐齐全后，把系统从“原型工具”提升到“可长期维护的工程项目”。

### 10.2 本阶段要解决的问题

- 数据存储如何正式落地
- Prompt 如何做版本管理
- 如何做回归评测
- 如何让系统更稳定、更可维护

### 10.3 计划范围

- 数据库接入
- Repository 实现
- Prompt 元数据管理
- Evaluation / 回归样例
- Worker 雏形
- 更细粒度日志与错误分类
- 向量检索扩展口预留

### 10.4 本阶段交付物

- 数据持久化方案
- Prompt 版本管理方案
- 基础评测样例集
- 更完善的可观测性

### 10.5 本阶段不包含

- 自动训练平台
- 多租户平台化
- 完整出版排版系统

### 10.6 完成标准

- 核心数据可持久化
- Prompt 与输出质量可以持续回归比较
- 系统具备继续扩展为长期项目的基础

---

## 11. 跨阶段关键设计决策

### 8.1 Skill 编排
已确认：

- 通过 `ISkillOrchestrator` 统一调度 Skill
- Skill 之间保持解耦
- 应用层不直接耦合具体模型细节

### 8.2 Application 风格
已确认：

- 使用普通 `Application Service`
- 不引入 `MediatR`
- 保持结构简单直观

### 8.3 Prompt 存储
已确认：

- 第一版使用 `prompts/` 文件系统
- 后续如有需要再扩展数据库版本

### 8.4 输出格式
已确认：

- 模型输出优先使用 JSON
- 文本类输出也尽量采用结构化外包裹方式设计
- 后续允许加入重试或降级策略

### 8.5 异步设计
已确认：

- API 使用异步
- Application Service 使用异步
- Skill 与模型客户端使用异步
- 第一版先做“代码异步”，不强制做复杂后台任务机制

### 8.6 数据策略
已确认：

- 第一阶段先不重投入数据库
- 先把接口和扩展口留出来
- 后续再接具体数据库实现

### 8.7 数据建模简化原则
第一阶段优先简化，不做过度规范化。

建议：

- `Tags` 先用文本或集合表示
- `ForbiddenBehaviors` 先简单表示
- `PublicSecrets` / `PrivateSecrets` 先简单表示
- `InvolvedCharacterIds` 先用集合表达
- 后续接数据库时再决定是否拆分关联表

---

## 12. 第一阶段推荐目录草案

```text
src/
  MuseSpace.Api/
    Controllers/
    Extensions/

  MuseSpace.Application/
    Abstractions/
      Llm/
      Prompt/
      Skills/
      Story/
      Logging/
    Services/
      Drafting/
    Models/

  MuseSpace.Domain/
    Entities/
    ValueObjects/
    Enums/

  MuseSpace.Infrastructure/
    Llm/
    Prompt/
    Logging/
    Story/

  MuseSpace.Contracts/
    Draft/
    Common/

prompts/
  drafting/
    scene-v1.md

docs/
  DevelopmentPlan.md

tests/
  MuseSpace.UnitTests/
```

---

## 13. 第一阶段建议优先实现的核心类

建议第一阶段先把这些类和接口搭起来：

### 10.1 LLM 相关
- `ILlmClient`
- `LocalModelClient`

### 10.2 Prompt 相关
- `IPromptTemplateProvider`
- `FileSystemPromptTemplateProvider`
- `IPromptTemplateRenderer`
- `PromptTemplateRenderer`

### 10.3 Story / Context 相关
- `IStoryContextBuilder`
- `StoryContextBuilder`

### 10.4 Skill 相关
- `ISkillOrchestrator`
- `ISkill`
- `SceneDraftSkill`
- `SkillOrchestrator`

### 10.5 Application 相关
- `GenerateSceneDraftAppService`

### 10.6 API 相关
- `DraftController`
- `GenerateSceneDraftRequest`
- `GenerateSceneDraftResponse`

### 10.7 Logging 相关
- `IGenerationLogService`
- `GenerationLogService`
- `GenerationRecord`

---

## 14. 第一阶段任务拆解顺序

建议按下面顺序推进：

### 任务 1：建立解决方案与项目结构
- 创建分层项目
- 建立基础目录
- 建立项目引用关系

### 任务 2：定义 Contracts
- 定义 `Draft` 请求响应 DTO
- 定义基础返回结构

### 任务 3：定义 Domain 最小模型
- 建立最小实体
- 不追求字段完整

### 任务 4：定义 Application 抽象接口
- LLM 接口
- Prompt 接口
- Context Builder 接口
- Skill 接口
- Logging 接口

### 任务 5：实现 Infrastructure 占位类
- `LocalModelClient`
- `FileSystemPromptTemplateProvider`
- `GenerationLogService`

### 任务 6：实现 Skill 编排骨架
- `ISkill`
- `SceneDraftSkill`
- `ISkillOrchestrator`
- `SkillOrchestrator`

### 任务 7：实现 `GenerateSceneDraftAppService`
- 串联 Context、Prompt、Skill、Logging
- 暂可使用占位实现

### 任务 8：暴露 Draft API
- `POST /api/draft/scene`
- 保持异步签名

### 任务 9：补最小 Prompt 模板文件
- 新建 `prompts/drafting/scene-v1.md`

### 任务 10：整理文档与下一阶段入口
- 更新当前规划
- 为第二阶段留出接模型和数据存储的位置

---

## 15. 当前已确认的第一阶段边界

### 第一阶段明确要做
- 项目分层骨架
- 核心抽象接口
- `SceneDraft` 调用链框架
- Prompt 文件系统入口
- 异步 API 骨架
- 简单日志骨架

### 第一阶段明确不做深
- 数据库正式接入
- 改稿能力
- 一致性检查能力
- Worker
- 向量检索
- 评测体系
- 复杂权限与安全体系

### 第一阶段允许状态
- 可以存在占位实现
- 可以存在 mock / stub
- 可以存在未打通真实模型调用的情况
- 重点是结构和边界清晰

---

## 16. 各阶段进入条件与验收方式

### 16.1 Phase 0 -> Phase 1

进入条件：

- 规划文档已确认
- 第一阶段边界已确认

验收方式：

- 文档冻结
- 开始搭建代码骨架

### 16.2 Phase 1 -> Phase 2

进入条件：

- 分层结构已建立
- 核心接口已定义
- `SceneDraft` 主链路骨架已存在

验收方式：

- 查看项目结构
- 查看接口与类关系

### 16.3 Phase 2 -> Phase 3

进入条件：

- `SceneDraft` 真实调用跑通
- 最小返回结果已可见

验收方式：

- 接口演示
- 最小日志验证

### 16.4 Phase 3 -> Phase 4

进入条件：

- 上下文可以真实带入角色、摘要、规则

验收方式：

- 对比无上下文与有上下文的输出差异

### 16.5 Phase 4 -> Phase 5

进入条件：

- 核心工作流已成型
- 不再只是单点能力原型

验收方式：

- 功能清单审查
- 关键调用链验证

---

## 17. 下一步执行建议

当前最合理的下一步是：

**按本规划进入 Phase 1 详细设计，输出项目目录树、类清单与第一批落地任务。**

建议下一份产物为：

1. 第一阶段项目目录树
2. 第一阶段类清单
3. 第一阶段接口定义草案
4. 第一阶段任务清单（可直接开发）

---

## 18. 结论

当前开发策略已经明确：

- 先分阶段
- 先搭框架
- 第一阶段不要求跑通
- 第二阶段再追求最小生成闭环
- 第三阶段补资料与上下文
- 第四阶段扩展创作工作流能力
- 第五阶段做工程化与长期演进

该文档作为当前讨论结果的正式留档，可作为后续开发与验收依据。
