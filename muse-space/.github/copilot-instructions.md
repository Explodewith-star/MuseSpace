# Copilot Instructions

## 项目指南
- For this project, prefer phased development with a confirmed stage plan first; keep the first version simple and understandable for a solo developer; use `ISkillOrchestrator` for decoupling, ordinary Application Services instead of MediatR, prompt templates from the `prompts/` filesystem, prefer JSON outputs from the model, and design APIs/workflows asynchronously.

- Model output uses JSON format; the backend does lenient parsing (don't fail the entire request on parse error).
- API keys should be in local-only config (appsettings.Development.json or user secrets), not committed to source control.
- Every code submission to the Git repository (including git commit and git push) must first obtain explicit user consent and cannot be executed automatically.
- 用户希望在该项目中遵循更严格的代码规范：尽量保证每个 .cs 文件单类、调用层级清晰，以通过后续代码审核。

## 注释要求
- 注释应该是自然语言的描述，避免机械化或 AI 风格的注释。每个方法上方应该有一句简洁明了的注释，说明该方法的功能和目的。
- 注释应该清晰、简洁，避免冗长或过于复杂的描述。注释应该直接说明方法的作用，而不是重复代码的内容。

## 拒绝重复造轮子
- 先检索再编码： 在实现任何新功能（如 Service 方法、工具类、Vue 组件）之前，必须优先检索现有代码库中是否已存在类似逻辑。
- 优先复用： 优先调用已有的实体配置（Entity Configurations）、DTO 映射规则及公用扩展方法。
- 禁止冗余： 除非现有方法无法满足业务逻辑且不可扩展，否则严禁编写重复的逻辑片段。

## 深度功能链条
- 拒绝原子化思维： 当我要求实现一个功能（如“删除”）时，你不仅要处理该表本身，还要执行“全链路影响评估”，并且将得到的信息反馈出来看是否思路是对的，得到用户的确认后再进行。
- 关联处理： * 数据库层面： 检查外键关联。是级联删除（Cascade）、设为 Null，还是需要手动清理关联的附件/多对多中间表？业务层面： 如果删除了一个“素材”，其关联的“任务记录”或“生成的视频片段”是否需要同步处理？文件系统： 如果记录关联了本地存储或云端资源（如 SSD 上的模型权重、生成的视频文件），必须包含清理物理文件的逻辑。

## 技术栈规范
- 前端 (Vue 3 & TS)： * 尽量复用已有的全局组件和 Hooks，保持 UI 交互一致性（如加载状态、错误通知，如果做一个新的功能需要考虑是否要做新的组件或者函数等解耦可复用的东西，并向用户提出申请，说出思路，得到肯定后可以封装新的组件或者函数。

## 思考模板
- 现有组件/方法复用： [列出你打算复用的已有代码]、逻辑边界评估： [说明该功能会触及的关联表、文件系统或第三方 API 状态]、潜在风险点： [如并发冲突、孤立数据残留等]