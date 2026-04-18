# Vue 3 项目搭建模板大纲

> 使用此文档指导 AI 搭建标准化 Vue 3 企业级前端项目。

---

## 技术栈

- **框架**：Vue 3 + TypeScript
- **构建工具**：Vite（最新版）
- **状态管理**：Pinia
- **路由**：Vue Router 4
- **HTTP**：Axios（二次封装）
- **CSS 工具**：UnoCSS
- **代码规范**：ESLint + Prettier
- **自动导入**：unplugin-auto-import + unplugin-vue-components
- **Git 规范**：Husky + lint-staged + Commitlint（Conventional Commits）

---

## 一、初始化

- 使用 `npm create vite@latest` 选择 `vue-ts` 模板
- 删除默认生成的 `style.css`、`HelloWorld.vue`、`App.vue` 默认内容

---

## 二、依赖安装

### 运行时依赖
- `vue-router@4`
- `pinia`
- `axios`

### 开发依赖
- `unocss` + `@unocss/preset-uno` + `@unocss/preset-attributify` + `@unocss/preset-icons` + `@unocss/transformer-directives`
- `unplugin-auto-import` + `unplugin-vue-components`
- `eslint` + `typescript-eslint` + `eslint-plugin-vue` + `eslint-plugin-prettier` + `eslint-config-prettier` + `@eslint/js` + `prettier`
- `husky` + `lint-staged`
- `@commitlint/cli` + `@commitlint/config-conventional`

---

## 三、目录结构

在 `src/` 下创建以下目录：

| 目录 | 用途 |
|------|------|
| `api/` | 接口请求函数，按模块拆文件 |
| `assets/` | 静态资源（图片、字体等） |
| `components/` | 公共组件（自动导入范围） |
| `hooks/` | 自定义组合式函数 |
| `router/` | Vue Router 配置 |
| `store/` | Pinia 入口 + `modules/` 子模块 |
| `styles/` | 全局样式（含 `reset.css`） |
| `utils/` | 工具函数（含 Axios 封装） |
| `views/` | 页面级组件 |

---

## 四、配置文件清单

| 文件 | 说明 |
|------|------|
| `vite.config.ts` | 路径别名 `@`、dev server proxy、生产构建分包、注册插件 |
| `uno.config.ts` | UnoCSS：presetUno + presetAttributify + presetIcons + transformerDirectives，含常用 shortcuts |
| `tsconfig.app.json` | 添加 `paths` 支持 `@` 别名，`include` 覆盖 `.d.ts` |
| `eslint.config.js` | flat config 格式，集成 ts + vue + prettier 规则 |
| `.prettierrc` | 无分号、单引号、100 列、trailing comma、endOfLine auto |
| `.prettierignore` | 忽略 `dist/`、`node_modules/`、自动生成的 `.d.ts` |
| `commitlint.config.js` | 继承 `@commitlint/config-conventional`，枚举允许的 type |

---

## 五、package.json 配置要点

### scripts
- `dev` / `build` / `preview` / `build:staging`（多环境构建）
- `lint`：`eslint . --fix`
- `format`：`prettier --write "src/**/*.{ts,tsx,vue,css,json}"`
- `prepare`：`husky`（安装后自动初始化 hooks）

### lint-staged
- `*.{ts,tsx,vue}` → eslint fix + prettier write
- `*.{css,json,md}` → prettier write

---

## 六、环境变量

| 文件 | 环境 |
|------|------|
| `.env` | 所有环境通用（如 `VITE_APP_TITLE`） |
| `.env.development` | 开发环境 API 地址 |
| `.env.staging` | 测试环境 API 地址 |
| `.env.production` | 生产环境 API 地址 |

- 所有变量以 `VITE_` 开头
- 在 `src/env.d.ts` 中为 `ImportMetaEnv` 补充类型声明

---

## 七、核心模块实现

### `src/main.ts`
- 创建 app，`use(pinia)`、`use(router)`、`mount('#app')`
- 引入 `reset.css` 和 `virtual:uno.css`

