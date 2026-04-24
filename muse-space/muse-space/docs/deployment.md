# MuseSpace 部署指南

> 目标服务器：`152.136.11.140`（腾讯云 Docker CE，ubuntu 用户）  
> 部署完成后访问地址：`http://152.136.11.140`

---

## 整体流程概览

```
① 本地推代码 → ② GitHub Actions 自动构建镜像并推送到 ghcr.io
                                        ↓
③ 服务器 git pull（同步配置）→ ④ docker compose pull（拉镜像）→ ⑤ docker compose up -d → ⑥ 浏览器访问
```

> **为什么用 ghcr.io 而不是 Docker Hub？**  
> 腾讯云大陆服务器无法访问 Docker Hub（`registry-1.docker.io` 超时），ghcr.io（GitHub Container Registry）在大陆服务器上可以正常访问。

---

## 一、本地准备（每次更新代码时执行）

推送代码后 GitHub Actions 会自动构建镜像，**约需 5～10 分钟**，等 Actions 页面出现绿色 ✅ 后再继续服务器操作。

```powershell
cd C:\Users\E1557417\Documents\WorkItem\StoryWorkFlow\MuseSpaceNew
git add .
git commit -m "你的提交说明"
# ⚠️ 推送前确认 .env 和 appsettings.Development.json 不在暂存区
git push origin main
```

---

## 二、服务器首次初始化（只做一次）

通过腾讯云控制台网页终端连接服务器。

---

### 步骤 1：授予 ubuntu 用户 Docker 权限

```bash
sudo usermod -aG docker $USER
newgrp docker
docker version   # 能看到版本号即成功
```

> ⚠️ 退出重新登录后如果又报 permission denied，重新执行 `newgrp docker`。

---

### 步骤 2：拉取代码

> ⚠️ 必须用 HTTPS 克隆，不要用 SSH（服务器未配 GitHub SSH 密钥，会卡住）。

```bash
git clone https://github.com/Explodewith-star/MuseSpace.git ~/musespace/code
cd ~/musespace/code/muse-space/muse-space
```

如果之前用 SSH 克隆了，改成 HTTPS：

```bash
cd ~/musespace/code
git remote set-url origin https://github.com/Explodewith-star/MuseSpace.git
```

---

### 步骤 3：创建生产环境配置文件

```bash
cp .env.example .env
nano .env
```

**将 `.env` 内容替换为以下完整配置（把 `YOUR_*` 换成真实值）：**

```
# GitHub 用户名（全小写）
GITHUB_USERNAME=explodewith-star

# ⚠️ 密码末尾不要有 "." 等特殊字符紧接 ";"，否则会被截断
DB_CONNECTION_STRING=Host=152.136.11.140;Port=6286;Database=musespace_dev;Username=msadmin;Password=YOUR_DB_PASSWORD;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;

# OpenRouter LLM（主渠道）
LLM_BASE_URL=https://openrouter.ai/api/v1
LLM_API_KEY=YOUR_OPENROUTER_API_KEY
LLM_MODEL_NAME=z-ai/glm-4.5-air:free

# DeepSeek LLM（备用渠道）
DEEPSEEK_API_KEY=YOUR_DEEPSEEK_API_KEY

# SiliconFlow 向量化（Embedding）
EMBEDDING_API_KEY=YOUR_SILICONFLOW_API_KEY

# ── 认证配置（必须填，否则后端 fail-fast 拒绝启动）──────────────────────────
# JWT 密钥：≥32 字符随机字符串
# 在服务器上执行 openssl rand -base64 48 可生成一个
AUTH_JWT_SECRET=YOUR_RANDOM_32PLUS_CHAR_SECRET
AUTH_USER_TOKEN_EXPIRY_DAYS=7
AUTH_ADMIN_TOKEN_EXPIRY_HOURS=24

# ── 管理员账号（首次部署可先留空，见下方说明）────────────────────────────────
ADMIN_PHONE_NUMBER=
ADMIN_PASSWORD_HASH=
```

**nano 操作：**`Ctrl+O` → 回车（保存）→ `Ctrl+X`（退出）

---

#### 首次配置管理员账号（首次部署后执行一次）

`ADMIN_PHONE_NUMBER` 和 `ADMIN_PASSWORD_HASH` 首次留空也可以启动，按以下步骤获取密码哈希后再填回：

