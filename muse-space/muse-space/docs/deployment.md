# MuseSpace 部署指南

> 目标服务器：`152.136.11.140`（腾讯云 Docker CE）
> 部署完成后访问地址：`http://152.136.11.140`

---

## 准备工作（在你自己电脑上做一次）

### 1. 确认 `.env` 文件加入了 .gitignore

`muse-space/.gitignore` 里已经有 `.env`，无需额外操作。

### 2. 把代码推送到 GitHub

```powershell
cd C:\Users\E1557417\Documents\WorkItem\StoryWorkFlow\MuseSpaceNew
git add .
git commit -m "chore: add docker deployment files"
# ⚠️ 推送前确认 .env 和 appsettings.Development.json 不在暂存区
git push origin main
```

---

## 第一阶段：部署后端

### 步骤 1：SSH 登录服务器

在你的电脑上打开 PowerShell：

```powershell
ssh root@152.136.11.140
```

输入腾讯云的服务器密码，登录成功后你会看到 `[root@VM ~]#` 这样的提示符。

---

### 步骤 2：拉取代码

```bash
# 把项目克隆到服务器
git clone https://github.com/Explodewith-star/MuseSpace.git ~/musespace/code

# 仓库根目录是 ~/musespace/code/muse-space
# docker-compose.yml 在仓库内的子目录，需要再进一层
cd ~/musespace/code/muse-space/muse-space
```

---

### 步骤 3：创建生产环境配置文件

```bash
# 复制模板
cp .env.example .env

# 编辑配置（使用 nano 编辑器，比 vim 友好）
nano .env
```

把文件内容改成你的真实值：

```
DB_CONNECTION_STRING=Host=152.136.11.140;Port=6286;Database=musespace_dev;Username=msadmin;Password=leoisAdmin441621.;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;
LLM_BASE_URL=https://openrouter.ai/api/v1
LLM_API_KEY=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
LLM_MODEL_NAME=z-ai/glm-4.5-air:free
DEEPSEEK_API_KEY=sk-93b0ffca2024490098eb9da35b6aa279
EMBEDDING_API_KEY=sk-mepbllkxmhltwshbjfgtfrudymokusygjrgnnuvavtrgdqjk
```

**nano 操作说明：**
- 方向键移动光标
- 修改完后按 `Ctrl+O` → 回车（保存）
- 按 `Ctrl+X` 退出

---

### 步骤 4：配置 GitHub Actions 自动构建（一次性准备）

> **为什么这样做？**
> 服务器在中国大陆无法访问 `mcr.microsoft.com` 拉取 .NET 基础镜像（TLS timeout）。
> 解决方案：让 GitHub 的服务器帮你构建镜像并推送到 Docker Hub，服务器只需要 `docker pull`。

**第一步：注册 Docker Hub 账号**

访问 [https://hub.docker.com](https://hub.docker.com) 免费注册，记住你的用户名。

**第二步：生成 Docker Hub Access Token**

Docker Hub → 右上角头像 → **Account Settings** → **Security** → **New Access Token**
- Description 随便写，如 `github-actions`
- Permissions 选 `Read & Write`
- 点击 Generate，**复制 token（只显示一次）**

**第三步：在 GitHub 仓库添加 Secrets**

进入你的 GitHub 仓库 → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**，依次添加：

| Secret 名称 | 值 |
|---|---|
| `DOCKERHUB_USERNAME` | 你的 Docker Hub 用户名 |
| `DOCKERHUB_TOKEN` | 上一步生成的 Token |

**第四步：推送代码触发构建**

```powershell
# 在你自己电脑上执行
cd C:\Users\E1557417\Documents\WorkItem\StoryWorkFlow\MuseSpaceNew
git add .
git commit -m "ci: add GitHub Actions docker build workflow"
git push origin main
```

推送后，进入 GitHub 仓库 → **Actions** 页面，可以看到构建进度，约 5~10 分钟完成。
构建完成后，Docker Hub 上会出现 `你的用户名/musespace-api:latest` 和 `你的用户名/musespace-web:latest`。

---

### 步骤 5：在服务器上拉取镜像并启动

**通过腾讯云控制台连接服务器，执行：**

```bash
cd ~/musespace/code/muse-space/muse-space

# 编辑 .env，补充 DOCKERHUB_USERNAME
nano .env
# 在文件顶部加一行：DOCKERHUB_USERNAME=你的DockerHub用户名

# 从 Docker Hub 拉取镜像（服务器可以访问 Docker Hub）
docker compose pull

# 启动所有服务
docker compose up -d
```

---

## ✅ 验证后端是否部署成功

**在服务器上执行：**

```bash
# 1. 查看容器是否在运行（Status 应该是 Up）
docker compose ps

# 2. 查看启动日志（看有没有报错）
docker compose logs api

# 3. 直接测试 API 是否响应（返回 JSON 说明成功）
curl http://localhost:8080/api/projects
```

**期望看到的结果：**

```
# docker compose ps 输出示例
NAME             STATUS
musespace-api    Up X seconds

# curl 输出示例（即使返回 404 或 200 都说明后端正常运行）
{"data":...,"success":true}
```

**如果有报错，查看详细日志：**

```bash
docker compose logs --tail=50 api
```

常见问题：
- `Connection refused` → 检查 `.env` 里的数据库连接字符串
- `Image not found` → 重新执行 `docker compose build api`

---

## 第二阶段：部署前端

> 确认后端验证通过后再继续。

### 步骤 6：启动前端

> 前端镜像已在步骤 5 的 `docker compose pull` 中一并拉取，无需额外操作，服务已随 `docker compose up -d` 启动。

---

### 步骤 6：开放腾讯云防火墙端口

登录腾讯云控制台：

1. 进入 **云服务器** → 找到你的实例 → 点击实例名
2. 点击左侧 **安全组** → 点击已绑定的安全组
3. 点击 **入站规则** → **添加规则**
4. 填写：
   - 协议：`TCP`
   - 端口：`80`
   - 来源：`0.0.0.0/0`（所有人可访问）
5. 保存

---

## ✅ 验证前端是否部署成功

```bash
# 在服务器上测试前端是否响应
curl http://localhost:80
```

然后在你自己电脑的浏览器打开：

```
http://152.136.11.140
```

能看到 MuseSpace 页面即表示部署完成。

---

## 日常维护命令

```bash
cd ~/musespace/code/muse-space/muse-space

# 查看所有容器状态
docker compose ps

# 查看实时日志
docker compose logs -f

# 重启所有服务
docker compose restart

# 停止所有服务
docker compose down
```

### 更新部署（代码有更新时）

1. 本地推送代码到 `main` 分支
2. GitHub Actions 自动重新构建并推送镜像到 Docker Hub（约 5~10 分钟）
3. 在腾讯云控制台连接服务器，执行：

```bash
cd ~/musespace/code/muse-space/muse-space
docker compose pull   # 拉取最新镜像
docker compose up -d  # 滚动更新容器
```

---

## 架构说明

```
浏览器
  │
  ▼
[Nginx 容器] :80
  ├── /        → 返回 Vue 前端静态文件
  └── /api/    → 反代到 [.NET API 容器] :8080
                       │
                       ▼
               [PostgreSQL] 152.136.11.140:6286
```

前端和后端在同一个 Docker 内部网络里通信，后端不直接对外暴露端口，所有请求都经过 Nginx。
