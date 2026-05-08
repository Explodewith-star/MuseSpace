# MuseSpace 本地构建 + SSH 一键部署方案（备选）

> 这是一个**备选**部署方案，**与现有 [deployment.md](./deployment.md)（GitHub Actions + ghcr.io）并存**，两者择一即可。
>
> 适用场景：服务器在国内，访问 GitHub / ghcr.io / Docker Hub 不稳定，想省掉 CI 等待和镜像仓库登录环节。
>
> 核心思想：**完全砍掉中间环节**——本地直接构建镜像，通过 SSH 流式传给服务器，不经过任何镜像仓库。

---

## 一、它解决了什么

| 旧方案痛点 | 本方案 |
|---|---|
| `git push` 后等 GitHub Actions 5–10 分钟 | 本地构建即推完，约 1–2 分钟 |
| 服务器 `git pull` 经常卡 / 超时 | 不再需要 git pull |
| 服务器 `docker pull ghcr.io` 401 / 慢 | 不再需要 pull |
| 凭证管理（GitHub PAT、ghcr.io 登录） | 仅需 SSH 密钥 |
| 多平台账号（GitHub + Registry + 服务器） | 只用服务器一台 |

代价：**镜像通过 SSH 传输**，首次约 1–2 分钟（API + Web 总共 200–300 MB），之后 Docker 增量传层，通常 30 秒以内。

---

## 二、整体流程

```
[你的 Win 机器]                                 [服务器 152.136.11.140]
   ┌──────────────────┐                            ┌──────────────────┐
   │ 1. docker build  │                            │  docker compose  │
   │    api / web     │                            │     up -d        │
   └────────┬─────────┘                            └────────▲─────────┘
            │                                               │
            │ 2. docker save | ssh "docker load"            │
            └────────────────────────────────────────────► ─┘
                            一条命令完成
```

**没有 GitHub、没有 Gitee、没有镜像仓库。** 镜像是私有的、永远不出你和服务器之间这条 SSH 通道。

---

## 三、一次性准备工作

### 3.1 本地：生成并安装 SSH 密钥（让 Win 免密登服务器）

```powershell
# 如果还没有密钥
ssh-keygen -t ed25519 -C "musespace-deploy"
# 一路回车，密钥落在 C:\Users\<你>\.ssh\id_ed25519(.pub)

# 把公钥复制到服务器（首次需要输入服务器密码）
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh ubuntu@152.136.11.140 "cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys"
```

验证免密：

```powershell
ssh ubuntu@152.136.11.140 "echo ok"
# 不再要密码、直接输出 ok 即成功
```

### 3.2 服务器：准备 compose 目录（一次性）

> 服务器上**不再需要 git clone 整个仓库**，只需要保留 `docker-compose.yml` 和 `.env` 这两份运行时配置。

```bash
mkdir -p ~/musespace/code/muse-space/muse-space
cd ~/musespace/code/muse-space/muse-space
nano .env   # 内容参考 deployment.md 步骤 3，注意把 image 引用的 GITHUB_USERNAME 那条删掉
```

把改造后的 `docker-compose.yml`（见下文 §4.2）通过 `scp` 传上去：

```powershell
scp ./muse-space/muse-space/docker-compose.yml ubuntu@152.136.11.140:~/musespace/code/muse-space/muse-space/docker-compose.yml
```

### 3.3 服务器：开通 docker 权限 + 防火墙 80（与原方案相同）

参考 [deployment.md](./deployment.md) 步骤 1 和步骤 4，**这两步两种方案都要做**。

---

## 四、改造点

### 4.1 删除（或保留禁用）GitHub Actions

最简单：删除 `.github/workflows/docker-publish.yml` 即可，本方案不再使用 CI。

如果你想以后两种方案并行，也可以保留 workflow，但服务器上不再 `docker pull`。

### 4.2 `docker-compose.yml`：把 image 改成本地 tag

```yaml
services:
  api:
    # 本地构建后通过 ssh 灌进来；不再从 ghcr.io 拉
    image: musespace-api:latest
    container_name: musespace-api
    restart: unless-stopped
    expose:
      - "8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION_STRING}
      - Llm__ApiKey=${LLM_API_KEY}
      - Llm__BaseUrl=${LLM_BASE_URL}
      - Llm__ModelName=${LLM_MODEL_NAME}
      - DeepSeek__ApiKey=${DEEPSEEK_API_KEY}
      - Embedding__ApiKey=${EMBEDDING_API_KEY}
      - Auth__JwtSecret=${AUTH_JWT_SECRET}
      - Auth__UserTokenExpiryDays=${AUTH_USER_TOKEN_EXPIRY_DAYS:-7}
      - Auth__AdminTokenExpiryHours=${AUTH_ADMIN_TOKEN_EXPIRY_HOURS:-24}
      - Admin__PhoneNumber=${ADMIN_PHONE_NUMBER}
      - Admin__PasswordHash=${ADMIN_PASSWORD_HASH}
    volumes:
      - ./logs:/app/logs
      - musespace-data:/data
    networks:
      - musespace-net

  web:
    image: musespace-web:latest
    container_name: musespace-web
    restart: unless-stopped
    ports:
      - "80:80"
    depends_on:
      - api
    networks:
      - musespace-net

networks:
  musespace-net:
    driver: bridge

volumes:
  musespace-data:
```

