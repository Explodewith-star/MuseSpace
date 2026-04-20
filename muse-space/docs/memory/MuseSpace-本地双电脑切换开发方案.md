# MuseSpace 本地双电脑切换开发方案

> 适用场景：个人开发、两台电脑轮换开发、本地 PostgreSQL + pgvector、无远程数据库依赖
> 技术栈：Vue + TypeScript + C#/.NET + PostgreSQL 16 + pgvector + Docker

---

## 一、核心原则

1. **一次只在一台电脑写入数据**，不做双向数据合并
2. **代码用 Git 同步**，数据库结构变更用 EF Core Migration 管理
3. **数据库 + 文件 + 配置** 三类资源打包成完整快照再迁移
4. **不存绝对路径**，文件统一用逻辑 Key 存入数据库
5. 每次切换电脑前必须完成：提交代码 → 导出快照 → 带走快照

---

## 二、本地目录结构规范

每台开发电脑上，项目目录统一保持如下结构：

```text
D:\Dev\MuseSpace\                   （或 ~/Dev/MuseSpace/，路径统一）
  ├── frontend/                     # Vue + TypeScript 前端
  ├── backend/                      # C# .NET 后端
  ├── infra/                        # 基础设施配置
  │   ├── docker-compose.yml        # 本地数据库服务
  │   └── postgres/
  │       └── init.sql              # 初始化脚本（扩展启用）
  ├── dev-data/                     # 开发数据（不进 Git）
  │   ├── files/
  │   │   ├── raw/                  # 上传原始文档
  │   │   ├── parsed/               # 解析产物 / chunk 中间文件
  │   │   └── exports/              # 导出结果
  │   └── snapshots/                # 数据库快照归档
  ├── scripts/                      # 快照导出 / 导入脚本
  │   ├── export-snapshot.ps1       # Windows 导出
  │   ├── export-snapshot.sh        # Linux/Mac 导出
  │   ├── import-snapshot.ps1       # Windows 导入
  │   └── import-snapshot.sh        # Linux/Mac 导入
  └── .gitignore
```

### .gitignore 必须包含
```gitignore
dev-data/
*.env
*.env.local
appsettings.Development.json
```

---

## 三、Docker Compose 本地数据库配置

路径：`infra/docker-compose.yml`

```yaml
version: '3.9'

services:
  postgres:
    image: pgvector/pgvector:pg16
    container_name: musespace_postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: musespace_dev
      POSTGRES_PASSWORD: dev_password_change_me
      POSTGRES_DB: musespace_dev
    ports:
      - "5432:5432"
    volumes:
      - musespace_pgdata:/var/lib/postgresql/data
      - ./postgres/init.sql:/docker-entrypoint-initdb.d/init.sql
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U musespace_dev"]
      interval: 10s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: musespace_redis
    restart: unless-stopped
    ports:
      - "6379:6379"
    volumes:
      - musespace_redisdata:/data

volumes:
  musespace_pgdata:
  musespace_redisdata:
```

### 启动本地数据库
```bash
cd infra
docker compose up -d
```

### 停止
```bash
docker compose down
```

### 停止并清除所有数据（慎用）
```bash
docker compose down -v
```

---

## 四、PostgreSQL 初始化脚本

路径：`infra/postgres/init.sql`

```sql
-- 启用���要扩展
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- 创建 schema 分层
CREATE SCHEMA IF NOT EXISTS memory;
CREATE SCHEMA IF NOT EXISTS audit;

-- 提示
SELECT 'MuseSpace Dev DB initialized.' AS status;
```

---

## 五、后端连接配置