```bash
# 1. 先启动服务（ADMIN_* 留空时跳过初始化，不影响启动）
docker compose up -d

# 2. 触发一次 admin-login，后端会在日志中打印 BCrypt 哈希
curl -X POST http://localhost/api/auth/admin-login \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber":"你的手机号","password":"你想设置的密码"}'

# 3. 查日志拿到哈希
docker compose logs api --tail=20

# 4. 把哈希填回 .env
nano .env
# ADMIN_PHONE_NUMBER=你的手机号
# ADMIN_PASSWORD_HASH=$2a$11$xxxxxx...（日志里输出的那串）

# 5. 重启让配置生效
docker compose up -d
```

---

### 步骤 4：开放腾讯云防火墙端口

登录腾讯云控制台 → **云服务器** → 实例 → **安全组** → **入站规则** → **添加规则**：
- 协议：`TCP`，端口：`80`，来源：`0.0.0.0/0`

---

### 步骤 5：登录 GitHub Container Registry

ghcr.io 的包默认私有，需要用 GitHub PAT 登录。

1. 打开 https://github.com/settings/tokens → **Generate new token (classic)**
2. Note 填 `musespace-deploy`，勾选 `read:packages`，点 **Generate token**
3. 复制 token（离开页面后不可再查看）

```bash
docker login ghcr.io -u explodewith-star -p 你的PAT_TOKEN
```

> **更简单的替代方案（免登录）**：GitHub → Profile → Packages → 找到 `musespace-api` / `musespace-web` → Settings → 改为 **Public**。

---

### 步骤 6：首次启动

> ⚠️ 确认 GitHub Actions 显示绿色 ✅ 再继续。  
> ⚠️ 所有 docker compose 命令必须在 `docker-compose.yml` 所在目录执行。

```bash
cd ~/musespace/code/muse-space/muse-space
git pull origin main
docker compose pull
docker compose up -d
```

---

### 步骤 7：验证部署是否成功

```bash
docker compose ps                      # STATUS 全为 Up
curl http://localhost/api/projects     # 返回 JSON 即后端正常
curl http://localhost/                 # 返回 HTML 即前端正常
```

浏览器打开 `http://152.136.11.140` 确认页面可以访问。

---

## 三、日常更新部署

### 标准流程（每次推代码后）

```bash
# 本地：推送代码，等 GitHub Actions 绿色 ✅（约 5～10 分钟）
git push origin main

# 服务器：
cd ~/musespace/code/muse-space/muse-space
git pull origin main      # 同步 docker-compose.yml 等配置变更（不能省！）
docker compose pull       # 拉取最新镜像
docker compose up -d      # 滚动更新（不中断服务）
```

### 有新环境变量时

当 `docker-compose.yml` 新增了 `${VAR}` 引用，需要先补充 `.env` 再重启：

```bash
# 查看本次是否有新变量
git diff HEAD~1 HEAD -- muse-space/muse-space/docker-compose.yml | grep "^\+" | grep '\${'

# 如有新变量，编辑 .env 追加后重启
nano .env
docker compose up -d
```

---

## 四、常用运维命令

```bash
cd ~/musespace/code/muse-space/muse-space

# 查看容器状态
docker compose ps

# 查看实时日志（Ctrl+C 退出）
docker compose logs -f

# 只看后端日志（最近 50 行）
docker compose logs api --tail=50

# 重启所有服务
docker compose restart

# 停止所有服务
docker compose down

# 验证容器实际收到的配置（排查变量是否生效）
docker exec musespace-api printenv ConnectionStrings__DefaultConnection
docker exec musespace-api printenv Auth__JwtSecret

# 进入数据库
docker exec -it musespace_db psql -U msadmin -d musespace_dev

# 修改数据库密码
docker exec -it musespace_db psql -U msadmin -d musespace_dev -c "ALTER USER msadmin WITH PASSWORD '新密码';"
```

---

## 五、常见错误处理

| 错误信息 | 原因 | 解决方法 |
|---|---|---|
| `permission denied while trying to connect to Docker daemon` | ubuntu 用户没有 Docker 权限 | `newgrp docker`，或重新登录 |
| `context deadline exceeded` 拉取镜像时 | 旧 docker-compose.yml 还在用 Docker Hub | 先 `git pull origin main` 再重试 |
| `unauthorized` / `denied` 拉取 ghcr.io | ghcr.io 包是私有的，未登录 | `docker login ghcr.io` 或把包设为 Public |
| `no such manifest` | ghcr.io 上还没有镜像 | 等 GitHub Actions 构建完成（绿色 ✅）再 pull |
| `invalid reference format` | `.env` 中 `GITHUB_USERNAME` 为空 | `nano .env` 确认 `GITHUB_USERNAME=explodewith-star` |
| `no configuration file provided` | 在错误目录执行了 docker compose | `cd ~/musespace/code/muse-space/muse-space` |
| `password authentication failed` | 密码末尾特殊字符被截断 | `docker exec musespace-api printenv ConnectionStrings__DefaultConnection` 核对密码 |
| 容器启动后立即退出 | 程序启动报错（如 JwtSecret 未配置） | `docker compose logs api` 查看详细错误 |
| 后端拒绝启动，日志报 `JWT secret` | `AUTH_JWT_SECRET` 未填或长度 < 32 | `nano .env` 补充 `AUTH_JWT_SECRET`，重启 |
| `port is already allocated` | 80 端口被占用 | `sudo lsof -i:80` 找到进程后停止 |
| 接口 500，无明显报错 | DI 注册缺失 | `docker compose logs api` 查找 Exception 行 |

