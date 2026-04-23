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
> 腾讯云大陆服务器无法访问 Docker Hub（`registry-1.docker.io` 超时），即使配置腾讯云镜像加速也只代理官方镜像，用户命名空间镜像（`用户名/xxx`）仍然超时。ghcr.io（GitHub Container Registry）在大陆服务器上可以正常访问。

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

> 默认 ubuntu 用户无法直接运行 docker 命令，会报 `permission denied`，需要加入 docker 用户组。

```bash
sudo usermod -aG docker $USER
newgrp docker
```

验证权限已生效（能看到版本号即表示成功）：

```bash
docker version
```

> ⚠️ 如果后续退出重新登录后提示 permission denied，重新执行 `newgrp docker` 即可。

---

### 步骤 2：拉取代码

> ⚠️ 必须用 HTTPS 克隆，不要用 SSH（服务器没有配置 GitHub SSH 密钥，SSH 会卡住报 `Permission denied (publickey)`）。

```bash
# 克隆仓库（只做一次）
git clone https://github.com/Explodewith-star/MuseSpace.git ~/musespace/code

# 进入 docker-compose.yml 所在目录
# 注意：仓库根是 muse-space/，docker-compose.yml 在其内层子目录
cd ~/musespace/code/muse-space/muse-space
```

如果之前已经克隆（用了 SSH），把远程地址改成 HTTPS：

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

将文件内容改成真实值：

```
# GitHub 用户名（全小写），ghcr.io 镜像地址用此值
GITHUB_USERNAME=explodewith-star

# ⚠️ 密码末尾不要有句号"."等特殊字符紧接分号";"
# 因为 docker compose 解析 .env 时会把分号前的内容截断，导致密码丢失最后一个字符
DB_CONNECTION_STRING=Host=152.136.11.140;Port=6286;Database=musespace_dev;Username=msadmin;Password=YOUR_DB_PASSWORD;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;
LLM_BASE_URL=https://openrouter.ai/api/v1
LLM_API_KEY=YOUR_LLM_API_KEY
LLM_MODEL_NAME=openai/gpt-oss-120b:free
DEEPSEEK_API_KEY=YOUR_DEEPSEEK_API_KEY
EMBEDDING_API_KEY=YOUR_EMBEDDING_API_KEY
```

**nano 操作说明：**
- 方向键移动光标
- 修改完后按 `Ctrl+O` → 回车（保存）
- 按 `Ctrl+X` 退出

验证容器实际收到的连接字符串（排查密码截断问题）：

```bash
docker exec musespace-api printenv ConnectionStrings__DefaultConnection
```

---

### 步骤 4：开放腾讯云防火墙端口

登录腾讯云控制台：

1. 进入 **云服务器** → 找到你的实例 → 点击实例名
2. 点击左侧 **安全组** → 点击已绑定的安全组
3. 点击 **入站规则** → **添加规则**，填写：
   - 协议：`TCP`，端口：`80`，来源：`0.0.0.0/0`
4. 保存

---

### 步骤 5：登录 GitHub Container Registry

ghcr.io 的包默认是私有的，拉取前需要用 GitHub Personal Access Token 登录。

**首先在 GitHub 网页上创建 PAT（只做一次）：**
1. 打开 https://github.com/settings/tokens
2. 点击 **Generate new token (classic)**
3. Note 填 `musespace-deploy`，勾选 `read:packages`，点 **Generate token**
4. 复制生成的 token（页面离开后无法再看到）

**然后在服务器上登录：**

```bash
docker login ghcr.io -u explodewith-star -p 你的PAT_TOKEN
```

> 登录成功会显示 `Login Succeeded`，之后 docker pull 就不会被拒绝。

> **更简单的替代方案（免登录）**：去 GitHub → 你的 Profile → Packages → 找到 `musespace-api` 和 `musespace-web` → 进入每个包的 Settings → 改为 **Public**。之后无需登录即可 pull。

---

### 步骤 6：同步配置并启动

> ⚠️ 执行前请确认 GitHub Actions 已显示绿色 ✅，否则 pull 会找不到镜像。
>
> ⚠️ 所有 docker compose 命令必须在 `docker-compose.yml` 所在目录执行，否则报 `no configuration file provided`。