### C# appsettings.Development.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=musespace_dev;Username=musespace_dev;Password=dev_password_change_me"
  },
  "Storage": {
    "BasePath": "../dev-data/files"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

> ⚠️ 此文件不进 Git，每台电脑本地各自持有。

---

## 六、文件路径规范（重要）

数据库中 **绝对不存本地绝对路径**，统一存逻辑 Key。

### 示例
| 字段 | 错误示例 | 正确示例 |
|------|----------|----------|
| file_path | `D:\Dev\MuseSpace\dev-data\files\raw\doc.pdf` | `raw/workspace-01/doc-001.pdf` |
| parsed_path | `C:\Users\xxx\parsed\chunks.json` | `parsed/workspace-01/doc-001/chunks.json` |
| export_path | `/home/user/musespace/exports/out.md` | `exports/task-001/result.md` |

后端在读取文件时，统一通过配置项 `Storage:BasePath` 拼接完整路径：

```csharp
var fullPath = Path.Combine(_config["Storage:BasePath"], fileKey);
```

这样换电脑时，只要 `BasePath` 指向正确目录，逻辑 Key 永远有效。

---

## 七、数据库结构管理规范

### 使用 EF Core Migration

所有表结构变更必须走 Migration，不允许手动在数据库里改表结构。

```bash
# 生成 Migration
dotnet ef migrations add AddMemoryItemsTable

# 应用到数据库
dotnet ef database update

# 查看当前 migration 状态
dotnet ef migrations list
```

### 换电脑后第一件事
```bash
git pull
dotnet ef database update
```

这样结构永远和代码同步。

---

## 八、快照导出脚本

### Windows 版：scripts/export-snapshot.ps1

```powershell
# MuseSpace 开发快照导出脚本 (Windows)
param(
    [string]$Label = "manual"
)

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$SnapshotName = "snapshot_${Label}_${Timestamp}"
$SnapshotDir = "..\dev-data\snapshots\$SnapshotName"

Write-Host "=== MuseSpace 快照导出 ===" -ForegroundColor Cyan
Write-Host "快照名称: $SnapshotName"

# 创建快照目录
New-Item -ItemType Directory -Force -Path "$SnapshotDir\db" | Out-Null
New-Item -ItemType Directory -Force -Path "$SnapshotDir\files" | Out-Null
New-Item -ItemType Directory -Force -Path "$SnapshotDir\meta" | Out-Null

# 导出数据库
Write-Host "[1/4] 导出 PostgreSQL 数据库..." -ForegroundColor Yellow
$env:PGPASSWORD = "dev_password_change_me"
docker exec musespace_postgres pg_dump `
    -U musespace_dev `
    -d musespace_dev `
    -F c `
    -f "/tmp/musespace_dev.dump"

docker cp musespace_postgres:/tmp/musespace_dev.dump "$SnapshotDir\db\musespace_dev.dump"
Write-Host "  数据库导出完成" -ForegroundColor Green

# 复制文件目录
Write-Host "[2/4] 复制文件目录..." -ForegroundColor Yellow
if (Test-Path "..\dev-data\files") {
    Copy-Item -Recurse -Force "..\dev-data\files\*" "$SnapshotDir\files\"
    Write-Host "  文件目录复制完成" -ForegroundColor Green
} else {
    Write-Host "  文件目录不存在，跳过" -ForegroundColor Gray
}

# 获取 Git 信息
Write-Host "[3/4] 记录版本信息..." -ForegroundColor Yellow
$GitCommit = git rev-parse HEAD 2>$null
$GitBranch = git branch --show-current 2>$null
$MigrationList = dotnet ef migrations list 2>$null

# 生成 manifest
$Manifest = @{
    snapshot_name    = $SnapshotName
    created_at       = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    label            = $Label
    git_commit       = $GitCommit
    git_branch       = $GitBranch
    migrations       = ($MigrationList -join "`n")
    machine          = $env:COMPUTERNAME
    db_name          = "musespace_dev"
    postgres_version = "16"
} | ConvertTo-Json

$Manifest | Out-File -FilePath "$SnapshotDir\meta\manifest.json" -Encoding UTF8
Write-Host "  版本信息记录完成" -ForegroundColor Green

# 打包
Write-Host "[4/4] 压缩快照..." -ForegroundColor Yellow
Compress-Archive -Path $SnapshotDir -DestinationPath "..\dev-data\snapshots\${SnapshotName}.zip"
Remove-Item -Recurse -Force $SnapshotDir
Write-Host "  快照压缩完成" -ForegroundColor Green

Write-Host ""
Write-Host "=== 导出完成 ===" -ForegroundColor Cyan
Write-Host "快照文件: dev-data\snapshots\${SnapshotName}.zip"
Write-Host "请将此文件带到另一台电脑，运行 import-snapshot.ps1 恢复。"
```

---

### Linux/Mac 版：scripts/export-snapshot.sh

```bash
#!/bin/bash
# MuseSpace 开发快照导出脚本 (Linux/Mac)

LABEL=${1:-"manual"}
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
SNAPSHOT_NAME="snapshot_${LABEL}_${TIMESTAMP}"
SNAPSHOT_DIR="../dev-data/snapshots/${SNAPSHOT_NAME}"

echo "=== MuseSpace 快照导出 ==="
echo "快照名称: ${SNAPSHOT_NAME}"

mkdir -p "${SNAPSHOT_DIR}/db"
mkdir -p "${SNAPSHOT_DIR}/files"
mkdir -p "${SNAPSHOT_DIR}/meta"

# 导出数据库
echo "[1/4] ��出 PostgreSQL 数据库..."
docker exec musespace_postgres pg_dump \
    -U musespace_dev \
    -d musespace_dev \
    -F c \
    -f /tmp/musespace_dev.dump