> 与原 `docker-compose.yml` 比较，主要差异：
> 1. `image:` 不再是 `ghcr.io/${GITHUB_USERNAME}/...`，改成本地 tag。
> 2. **删掉了 `build:` 段**——服务器上不再做构建（也不再需要 git clone 整个仓库）。
> 3. `.env` 里 `GITHUB_USERNAME=` 这一行可以删除。

### 4.3 一键部署脚本 `scripts/deploy.ps1`

放到仓库根目录 `scripts/deploy.ps1`：

```powershell
<#
.SYNOPSIS
    本地构建 MuseSpace 镜像并通过 SSH 部署到生产服务器。

.PARAMETER Server
    SSH 目标，默认 ubuntu@152.136.11.140

.PARAMETER ComposeDir
    服务器上 docker-compose.yml 所在目录

.PARAMETER OnlyApi / OnlyWeb
    只构建并部署其中一端

.PARAMETER NoBuild
    跳过本地 docker build，仅传输已存在的镜像并重启

.EXAMPLE
    .\scripts\deploy.ps1
    .\scripts\deploy.ps1 -OnlyWeb
#>

param(
    [string] $Server = "ubuntu@152.136.11.140",
    [string] $ComposeDir = "~/musespace/code/muse-space/muse-space",
    [switch] $OnlyApi,
    [switch] $OnlyWeb,
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$Repo = Split-Path -Parent $PSScriptRoot

function Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }

# 1. 本地构建 ─────────────────────────────────────────────────────────────
if (-not $NoBuild) {
    if (-not $OnlyWeb) {
        Step "构建 API 镜像（musespace-api:latest）"
        docker build `
            -t musespace-api:latest `
            -f "$Repo/muse-space/muse-space/Dockerfile" `
            "$Repo/muse-space"
        if ($LASTEXITCODE) { throw "API 构建失败" }
    }
    if (-not $OnlyApi) {
        Step "构建 Web 镜像（musespace-web:latest）"
        docker build `
            -t musespace-web:latest `
            -f "$Repo/muse-space-web/Dockerfile" `
            "$Repo/muse-space-web"
        if ($LASTEXITCODE) { throw "Web 构建失败" }
    }
}

# 2. 通过 SSH 传输（save → 流式 ssh → load） ─────────────────────────────
function Transfer-Image($name) {
    Step "推送 $name 到服务器（流式传输 + gzip 压缩）"
    # PowerShell 下 docker save 二进制流要走管道而非临时文件以保留性能
    $tmp = New-TemporaryFile
    try {
        docker save $name | Out-File -FilePath $tmp -Encoding Byte -Force 2>$null
        # PowerShell 5/7 对二进制管道支持有差异，统一用临时文件 + scp 最稳：
        $tar = "$($tmp.FullName).tar"
        Move-Item $tmp.FullName $tar -Force
        docker save -o $tar $name
        scp $tar "${Server}:/tmp/musespace-deploy.tar"
        ssh $Server "docker load -i /tmp/musespace-deploy.tar && rm /tmp/musespace-deploy.tar"
    } finally {
        if (Test-Path $tar) { Remove-Item $tar -Force }
    }
}

if (-not $OnlyWeb) { Transfer-Image "musespace-api:latest" }
if (-not $OnlyApi) { Transfer-Image "musespace-web:latest" }

# 3. 服务器侧 up -d ──────────────────────────────────────────────────────
Step "在服务器执行 docker compose up -d"
ssh $Server "cd $ComposeDir && docker compose up -d"

# 4. 健康检查 ────────────────────────────────────────────────────────────
Step "健康检查"
Start-Sleep -Seconds 4
$ip = ($Server -split '@')[1]
try {
    $resp = Invoke-WebRequest -Uri "http://$ip/api/projects" -UseBasicParsing -TimeoutSec 10
    Write-Host "  API 返回 HTTP $($resp.StatusCode)" -ForegroundColor Green
} catch {
    Write-Warning "  API 健康检查失败：$($_.Exception.Message)"
}
try {
    $resp = Invoke-WebRequest -Uri "http://$ip/" -UseBasicParsing -TimeoutSec 10
    Write-Host "  Web 返回 HTTP $($resp.StatusCode)" -ForegroundColor Green
} catch {
    Write-Warning "  Web 健康检查失败：$($_.Exception.Message)"
}

Write-Host "`n部署完成，访问 http://$ip" -ForegroundColor Green
```

> 简化版的"流式传输"（不落临时文件、更快）：
>
> ```powershell
> # 在 PowerShell 7+ 上可用，5.1 上对二进制 stdin 支持不佳
> docker save musespace-api:latest | ssh $Server "docker load"
> ```
>
> 上面的脚本采用了"先 save 到临时 .tar → scp → load → 删除"的稳妥写法，跨 PowerShell 版本兼容性最好。

---

## 五、日常使用

### 第一次部署

```powershell
# 1) 本地（一次性）
ssh-keygen -t ed25519 ...
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh ubuntu@152.136.11.140 "cat >> ~/.ssh/authorized_keys"

# 2) 服务器（一次性）
ssh ubuntu@152.136.11.140
mkdir -p ~/musespace/code/muse-space/muse-space
nano ~/musespace/code/muse-space/muse-space/.env  # 见 deployment.md 步骤 3

# 3) 本地传 compose
scp .\muse-space\muse-space\docker-compose.yml ubuntu@152.136.11.140:~/musespace/code/muse-space/muse-space/docker-compose.yml

# 4) 一键部署
.\scripts\deploy.ps1
```

### 之后每次更新

```powershell
.\scripts\deploy.ps1
```

只改前端：

```powershell
.\scripts\deploy.ps1 -OnlyWeb
```

只重启（不重新构建）：

```powershell
.\scripts\deploy.ps1 -NoBuild
```

---

## 六、常见问题

| 现象 | 原因 | 解决 |
|---|---|---|
| 第一次 `ssh ubuntu@...` 还要密码 | 公钥未传上去或权限错 | `chmod 600 ~/.ssh/authorized_keys` |
| `docker save` 输出乱码进 ssh 失败 | PowerShell 5.1 对二进制管道支持差 | 用脚本里的 "落 tar → scp → load" 路径 |
| `docker compose up -d` 找不到 `image` | 服务器没收到镜像 | 看 deploy.ps1 输出的 transfer 步骤是否成功 |
| 服务器磁盘越来越大 | 旧镜像没清理 | `ssh $server "docker image prune -f"` |
| 镜像传输慢 | 第一次必然慢 | 之后 Docker 自动增量；或在脚本中加 `gzip -1` 压缩选项 |
| 想快速回滚到上一版 | 现在覆盖式 tag | 改成 `musespace-api:$(git rev-parse --short HEAD)` 形式（脚本可加） |

---

## 七、与现行方案对比

| 维度 | 现行（deployment.md） | 本方案 |
|---|---|---|
| 学习门槛 | 高（CI / Registry / 凭证） | 低（仅 SSH） |
| 一次部署耗时 | 5–15 分钟 | 1–3 分钟 |
| 网络依赖 | 本机→GitHub→ghcr.io→服务器 | 本机→服务器 |
| 凭证 | SSH + GitHub PAT + Registry | 仅 SSH |
| 多人协作 | 友好（推 main 即部署） | 不友好（部署必须由你本机发起） |
| 历史回滚 | ghcr.io 历史镜像可拉 | 需要本地保留旧镜像或加 git tag |
| 隐私性 | 镜像存在公网 Registry | 镜像不出 SSH 通道 |

**结论**：单人开发 + 国内服务器 + 网络不稳，本方案最适合。一旦多人协作或需要灰度回滚，再回到 CI/Registry 方案。

---

## 八、迁移检查清单（如果未来真要切换到本方案）

- [ ] 本地生成 SSH 密钥并安装到服务器
- [ ] 验证免密 `ssh ubuntu@152.136.11.140 "echo ok"`
- [ ] 服务器创建 `~/musespace/code/muse-space/muse-space/`
- [ ] 改造 `docker-compose.yml`（去掉 ghcr 引用、删 build 段），scp 上传
- [ ] 服务器 `.env` 内容继续沿用（只删 `GITHUB_USERNAME`）
- [ ] 创建 `scripts/deploy.ps1`（参考 §4.3）
- [ ] 删除 `.github/workflows/docker-publish.yml`（或保留禁用）
- [ ] 第一次 `.\scripts\deploy.ps1` 完整跑一遍
- [ ] 更新 README，标注：默认部署方式已切换为 SSH 直推

---

## 九、不在本方案范围内（即使切换也保持原样）

- PostgreSQL 容器：仍由独立 `~/musespace/infra/docker-compose.yml` 管理。
- 数据库迁移：仍在本地 `dotnet ef database update` 直连生产库。
- 防火墙、证书、Nginx 配置：与原方案相同。
- 日志、`musespace-data` 数据卷：与原方案相同。