```bash
cd ~/musespace/code/muse-space/muse-space

# 同步最新的 docker-compose.yml 等配置文件（非常重要，每次都要做）
git pull origin main

# 从 ghcr.io 拉取最新镜像（后端 + 前端各约 200~400MB）
docker compose pull

# 后台启动所有服务（api + web）
docker compose up -d
```

---

### 步骤 7：验证部署是否成功

```bash
# 查看所有容器是否在运行（STATUS 应全为 Up）
docker compose ps

# 测试完整链路（通过 nginx 代理访问后端，返回 JSON 即表示全链路正常）
curl http://localhost/api/projects

# 测试前端（返回 HTML 即表示前端正常）
curl http://localhost/
```

然后在你自己电脑的浏览器打开 `http://152.136.11.140`，能看到页面即部署完成。

---

## 三、常见错误处理

| 错误信息 | 原因 | 解决方法 |
|---|---|---|
| `permission denied while trying to connect to the Docker daemon` | ubuntu 用户没有 Docker 权限 | 执行 `newgrp docker`，或重新登录再试 |
| `context deadline exceeded` 拉取镜像时 | 服务器无法访问 Docker Hub（中国大陆网络限制） | 改用 ghcr.io（已配置），或先 `git pull origin main` 确认 docker-compose.yml 已更新 |
| `unauthorized` 或 `denied` 拉取 ghcr.io 镜像 | ghcr.io 包是私有的，未登录 | 执行 `docker login ghcr.io` 或把包设为 Public |
| `unable to get image ... no such manifest` | ghcr.io 上还没有镜像 | 等待 GitHub Actions 构建完成（绿色 ✅）再 pull |
| `invalid tag ... repository name must be lowercase` | GitHub Actions 中 `github.repository_owner` 含大写字母 | workflow 已用 `tr` 转小写修复，无需手动处理 |
| `invalid reference format` | `.env` 中 `GITHUB_USERNAME` 未填或为空 | `nano .env` 确认 `GITHUB_USERNAME=explodewith-star` |
| `no configuration file provided` | 在错误目录执行了 docker compose | `cd ~/musespace/code/muse-space/muse-space` 再重试 |
| `password authentication failed for user "msadmin"` | 密码被截断（末尾字符紧接分号被丢弃） | 用 `docker exec musespace-api printenv ConnectionStrings__DefaultConnection` 确认密码，与数据库实际密码对比后修改其中一个 |
| `role "postgres" does not exist` | 数据库容器没有 postgres 超级用户，创建时指定的是 msadmin | 用 `docker exec -it musespace_db psql -U msadmin -d musespace_dev` 进入 |
| `Permission denied (publickey)` git pull 卡住 | 服务器没有配置 GitHub SSH 密钥 | `git remote set-url origin https://github.com/Explodewith-star/MuseSpace.git` |
| `Connection refused` 或数据库报错 | `.env` 里连接字符串有误 | `nano .env` 检查 `DB_CONNECTION_STRING` |
| `Access to the path '/app/logs/generations' is denied` | 容器内 app 用户没有写宿主机挂载目录的权限 | 已在代码中修复（日志写失败不再抛异常），无需手动处理 |
| `port is already allocated` | 80 端口被占用 | `sudo lsof -i:80` 查找占用进程并停止 |
| 容器启动后又退出 | 程序启动报错 | `docker compose logs api` 查看详细日志 |
| 接口返回 500，日志无明显报错 | DI 容器注册缺失（如 `LlmProviderSelector` 未注册） | 检查 `ServiceCollectionExtensions.cs` 是否完整注册所有服务 |

---

## 四、日常维护

### 更新部署（代码有更新时）

```bash
# 第一步：本地推送代码（自动触发 GitHub Actions 构建，等待绿色 ✅）
git push origin main

# 第二步：服务器执行（等 Actions 完成后）
cd ~/musespace/code/muse-space/muse-space
git pull origin main     # 同步 docker-compose.yml 等配置文件的变更（不能省略！）
docker compose pull      # 拉取最新镜像
docker compose up -d     # 滚动更新（不会中断服务）
```