docker cp musespace_postgres:/tmp/musespace_dev.dump "${SNAPSHOT_DIR}/db/musespace_dev.dump"
echo "  数据库导出完成"

# 复制文件目录
echo "[2/4] 复制文件目录..."
if [ -d "../dev-data/files" ]; then
    cp -r ../dev-data/files/. "${SNAPSHOT_DIR}/files/"
    echo "  文件目录复制完成"
else
    echo "  文件目录不存在，跳过"
fi

# 记录版本信息
echo "[3/4] 记录版本信息..."
GIT_COMMIT=$(git rev-parse HEAD 2>/dev/null || echo "unknown")
GIT_BRANCH=$(git branch --show-current 2>/dev/null || echo "unknown")

cat > "${SNAPSHOT_DIR}/meta/manifest.json" <<EOF
{
  "snapshot_name": "${SNAPSHOT_NAME}",
  "created_at": "$(date '+%Y-%m-%d %H:%M:%S')",
  "label": "${LABEL}",
  "git_commit": "${GIT_COMMIT}",
  "git_branch": "${GIT_BRANCH}",
  "machine": "$(hostname)",
  "db_name": "musespace_dev",
  "postgres_version": "16"
}
EOF
echo "  版本信息记录完成"

# 打包
echo "[4/4] 压缩快照..."
cd ../dev-data/snapshots
tar -czf "${SNAPSHOT_NAME}.tar.gz" "${SNAPSHOT_NAME}"
rm -rf "${SNAPSHOT_NAME}"
echo "  快照压缩完成"

echo ""
echo "=== 导出完成 ==="
echo "快照文件: dev-data/snapshots/${SNAPSHOT_NAME}.tar.gz"
echo "请将此文件带到另一台电脑，运行 import-snapshot.sh 恢复。"
```

---

## 九、快照导入脚本

### Windows 版：scripts/import-snapshot.ps1

```powershell
# MuseSpace 开发快照导入脚本 (Windows)
param(
    [Parameter(Mandatory=$true)]
    [string]$SnapshotFile
)

Write-Host "=== MuseSpace 快照导入 ===" -ForegroundColor Cyan
Write-Host "快照文件: $SnapshotFile"

# 解压
$TempDir = "..\dev-data\snapshots\temp_import"
New-Item -ItemType Directory -Force -Path $TempDir | Out-Null
Expand-Archive -Path $SnapshotFile -DestinationPath $TempDir -Force

$SnapshotDir = Get-ChildItem $TempDir | Select-Object -First 1 -ExpandProperty FullName

# 读取 manifest
$Manifest = Get-Content "$SnapshotDir\meta\manifest.json" | ConvertFrom-Json
Write-Host "快照信息: $($Manifest.snapshot_name) / $($Manifest.created_at)" -ForegroundColor Gray

# 启动数据库
Write-Host "[1/4] 启动本地数据库..." -ForegroundColor Yellow
Push-Location ..\infra
docker compose up -d postgres
Start-Sleep -Seconds 5
Pop-Location
Write-Host "  数据库已启动" -ForegroundColor Green

# 恢复数据库
Write-Host "[2/4] 恢复数据库..." -ForegroundColor Yellow
docker cp "$SnapshotDir\db\musespace_dev.dump" musespace_postgres:/tmp/musespace_dev.dump

# 先清空再恢复
docker exec musespace_postgres psql -U musespace_dev -c "DROP DATABASE IF EXISTS musespace_dev;"
docker exec musespace_postgres psql -U musespace_dev -c "CREATE DATABASE musespace_dev;"
docker exec musespace_postgres pg_restore `
    -U musespace_dev `
    -d musespace_dev `
    --no-owner `
    --role=musespace_dev `
    /tmp/musespace_dev.dump

Write-Host "  数据库恢复完成" -ForegroundColor Green

# 恢复文件目录
Write-Host "[3/4] 恢复文件目录..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path "..\dev-data\files" | Out-Null
if (Test-Path "$SnapshotDir\files") {
    Copy-Item -Recurse -Force "$SnapshotDir\files\*" "..\dev-data\files\"
    Write-Host "  文件目录恢复完成" -ForegroundColor Green
} else {
    Write-Host "  快照中无文件目录，跳过" -ForegroundColor Gray
}

# 清理临时目录
Write-Host "[4/4] 清理临时文件..." -ForegroundColor Yellow
Remove-Item -Recurse -Force $TempDir
Write-Host "  清理完成" -ForegroundColor Green

Write-Host ""
Write-Host "=== 导入完成 ===" -ForegroundColor Cyan
Write-Host "来源 Git Commit: $($Manifest.git_commit)"
Write-Host "接下来请执行："
Write-Host "  git pull"
Write-Host "  dotnet ef database update"
```

