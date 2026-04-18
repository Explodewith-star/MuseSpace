# MuseSpace 网页端前端开发规范

本文档记录 MuseSpace Web 端项目的技术栈、开发规范和架构设计，作为所有前端开发的准则。

## 1. 技术栈

- 核心框架：[Vue 3](https://vuejs.org/) (Composition API, `<script setup>`)
- 语言：[TypeScript](https://www.typescriptlang.org/)
- 构建工具：[Vite](https://vitejs.dev/)
- 状态管理：[Pinia](https://pinia.vuejs.org/)
- 路由：[Vue Router 4](https://router.vuejs.org/)
- 样式/CSS：[UnoCSS](https://unocss.dev/) (取代 Tailwind/Tailwind组件库)
- HTTP 客户端：[Axios](https://axios-http.com/)
- 图标库：[@iconify-json/lucide](https://icones.js.org/collection/lucide) (作为 UnoCSS 的图标预设使用)

## 2. 目录结构

项目目录遵循按功能模块划分的原则：

```
src/
├── api/             # 按数据实体划分的 HTTP 接口 (例如: projects.ts)
├── assets/          # 静态资源 (图片, 字体等)
├── components/      # UI 组件
│   ├── base/        # 基础/全局通用组件 (App 开头，如 AppButton)
│   ├── layout/      # 布局组件 (AppLayout, AppSidebar 等)
├── composables/     # 全局复用的组合式函数 Hooks (use 开头，如 useToast)
├── router/          # 路由配置
│   └── modules/     # 模块化路由
├── store/           # Pinia 状态树
│   └── modules/     # 模块化 store
├── styles/          # 全局样式，CSS Variables
├── theme/           # 主题配置与切换逻辑
├── types/           # 全局类型声明 (models.ts, api.ts)
└── views/           # 页面级组件 (按路由结构组织)
    ├── home/        # 首页
    └── projects/    # 项目内部模块
        ├── chapters/
        ├── characters/
        ├── draft/
        ├── overview/
        ├── style-profile/
        └── world-rules/
```

## 3. UI 与主题系统

本系统 **不使用任何第三方组件库 (如 Element Plus, Ant Design 等)**，所有组件均为手工通过基础的 HTML/CSS 和 UnoCSS 构建。

### 3.1 主题实现
- 使用 **CSS 变量 (CSS Variables)** 进行主题管理，定义在 `:root` 和 `html.dark` 中。
- 主题色 token 定义见 `src/theme/tokens.ts`，由 `src/theme/index.ts` 动态向文档注入 `<style data-theme-tokens>`。
- 默认支持 **浅色 (Light)** 模式，用户可手动切换到 **深色 (Dark)** 模式。
- 变量命名规范：使用 `--color-*`，例如 `--color-bg-base`, `--color-primary`, `--color-text-muted`。

### 3.2 基础组件使用法则
所有基础通用组件均放置在 `src/components/base/` 目录下，且命名以 `App` 开头。

- **`AppButton`**: 按钮。支持四个 `variant` (`primary`, `secondary`, `danger`, `ghost`) 和 `loading` 状态。
- **`AppInput` / `AppTextarea`**: 表单输入，内置 `label` 属性用于表单项标题。
- **`AppDrawer`**: 侧边抽屉容器，主要用于新建/编辑表单。
- **`AppConfirm`**: 确认对话框，主要用于二次确认删除等危险操作。
- **`AppToast`**: 全局轻提示，通过 `src/composables/useToast.ts` 在代码中调用。

### 3.3 样式编写规范
- 对于简单布局和工具类样式：**优先使用 UnoCSS 工具类**。
- 对于复杂的组件组件结构或组件内部特定交互态 (如伪类、复杂选择器 `:hover .child`)：**使用原生的 `<style scoped>` 块编写**，保持组件样式隔离。

## 4. API 层与网络请求

- 封装好的 `axios` 实例位于 `src/api/http.ts`，全局已配置统一的错误提示，并会自动拆包 `ApiResponse<T>.data`。
- API 定义在各个独立的模块中（如 `src/api/projects.ts`），不允许在 View 中直接调用 axios。
- **Silent 模式**：如果你不希望在请求失败/返回非 200 状态码时展示全局的报错 Toast（例如查询可能不存在的资源，404 是预期的业务流程），可以在请求参数里传入 `{ silent: true }`，如：`http.get('/path', { silent: true })`。

## 5. 视图 (Views) 与业务逻辑封装

对于复杂的页面级功能组件，采用 **逻辑拆分模式** 保持 Vue 文件可读性：

- **`index.vue`**: 仅包含 `<template>` 和 `<style>`，以及对 `hooks.ts` 返回状态与方法的解构调用。**绝不在此文件编写大段 `async` 业务方法**。
- **`hooks.ts`**: 用于导出一个 `initXXXState()` 函数，返回页面所需要的所有 `ref`, `reactive` 以及 `methods`。所有组件生命周期的勾子（`onMounted` 等）都在这里执行。
- **`types.ts`**: (如果有需要) 定义当前页面专门使用的类型接口（例如：新建表单结构、查询条件定义）。

> **Note**: `src/composables/` 和 `hooks.ts` 的区别：
> `composables/useXXX.ts` (如 `useToast`) 用于**跨页面复用的全局能力**。
> 本地模块内的 `hooks.ts` (如 `initHomeState`) 仅针对这一个具体视图打包相关状态。

## 6. 图标与视觉图标使用

- 我们使用 `@iconify-json/lucide` 配合 UnoCSS。
- 所有图标的使用格式统一为 `<i class="i-lucide-{icon-name}" />`。
- 例如：`<i class="i-lucide-settings" />`，`<i class="i-lucide-trash-2" />`。

## 7. 约定与禁止项

- **桌面端优先**：不做复杂的响应式支持或移动端适配，所有界面的基础宽度以中/大屏幕桌面窗口为准。
- **纯粹**：所有 UI 交互元素、弹窗、Loading 都应保持克制和极简，不要无谓叠加花哨特效，保证应用体验具有效率软件的快速响应感。
- **类型安全**：前后端全栈强类型约束。后端如有数据结构更新，请及时同步对齐 `src/types/models.ts` 中的相关接口。
