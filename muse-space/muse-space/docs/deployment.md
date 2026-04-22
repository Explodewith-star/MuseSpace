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
git clone https://github.com/Explodewith-star/MuseSpace.git /opt/musespace

# 进入后端目录
cd /opt/musespace/muse-space
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
DB_CONNECTION_STRING=Host=152.136.11.140;Port=6286;Database=musespace_dev;Username=msadmin;Password=你的真实密码;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;
LLM_BASE_URL=https://openrouter.ai/api/v1
LLM_API_KEY=sk-or-v1-你的真实Key
LLM_MODEL_NAME=z-ai/glm-4.5-air:free
```

**nano 操作说明：**
- 方向键移动光标
- 修改完后按 `Ctrl+O` → 回车（保存）
- 按 `Ctrl+X` 退出

---

### 步骤 4：构建并启动后端

```bash
# 只构建并启动后端服务（--no-deps 不启动 web）
docker compose build api
docker compose up -d api
```

> `build` 第一次会拉取 .NET 镜像，需要等几分钟，这是正常的。

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

### 步骤 5：启动前端

```bash
# 仍然在 /opt/musespace/muse-space 目录下
# 构建并启动前端（会自动识别 ../muse-space-web）
docker compose build web
docker compose up -d web
```

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
cd /opt/musespace/muse-space

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

```bash
cd /opt/musespace/muse-space
git pull origin main
docker compose build
docker compose up -d
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