### 常用运维命令

```bash
cd ~/musespace/code/muse-space/muse-space

# 查看所有容器状态
docker compose ps

# 查看实时日志（Ctrl+C 退出）
docker compose logs -f

# 只看后端日志
docker compose logs api

# 查看后端最近 50 行日志
docker compose logs api --tail=50

# 重启所有服务
docker compose restart

# 停止所有服务
docker compose down

# 查看容器实际收到的环境变量（排查配置问题）
docker exec musespace-api printenv ConnectionStrings__DefaultConnection
docker exec musespace-api printenv Llm__ApiKey

# 进入数据库（注意：没有 postgres 用户，用 msadmin）
docker exec -it musespace_db psql -U msadmin -d musespace_dev

# 修改数据库密码（与 .env 不一致时使用）
docker exec -it musespace_db psql -U msadmin -d musespace_dev -c "ALTER USER msadmin WITH PASSWORD '新密码';"
```

---

## 五、架构说明

```
浏览器
  │
  ▼ :80
[Nginx 容器]  musespace-web
  ├── /        → 返回 Vue 前端静态文件
  └── /api/**  → 反向代理到后端（容器内网，不走公网）
                    │
                    ▼ :8080（内部，不对外暴露）
              [.NET API 容器]  musespace-api
                    │
                    ▼ :6286
              [PostgreSQL 容器]  musespace_db（独立 docker-compose，~/musespace/infra/）
```

三个容器通过 Docker 内部网络 `musespace-net` 互通，外部只有 80 端口可访问。

> ⚠️ API 容器的 8080 端口**没有**映射到宿主机，`curl localhost:8080` 会失败（这是正常的）。
> 测试后端要通过 nginx：`curl http://localhost/api/projects`。

---

## 六、踩坑记录

本节记录首次部署时遇到的所有问题，供后续参考。

| # | 问题现象 | 根本原因 | 解决方案 |
|---|---|---|---|
| 1 | `docker compose pull` 超时，`context deadline exceeded` | 腾讯云大陆服务器无法访问 Docker Hub | 改用 ghcr.io；腾讯云镜像加速只代理官方镜像，用户命名空间镜像无效 |
| 2 | GitHub Actions 报 `repository name must be lowercase` | `github.repository_owner` 返回 `Explodewith-star`（含大写） | 用 `tr '[:upper:]' '[:lower:]'` 转小写后再拼 tag |
| 3 | `docker compose pull` 还是走 Docker Hub | 服务器上的 `docker-compose.yml` 是旧版本，没有 `git pull` 同步 | 每次部署必须先 `git pull origin main` |
| 4 | `unauthorized` 拉取 ghcr.io 镜像 | ghcr.io 包默认私有 | 创建 PAT 登录，或把包设为 Public |
| 5 | `.env` 里 `GITHUB_USERNAME` 未生效 | 旧 `.env` 文件还是 `DOCKERHUB_USERNAME` | `sed -i` 替换变量名，或重新 nano 编辑 |
| 6 | `password authentication failed for user "msadmin"` | 密码 `leoisAdmin441621.` 末尾 `.` 紧接 `;` 被截断 | 去掉密码末尾的 `.`，或用 `ALTER USER` 把数据库密码改成和截断后一致 |
| 7 | `role "postgres" does not exist` | 数据库创建时超级用户是 `msadmin`，不是 `postgres` | 改用 `docker exec -it musespace_db psql -U msadmin` |
| 8 | `git pull` 卡住，`Permission denied (publickey)` | 仓库用 SSH 克隆，服务器没有 GitHub SSH 密钥 | `git remote set-url origin https://...` 改为 HTTPS |
| 9 | `no configuration file provided` | 在 `~/musespace/code` 执行了 docker compose，而不是子目录 | 必须 `cd ~/musespace/code/muse-space/muse-space` |
| 10 | `/api/llm-provider` 返回 500 | `LlmProviderSelector` 未在 DI 容器注册；`ILlmClient` 直接绑定了 `OpenRouterLlmClient` 而非 `RoutingLlmClient`；`DeepSeekLlmClient` 和 `DeepSeekOptions` 也未注册 | 在 `ServiceCollectionExtensions.cs` 补全全部注册 |
| 11 | 草稿生成返回 500，日志报 `/app/logs/generations` 权限拒绝 | `docker-compose.yml` 把 `./logs` 挂载到容器，宿主机目录由 root 创建，容器 app 用户无写权限 | 在 `GenerationLogService.LogAsync` 外层加 try/catch，日志写失败只警告，不抛异常影响业务 |


