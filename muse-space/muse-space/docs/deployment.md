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