### `src/router/index.ts`
- `createWebHistory` 模式
- 路由全部使用动态 `import()` 懒加载
- 添加 404 通配路由

### `src/store/index.ts`
- `createPinia()` 导出实例
- 各业务 store 拆分到 `modules/` 下，使用 Setup Store 风格

### `src/utils/request.ts`（Axios 封装）
- 基于 `VITE_APP_BASE_API` 和 `timeout: 15000` 创建实例
- 请求拦截器：自动从 `localStorage` 读取 token 注入 `Authorization` header
- 响应拦截器：统一处理 401（清除 token 跳转登录）、403、404、500 等错误
- 导出 `get` / `post` / `put` / `delete` 泛型方法

### `src/styles/reset.css`
- `box-sizing: border-box`、清除 margin/padding
- 统一字体栈、去除列表样式、链接继承颜色
- `img` 设置 `max-width: 100%`

---

## 八、Husky + Git Hooks

```
git init
npx husky init
```

- `.husky/pre-commit`：执行 `npx lint-staged`
- `.husky/commit-msg`：执行 `npx --no-install commitlint --edit "$1"`

---

## 九、提交规范

格式：`<type>(<scope>): <subject>`

常用 type：`feat` / `fix` / `docs` / `style` / `refactor` / `perf` / `test` / `build` / `ci` / `chore` / `revert`

---

## 十、VSCode 插件

### 必装

| 插件名 | 插件 ID | 说明 |
|--------|---------|------|
| Vue - Official | `Vue.volar` | Vue 3 官方插件，语法高亮、类型检查、模板智能提示（含原 TypeScript Vue Plugin 功能） |
| ESLint | `dbaeumer.vscode-eslint` | 实时显示 ESLint 错误，保存自动修复 |
| Prettier - Code formatter | `esbenp.prettier-vscode` | 格式化，配合 `.prettierrc` 使用 |
| UnoCSS | `antfu.unocss` | UnoCSS class 智能提示与预览 |
| Iconify IntelliSense | `antfu.iconify` | UnoCSS Icons 图标悬停预览 |

### 推荐装

| 插件名 | 插件 ID | 说明 |
|--------|---------|------|
| Auto Rename Tag | `formulahendry.auto-rename-tag` | 自动同步修改 HTML/Vue 标签首尾 |
| Path Intellisense | `christian-kohler.path-intellisense` | 路径自动补全，配合 `@` 别名使用 |
| DotENV | `mikestead.dotenv` | `.env` 文件语法高亮 |
| Error Lens | `usernamehw.errorlens` | 错误/警告内联显示在代码行尾 |
| Vue VSCode Snippets | `sdras.vue-vscode-snippets` | 常用 Vue 代码片段（`vbase`、`vfor` 等） |

### `.vscode/settings.json`（随项目提交，统一团队编辑器行为）

```json
{
  "editor.defaultFormatter": "esbenp.prettier-vscode",
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll.eslint": "explicit"
  },
  "eslint.validate": ["javascript", "typescript", "vue"],
  "[vue]": {
    "editor.defaultFormatter": "esbenp.prettier-vscode"
  },
  "typescript.tsdk": "node_modules/typescript/lib"
}
```

---

## 十一、注意事项

1. **TypeScript 版本**：若使用 TS 6.x，`tsconfig` 中 `paths` 不需要配合 `baseUrl`，直接写相对路径即可
2. **自动导入生成文件**：`src/auto-imports.d.ts` 和 `src/components.d.ts` 由插件自动生成，加入 `.eslintignore` 和 `.prettierignore`
3. **UnoCSS 引入**：`main.ts` 中必须 `import 'virtual:uno.css'`，`vite.config.ts` 中注册 `UnoCSS()` 插件
4. **Vite proxy**：`server.proxy` 中配置 `/api` 代理，`rewrite` 去掉前缀，后端地址按实际修改
5. **多环境构建**：`build:staging` 使用 `vite build --mode staging`，会加载 `.env.staging`