---

## 整体流程概览

```
① 本地推代码 → ② GitHub Actions 自动构建镜像并推送到 ghcr.io
                                        ↓
③ 服务器 git pull（同步配置）→ ④ docker compose pull（拉镜像）→ ⑤ docker compose up -d → ⑥ 浏览器访问
```

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

> 默认 ubuntu 用户无法直接运行 docker 命令，会报 `permission denied`，需要加入 docker 用户组。

```bash
sudo usermod -aG docker $USER
newgrp docker
```

验证权限已生效（能看到版本号即表示成功）：

```bash
docker version
```

> ⚠️ 如果后续退出重新登录后提示 permission denied，重新执行 `newgrp docker` 即可。

---

### 步骤 2：拉取代码

```bash
# 克隆仓库（只做一次）
git clone https://github.com/Explodewith-star/MuseSpace.git ~/musespace/code

# 进入 docker-compose.yml 所在目录
# 注意：仓库根是 muse-space/，docker-compose.yml 在其内层子目录
cd ~/musespace/code/muse-space/muse-space
```

---

### 步骤 3：创建生产环境配置文件

```bash
# 复制模板生成配置文件
cp .env.example .env

# 编辑配置
nano .env
```

将文件内容改成你的真实值（全部替换）：

```
# GitHub 用户名（全小写），ghcr.io 镜像地址用此值
GITHUB_USERNAME=explodewith-star

DB_CONNECTION_STRING=Host=152.136.11.140;Port=6286;Database=musespace_dev;Username=msadmin;Password=YOUR_DB_PASSWORD;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;
LLM_BASE_URL=https://openrouter.ai/api/v1
LLM_API_KEY=YOUR_LLM_API_KEY
LLM_MODEL_NAME=openai/gpt-oss-120b:free
DEEPSEEK_API_KEY=YOUR_DEEPSEEK_API_KEY
EMBEDDING_API_KEY=YOUR_EMBEDDING_API_KEY
```

> ⚠️ 请将 `YOUR_*` 占位符替换为真实密钥，不要把真实密钥提交到 Git 仓库。

**nano 操作说明：**
- 方向键移动光标
- 修改完后按 `Ctrl+O` → 回车（保存）
- 按 `Ctrl+X` 退出

---

### 步骤 4：开放腾讯云防火墙端口

登录腾讯云控制台：

1. 进入 **云服务器** → 找到你的实例 → 点击实例名
2. 点击左侧 **安全组** → 点击已绑定的安全组
3. 点击 **入站规则** → **添加规则**，填写：
   - 协议：`TCP`，端口：`80`，来源：`0.0.0.0/0`
4. 保存

---

### 步骤 5：登录 GitHub Container Registry

ghcr.io 的包默认是私有的，拉取前需要用 GitHub Personal Access Token 登录。

**首先在 GitHub 网页上创建 PAT（只做一次）：**
1. 打开 https://github.com/settings/tokens
2. 点击 **Generate new token (classic)**
3. Note 填 `musespace-deploy`，勾选 `read:packages`，点 **Generate token**
4. 复制生成的 token（页面离开后无法再看到）

**然后在服务器上登录：**

```bash
# 用你的 GitHub 用户名和刚才生成的 token 登录
docker login ghcr.io -u explodewith-star -p 你的PAT_TOKEN
```

> 登录成功会显示 `Login Succeeded`，之后 docker pull 就不会被拒绝。