---

## 六、架构说明

```
浏览器
  │
  ▼ :80
[Nginx 容器]  musespace-web
  ├── /        → 返回 Vue 前端静态文件
  └── /api/**  → 反向代理到后端（容器内网）
                    │
                    ▼ :8080（内部，不对外暴露）
              [.NET API 容器]  musespace-api
                    │
                    ▼ :6286
              [PostgreSQL]  musespace_db（独立 docker-compose，~/musespace/infra/）
```

三个容器通过 Docker 内部网络 `musespace-net` 互通，外部只有 80 端口可访问。

> ⚠️ `curl localhost:8080` 会失败（8080 未映射到宿主机）。测试后端要通过 nginx：`curl http://localhost/api/projects`。

---

## 七、数据库迁移说明

> 本项目本地开发和生产使用**同一个数据库**（`152.136.11.140:6286`），在本地执行 `dotnet ef database update` 即同时更新了生产库，服务器上无需额外操作。

**本地执行迁移（无需设置环境变量，自动读 appsettings.Development.json）：**

```powershell
cd muse-space
dotnet ef database update --project src/MuseSpace.Infrastructure --startup-project src/MuseSpace.Api
```

**新建迁移：**

```powershell
dotnet ef migrations add 迁移名称 --project src/MuseSpace.Infrastructure --startup-project src/MuseSpace.Api
```

**验证所有迁移均已应用：**

```powershell
dotnet ef migrations list --project src/MuseSpace.Infrastructure --startup-project src/MuseSpace.Api
# 结果里没有 (Pending) 即代表数据库是最新的
```

用 DBeaver 连接 `152.136.11.140:6286` 确认新表存在即可。

---

## 八、踩坑记录

| # | 问题现象 | 根本原因 | 解决方案 |
|---|---|---|---|
| 1 | `docker compose pull` 超时 | 腾讯云大陆服务器无法访问 Docker Hub | 改用 ghcr.io |
| 2 | GitHub Actions 报 `repository name must be lowercase` | `github.repository_owner` 含大写 | workflow 已用 `tr` 转小写，无需手动处理 |
| 3 | `docker compose pull` 还是走 Docker Hub | 服务器 docker-compose.yml 是旧版本 | 每次部署必须先 `git pull origin main` |
| 4 | `unauthorized` 拉取 ghcr.io | 包默认私有 | 创建 PAT 登录，或把包设为 Public |
| 5 | `.env` 里 `GITHUB_USERNAME` 未生效 | 旧 `.env` 还是 `DOCKERHUB_USERNAME` | nano 重新编辑 |
| 6 | `password authentication failed` | 密码末尾 `.` 紧接 `;` 被截断 | 去掉密码末尾的 `.` |
| 7 | `role "postgres" does not exist` | 超级用户是 `msadmin` 不是 `postgres` | 用 `psql -U msadmin` 进入 |
| 8 | `git pull` 卡住 | SSH 克隆但未配 SSH 密钥 | `git remote set-url origin https://...` |
| 9 | `no configuration file provided` | 在错误目录执行 docker compose | `cd ~/musespace/code/muse-space/muse-space` |
| 10 | `/api/llm-provider` 返回 500 | DI 注册缺失 | 在 `ServiceCollectionExtensions.cs` 补全注册 |
| 11 | 草稿生成 500，日志报 `/app/logs/generations` 权限拒绝 | 宿主机挂载目录由 root 创建，容器 app 用户无写权限 | 已彻底移除 JSON 文件写入，改为 Serilog 日志 |
| 12 | GitHub Actions 前端构建失败，`npm ci` exit code 1 | Dockerfile 用 `node:24-alpine` 与本地 npm 生成的 lockfile 不兼容 | Dockerfile 改为 `node:22-alpine`（LTS），重新生成 package-lock.json |
| 13 | `dotnet ef` 命令需要手动设 `$env:MUSESPACE_CONN` | `DesignTimeDbContextFactory` 只读环境变量，不读 appsettings | 已修复：Factory 自动回退读 appsettings.Development.json |