---

### Linux/Mac 版：scripts/import-snapshot.sh

```bash
#!/bin/bash
# MuseSpace 开发快照导入脚本 (Linux/Mac)

SNAPSHOT_FILE=$1

if [ -z "$SNAPSHOT_FILE" ]; then
    echo "用法: ./import-snapshot.sh <快照文件路径>"
    exit 1
fi

echo "=== MuseSpace 快照导入 ==="
echo "快照文件: ${SNAPSHOT_FILE}"

TEMP_DIR="../dev-data/snapshots/temp_import"
mkdir -p "${TEMP_DIR}"
tar -xzf "${SNAPSHOT_FILE}" -C "${TEMP_DIR}"
SNAPSHOT_DIR=$(ls "${TEMP_DIR}" | head -1)
SNAPSHOT_PATH="${TEMP_DIR}/${SNAPSHOT_DIR}"

echo "[1/4] 启动本地数据库..."
cd ../infra && docker compose up -d postgres && sleep 5 && cd ../scripts
echo "  数据库已启动"

echo "[2/4] 恢复数据库..."
docker cp "${SNAPSHOT_PATH}/db/musespace_dev.dump" musespace_postgres:/tmp/musespace_dev.dump
docker exec musespace_postgres psql -U musespace_dev -c "DROP DATABASE IF EXISTS musespace_dev;"
docker exec musespace_postgres psql -U musespace_dev -c "CREATE DATABASE musespace_dev;"
docker exec musespace_postgres pg_restore \
    -U musespace_dev \
    -d musespace_dev \
    --no-owner \
    --role=musespace_dev \
    /tmp/musespace_dev.dump
echo "  数据库恢复完成"

echo "[3/4] 恢复文件目录..."
mkdir -p ../dev-data/files
if [ -d "${SNAPSHOT_PATH}/files" ]; then
    cp -r "${SNAPSHOT_PATH}/files/." ../dev-data/files/
    echo "  文件目录恢复完成"
else
    echo "  快照中无文件目录，跳过"
fi

echo "[4/4] 清理临时文件..."
rm -rf "${TEMP_DIR}"
echo "  清理完成"

echo ""
echo "=== 导入完成 ==="
echo "接下来请执行："
echo "  git pull"
echo "  dotnet ef database update"
```

---

## 十、完整切换流程（每次切换电脑前必做）

### 从当前电脑切出
```
1. git add . && git commit -m "checkpoint" && git push
2. cd scripts
3. ./export-snapshot.ps1 -Label "before-switch"   (Windows)
   ./export-snapshot.sh before-switch              (Linux/Mac)
4. 将 dev-data/snapshots/snapshot_xxx.zip 复制到 U盘 / 网盘 / 移动硬盘
```

### 在新电脑上恢复
```
1. git clone 或 git pull 最新代码
2. 将快照文件放到 dev-data/snapshots/
3. cd scripts
4. ./import-snapshot.ps1 -SnapshotFile "..\dev-data\snapshots\snapshot_xxx.zip"
   ./import-snapshot.sh ../dev-data/snapshots/snapshot_xxx.tar.gz
5. dotnet ef database update
6. 开始开发
```

---

## 十一、快照建议命名规范

| 场景 | 命名建议 |
|------|----------|
| 切换电脑前 | `before-switch` |
| 完成某功能后 | `feat-memory-layer` |
| 每日归档 | `daily` |
| 上线前存档 | `pre-release` |

---

## 十二、注意事项与常见问题

| 问题 | 处理方式 |
|------|----------|
| 两台电脑都有改动 | 只保留其中一份，另一份数据放弃或手动合并 |
| embedding 导入后与代码不一致 | 重新跑 embedding 生成任务 |
| migration 不匹配 | 先 `dotnet ef database update` 对齐结构 |
| 文件目录很大 | 只带走 `raw/` 和必要文件，`parsed/` 可重新生成 |
| 忘记导出就换电脑了 | 在旧电脑上远程操作导出，或接受数据回退 |

---

## 十三、未来迁移到远程数据库时的平滑升级路径

1. 在云平台开通托管 PostgreSQL（支持 pgvector）
2. 用最后一份本地快照导入到远程库：
   ```bash
   pg_restore -h <远程host> -U <user> -d musespace_dev musespace_dev.dump
   ```
3. 修改 `appsettings.Development.json` 连接串指向远程库
4. 把文件目录迁移到对象存储
5. 后续多电脑开发直接连远程 dev 库，不再需要快照迁移

---

*文档版本：v1.0 | 适用项目：MuseSpace | 最后更新：2026-04-18*