> **更简单的替代方案（免登录）**：去 GitHub → 你的 Profile → Packages → 找到 `musespace-api` 和 `musespace-web` → 进入每个包的 Settings → 改为 **Public**。之后无需登录即可 pull。

---

### 步骤 6：同步配置并启动

> ⚠️ 执行前请确认 GitHub Actions 已显示绿色 ✅，否则 pull 会找不到镜像。

```bash
cd ~/musespace/code/muse-space/muse-space

# 同步最新的 docker-compose.yml 等配置文件（非常重要，每次都要做）
git pull origin main

# 从 ghcr.io 拉取最新镜像（后端 + 前端各约 200~400MB）
docker compose pull

# 后台启动所有服务（api + web）
docker compose up -d
```

---

### 步骤 6：验证部署是否成功

```bash
# 查看所有容器是否在运行（STATUS 应全为 Up）
docker compose ps

# 测试后端（返回 JSON 即表示后端正常）
curl http://localhost:8080/api/projects

# 测试前端（返回 HTML 即表示前端正常）
curl http://localhost:80
```

然后在你自己电脑的浏览器打开 `http://152.136.11.140`，能看到页面即部署完成。

---

## 三、常见错误处理

| 错误信息 | 原因 | 解决方法 |
|---|---|---|
| `permission denied while trying to connect to the Docker daemon` | ubuntu 用户没有 Docker 权限 | 执行 `newgrp docker`，或重新登录再试 |
| `context deadline exceeded` 拉取镜像时 | 网络不通（通常是旧 docker-compose.yml 还在用 Docker Hub） | 先 `git pull origin main` 再重试 |
| `unauthorized` 或 `denied` 拉取 ghcr.io 镜像 | ghcr.io 包是私有的，未登录 | 执行 `docker login ghcr.io` 或把包设为 Public |
| `unable to get image ... no such manifest` | ghcr.io 上还没有镜像 | 等待 GitHub Actions 构建完成（绿色 ✅）再 pull |
| `invalid reference format` | `.env` 中 `GITHUB_USERNAME` 未填或为空 | `nano .env` 确认 `GITHUB_USERNAME=explodewith-star` |
| `Connection refused` 或数据库报错 | `.env` 里连接字符串有误 | `nano .env` 检查 `DB_CONNECTION_STRING` |
| `port is already allocated` | 80 端口被占用 | `sudo lsof -i:80` 查找占用进程并停止 |
| 容器启动后又退出 | 程序启动报错 | `docker compose logs api` 查看详细日志 |

---

## 四、日常维护

### 更新部署（代码有更新时）

```bash
# 第一步：本地推送代码（自动触发 GitHub Actions 构建，等待绿色 ✅）
git push origin main

# 第二步：服务器执行（等 Actions 完成后）
cd ~/musespace/code/muse-space/muse-space
git pull origin main     # 同步 docker-compose.yml 等配置文件的变更
docker compose pull      # 拉取最新镜像
docker compose up -d     # 滚动更新（不会中断服务）
```

### 常用运维命令

```bash
cd ~/musespace/code/muse-space/muse-space

# 查看所有容器状态
docker compose ps

# 查看实时日志（Ctrl+C 退出）
docker compose logs -f

# 只看后端日志
docker compose logs -f api

# 开启所有服务
docker compose up -d

# 重启所有服务
docker compose restart

# 停止所有服务
docker compose down

# 强制更改为一样的密码
docker exec -it musespace_db psql -U msadmin -d musespace_dev -c "ALTER USER msadmin WITH PASSWORD 'leoisAdmin441621';"

# 看 API 容器实际收到的连接字符串
docker exec musespace-api printenv ConnectionStrings__DefaultConnection
```


---

## 五、架构说明

```
浏览器
  │
  ▼ :80
[Nginx 容器]  musespace-web
  ├── /        → 返回 Vue 前端静态文件
  └── /api/**  → 反向代理到后端
                    │
                    ▼ :8080（内部，不对外暴露）
              [.NET API 容器]  musespace-api
                    │
                    ▼ :6286
              [PostgreSQL 容器]  musespace_db
```

三个容器通过 Docker 内部网络 `musespace-net` 互通，外部只有 80 端口可访问。
