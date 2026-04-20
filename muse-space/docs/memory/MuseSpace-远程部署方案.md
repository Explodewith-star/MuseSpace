# MuseSpace 远程部署方案

> 适用场景：多电脑协同开发、开发/测试/生产环境分离、正式上线
> 技术栈：Vue + TypeScript + C#/.NET + PostgreSQL 16 + pgvector + 对象存储 + Redis + Docker + Nginx

---

## 一、总体架构

```
开发电脑 A / B / C
        │
        ▼
   Git 仓库（GitHub）
        │
   ┌────┴────┐
   ▼         ▼
本地代码    远程 dev 数据库
运行前端     （托管 PostgreSQL）
运行后端
        │
        ▼
  ┌─────────────────────────────────┐
  │         云服务器                │
  │  Nginx → Vue 静态资源           │
  │  Nginx → .NET API               │
  │  .NET Worker（后台任务）        │
  │  Redis（可选）                  │
  └───────────────┬─────────────────┘
                  │
       ┌──────────┼──────────┐
       ▼          ▼          ▼
  托管 PostgreSQL  对象存储   Redis
  + pgvector      OSS/COS    （缓存/队列）
```

---

## 二、资源清单

### 你需要准备以下 3 类资源

| 类型 | 用途 | 推荐选项 |
|------|------|----------|
| **云服务器** | 跑应用（Nginx、前端、后端、Worker） | 阿里云 / 腾讯云轻量 4核4G 起步，4核8G 更稳 |
| **托管 PostgreSQL** | 核心数据库 + pgvector | 见下方详细推荐 |
| **对象存储** | 原始文档、解析文件、导出文件 | 阿里云 OSS / 腾讯云 COS / MinIO |

---

## 三、数据库选型推荐

### 3.1 开发阶段（首选）

| 平台 | 特点 | 是否支持 pgvector | 免费额度 | 推荐指数 |
|------|------|------------------|----------|----------|
| **Neon** | 开发友好，按需计费，分支功能 | ✅ | 有 | ⭐⭐⭐⭐⭐ |
| **Supabase** | PostgreSQL 全托管，控制台好用 | ✅ | 有 | ⭐⭐⭐⭐⭐ |
| **Railway** | 应用+数据库一站式 | ✅ | 有限额 | ⭐⭐⭐⭐ |

> 推荐开发阶段优先选 **Neon** 或 **Supabase**，注册即用，免费额度够个人开发使用。

### 3.2 正式上线（国内部署）

| 平台 | 特点 | 是否支持 pgvector | 推荐指数 |
|------|------|------------------|----------|
| **阿里云 RDS PostgreSQL** | 国内稳定，低延迟 | 需确认版本 | ⭐⭐⭐⭐ |
| **腾讯云 PostgreSQL** | 国内稳定，配套齐全 | 需确认版本 | ⭐⭐⭐⭐ |
| **云服务器自建 PostgreSQL** | 完全控制，安装任意扩展 | ✅ 完全支持 | ⭐⭐⭐⭐ |

> 国内正式上线推荐：**云服务器自建 PostgreSQL + pgvector**（完全可控，pgvector 安装无限制）

---

## 四、环境划分规范

```
musespace_dev      开发环境（所有开发电脑共用��
musespace_staging  测试/预发布环境
musespace_prod     正式生产环境
```

对应对象存储分桶：
```
musespace-dev/
musespace-staging/
musespace-prod/
```

> ⚠️ 开发库和生产库绝对不能混用

---

## 五、存储架构分层设计

### 5.1 数据分层总览

```
┌─────────────────────────────────────────────────────┐
│                    PostgreSQL                        │
│                                                      │
│  public schema        业务实体、用户、会话、消息      │
│  memory schema        记忆层核心表                   │
│  audit schema         操作日志、变更记录             │
└────────────────────────┬────────────────────────────┘
                         │ pgvector 扩展
                    向量检索层
                    chunk_embeddings
                    memory_embeddings
                    entity_embeddings

┌─────────────────────────────────────────────────────┐
│                   对象存储 (OSS)                     │
│                                                      │
│  raw/          原���上传文档                          │
│  parsed/       解析产物 / chunk JSON                 │
│  exports/      输出报告 / 导出结果                   │
│  assets/       静态资源 / 封面图                     │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│                     Redis                            │
│                                                      │
│  上下文缓存    session:{id}                         │
│  任务队列      queue:embedding / queue:chunking      │
│  热点检索缓存  search_cache:{hash}                  │
│  分布式锁      lock:doc:{id}                        │
└─────────────────────────────────────────────────────┘
```

---

### 5.2 核心数据库表设计

#### public schema（业务层）

```sql
-- 工作空间
CREATE TABLE workspaces (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name        VARCHAR(255) NOT NULL,
    description TEXT,
    created_at  TIMESTAMPTZ DEFAULT NOW(),
    updated_at  TIMESTAMPTZ DEFAULT NOW()
);

-- 用户
CREATE TABLE users (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email       VARCHAR(255) UNIQUE NOT NULL,
    display_name VARCHAR(255),
    created_at  TIMESTAMPTZ DEFAULT NOW()
);

-- 文档
CREATE TABLE documents (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID REFERENCES workspaces(id),
    title          VARCHAR(500) NOT NULL,
    file_key       VARCHAR(1000),       -- 对象存储逻辑 Key
    file_hash      VARCHAR(64),
    mime_type      VARCHAR(100),
    file_size      BIGINT,
    status         VARCHAR(50) DEFAULT 'pending',  -- pending/processing/indexed/failed
    source_type    VARCHAR(50),         -- upload/url/api
    created_at     TIMESTAMPTZ DEFAULT NOW(),
    updated_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 文档切片
CREATE TABLE document_chunks (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id    UUID REFERENCES documents(id),
    workspace_id   UUID REFERENCES workspaces(id),
    chunk_index    INT NOT NULL,
    content        TEXT NOT NULL,
    token_count    INT,
    metadata       JSONB DEFAULT '{}',
    created_at     TIMESTAMPTZ DEFAULT NOW()
);
```

#### memory schema（记忆层）

```sql
-- 记忆单元
CREATE TABLE memory.memory_items (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID REFERENCES workspaces(id),
    source_type    VARCHAR(50),         -- chunk / entity / summary / manual
    source_id      UUID,
    content        TEXT NOT NULL,
    summary        TEXT,
    importance     FLOAT DEFAULT 0.5,
    metadata       JSONB DEFAULT '{}',
    created_at     TIMESTAMPTZ DEFAULT NOW(),
    updated_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 记忆关联关系
CREATE TABLE memory.memory_links (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    from_id        UUID REFERENCES memory.memory_items(id),
    to_id          UUID REFERENCES memory.memory_items(id),
    relation_type  VARCHAR(100),
    weight         FLOAT DEFAULT 1.0,
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 实体
CREATE TABLE memory.entities (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID REFERENCES workspaces(id),
    name           VARCHAR(500) NOT NULL,
    entity_type    VARCHAR(100),
    description    TEXT,
    attributes     JSONB DEFAULT '{}',
    created_at     TIMESTAMPTZ DEFAULT NOW(),
    updated_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 实体关系
CREATE TABLE memory.entity_relations (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    from_entity_id UUID REFERENCES memory.entities(id),
    to_entity_id   UUID REFERENCES memory.entities(id),
    relation_type  VARCHAR(200),
    description    TEXT,
    weight         FLOAT DEFAULT 1.0,
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 时间线事件
CREATE TABLE memory.timeline_events (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID REFERENCES workspaces(id),
    event_type     VARCHAR(100),
    subject_id     UUID,
    subject_type   VARCHAR(100),
    description    TEXT,
    event_time     TIMESTAMPTZ,
    metadata       JSONB DEFAULT '{}',
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 向量：chunk embeddings
CREATE TABLE memory.chunk_embeddings (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chunk_id       UUID REFERENCES document_chunks(id),
    workspace_id   UUID REFERENCES workspaces(id),
    model_name     VARCHAR(200),
    embedding      vector(1536),
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 向量：记忆 embeddings
CREATE TABLE memory.memory_embeddings (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    memory_item_id UUID REFERENCES memory.memory_items(id),
    workspace_id   UUID REFERENCES workspaces(id),
    model_name     VARCHAR(200),
    embedding      vector(1536),
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 向量：实体 embeddings
CREATE TABLE memory.entity_embeddings (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_id      UUID REFERENCES memory.entities(id),
    workspace_id   UUID REFERENCES workspaces(id),
    model_name     VARCHAR(200),
    embedding      vector(1536),
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 风格规则
CREATE TABLE memory.style_rules (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID REFERENCES workspaces(id),
    rule_type      VARCHAR(100),
    content        TEXT,
    priority       INT DEFAULT 0,
    active         BOOLEAN DEFAULT TRUE,
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- 输出模板
CREATE TABLE memory.output_templates (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID REFERENCES workspaces(id),
    name           VARCHAR(255),
    template_body  TEXT,
    variables      JSONB DEFAULT '{}',
    created_at     TIMESTAMPTZ DEFAULT NOW()
);

-- Agent 执行记录
CREATE TABLE memory.agent_runs (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID REFERENCES workspaces(id),
    agent_type     VARCHAR(100),
    status         VARCHAR(50),
    input          JSONB,
    output         JSONB,
    error          TEXT,
    started_at     TIMESTAMPTZ,
    finished_at    TIMESTAMPTZ,
    created_at     TIMESTAMPTZ DEFAULT NOW()
);
```

#### 向量索引

```sql
-- HNSW 索引（推荐，查询更快）
CREATE INDEX idx_chunk_embedding_hnsw
    ON memory.chunk_embeddings
    USING hnsw (embedding vector_cosine_ops);

CREATE INDEX idx_memory_embedding_hnsw
    ON memory.memory_embeddings
    USING hnsw (embedding vector_cosine_ops);

CREATE INDEX idx_entity_embedding_hnsw
    ON memory.entity_embeddings
    USING hnsw (embedding vector_cosine_ops);

-- 全文搜索索引（支持 Hybrid RAG）
CREATE INDEX idx_chunks_content_trgm
    ON document_chunks
    USING gin (content gin_trgm_ops);
```

---

## 六、云服务器部署配置

### 6.1 服务器环境初始化

```bash
# 更新系统
sudo apt update && sudo apt upgrade -y

# 安装基础工具
sudo apt install -y curl git unzip nginx certbot python3-certbot-nginx

# 安装 Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# 安装 Docker Compose
sudo apt install -y docker-compose-plugin

# 安装 .NET 8
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update && sudo apt install -y dotnet-sdk-8.0
```

---

### 6.2 服务器目录结构

```text
/opt/musespace/
  ├── frontend/           # Vue 打包产物
  ├── backend/            # .NET 发布产物
  ├── worker/             # .NET Worker 发布产物
  ├── nginx/
  │   └── musespace.conf  # Nginx 配置
  ├── docker-compose.yml  # Redis 等辅助服务
  └── .env                # 环境变量（不进 Git）
```

---

### 6.3 服务器 Docker Compose（辅助服务）

```yaml
version: '3.9'

services:
  redis:
    image: redis:7-alpine
    container_name: musespace_redis
    restart: unless-stopped
    ports:
      - "127.0.0.1:6379:6379"
    volumes:
      - redis_data:/data
    command: redis-server --requirepass ${REDIS_PASSWORD}

volumes:
  redis_data:
```

---

### 6.4 Nginx 配置

路径：`/etc/nginx/sites-available/musespace`

```nginx
server {
    listen 80;
    server_name your-domain.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name your-domain.com;

    ssl_certificate     /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;

    # Vue 前端
    location / {
        root /opt/musespace/frontend;
        index index.html;
        try_files $uri $uri/ /index.html;
        
        # 缓存静态资源
        location ~* \.(js|css|png|jpg|ico|woff2)$ {
            expires 30d;
            add_header Cache-Control "public, immutable";
        }
    }

    # .NET API
    location /api/ {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 300s;
    }

    # 文件大小限制（上传文档用）
    client_max_body_size 100M;
}
```

启用配置：
```bash
sudo ln -s /etc/nginx/sites-available/musespace /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

---

### 6.5 SSL 证书（Let's Encrypt）

```bash
sudo certbot --nginx -d your-domain.com
# 自动续期
sudo certbot renew --dry-run
```

---

## 七、后端配置

### appsettings.Production.json 结构

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<db-host>;Port=5432;Database=musespace_prod;Username=<user>;Password=<password>;SSL Mode=Require"
  },
  "Storage": {
    "Provider": "OSS",
    "Endpoint": "https://oss-cn-shanghai.aliyuncs.com",
    "BucketName": "musespace-prod",
    "AccessKeyId": "",
    "AccessKeySecret": "",
    "BaseUrl": "https://musespace-prod.oss-cn-shanghai.aliyuncs.com"
  },
  "Redis": {
    "ConnectionString": "127.0.0.1:6379,password=<redis-password>"
  },
  "Embedding": {
    "Provider": "OpenAI",
    "ModelName": "text-embedding-3-small",
    "Dimension": 1536
  }
}
```

> ⚠️ 生产配置不进 Git，通过环境变量或 Secret Manager 注入

---

### 服务器环境变量（/opt/musespace/.env）

```env
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000

ConnectionStrings__DefaultConnection=Host=xxx;...
Storage__AccessKeyId=xxx
Storage__AccessKeySecret=xxx
Redis__ConnectionString=127.0.0.1:6379,password=xxx
```

---

## 八、CI/CD 自动化部署（可选但推荐）

### GitHub Actions 示例

路径：`.github/workflows/deploy.yml`

```yaml
name: Deploy MuseSpace

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0'

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Build Frontend
        run: |
          cd frontend
          npm ci
          npm run build

      - name: Build Backend
        run: |
          cd backend
          dotnet publish -c Release -o ./publish

      - name: Deploy to Server
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.SERVER_HOST }}
          username: ${{ secrets.SERVER_USER }}
          key: ${{ secrets.SERVER_SSH_KEY }}
          script: |
            # 部署前端
            rm -rf /opt/musespace/frontend/*
            
            # 部署后端
            systemctl stop musespace-api
            dotnet ef database update --project /opt/musespace/backend
            systemctl start musespace-api
            
            # 重载 Nginx
            nginx -s reload
```

---

## 九、systemd 服务配置

### .NET API 服务

路径：`/etc/systemd/system/musespace-api.service`

```ini
[Unit]
Description=MuseSpace API
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/opt/musespace/backend
ExecStart=/usr/bin/dotnet /opt/musespace/backend/MuseSpace.Api.dll
Restart=always
RestartSec=10
EnvironmentFile=/opt/musespace/.env
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

### .NET Worker 服务

路径：`/etc/systemd/system/musespace-worker.service`

```ini
[Unit]
Description=MuseSpace Background Worker
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/opt/musespace/worker
ExecStart=/usr/bin/dotnet /opt/musespace/worker/MuseSpace.Worker.dll
Restart=always
RestartSec=15
EnvironmentFile=/opt/musespace/.env
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

启用服务：
```bash
sudo systemctl daemon-reload
sudo systemctl enable musespace-api musespace-worker
sudo systemctl start musespace-api musespace-worker
sudo systemctl status musespace-api
```

---

## 十、多电脑开发配置规范

### 每台开发电脑本地保持
```text
backend/appsettings.Development.json    # 本地，不进 Git
frontend/.env.local                     # 本地，不进 Git
```

### appsettings.Development.json（开发机）
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<远程dev数据库host>;Port=5432;Database=musespace_dev;Username=<user>;Password=<password>;SSL Mode=Require"
  },
  "Storage": {
    "Provider": "OSS",
    "BucketName": "musespace-dev"
  },
  "Redis": {
    "ConnectionString": "<远程redis或本地redis>"
  }
}
```

### 前端 .env.local（开发机）
```env
VITE_API_BASE_URL=http://localhost:5000/api
```

### 新电脑加入开发流程
```bash
# 1. 拉代码
git clone https://github.com/Explodewith-star/MuseSpace.git

# 2. 配置本地开发环境变量
cp backend/appsettings.Development.template.json backend/appsettings.Development.json
# 填写远程 dev 数据库连接串

# 3. 同步数据库结构
cd backend
dotnet ef database update

# 4. 启动开发
dotnet run
cd ../frontend && npm run dev
```

---

## 十一、安全规范

### 数据库安全
- [ ] 只允许服务器 IP 和开发机 IP 连接（白名单）
- [ ] 必须开启 SSL
- [ ] dev / staging / prod 使用独立账户
- [ ] 定期备份（至少每天一次）
- [ ] 连接串不进 Git

### 服务器安全
- [ ] 关闭不必要端口
- [ ] 只开放 80、443、22
- [ ] 数据库端口不对公网开放
- [ ] Redis 绑定 127.0.0.1
- [ ] 使用 SSH Key 登录，关闭密码登录
- [ ] 定期更新系统

### 对象存储安全
- [ ] 原始文档设为私有读（不公开访问）
- [ ] 通过后端服务签名 URL 控制访问
- [ ] Access Key 用环境变量注入
- [ ] 开启版本控制

---

## 十二、备份方案

### 数据库备份脚本（服务器上定时执行）

```bash
#!/bin/bash
# /opt/musespace/scripts/backup-db.sh

DATE=$(date +"%Y%m%d_%H%M%S")
BACKUP_DIR="/opt/musespace/backups"
mkdir -p $BACKUP_DIR

pg_dump \
  -h $DB_HOST \
  -U $DB_USER \
  -d musespace_prod \
  -F c \
  -f "${BACKUP_DIR}/prod_${DATE}.dump"

# 删除 7 天前的备份
find $BACKUP_DIR -name "*.dump" -mtime +7 -delete

echo "备份完成: prod_${DATE}.dump"
```

```bash
# 加入定时任务
crontab -e
# 每天凌晨 3 点备份
0 3 * * * /opt/musespace/scripts/backup-db.sh >> /var/log/musespace-backup.log 2>&1
```

---

## 十三、性价比方案对比

| 方案 | 月费估算 | 适合阶段 | 说明 |
|------|----------|----------|------|
| **入门版**：4C4G 服务器自建全套 | ¥30~50/月 | 个人开发/测试 | 全部在一台机器，资源紧 |
| **推荐版**：4C4G 服务器 + Neon/Supabase 免费版 | ¥30~50/月 | 开发+小规模上线 | 分离最关键的数据库 |
| **标准版**：4C8G 服务器 + 托管 PostgreSQL 基础版 | ¥150~300/月 | 正式上线 | 稳定，可承载一定用户量 |
| **扩展版**：多台服务器 + 云数据库高可用版 | ¥500+/月 | 规模化 | 多人使用，高可用要求 |

---

## 十四、推荐实施顺序

```
阶段 1：开发期（现在）
  ├── 注册 Neon 或 Supabase，创建 musespace_dev
  ├── 启用 pgvector 扩展
  ├── 运行 EF Core Migration 建表
  ├── 注册对象存储，创建 musespace-dev bucket
  ├── 配置本地开发机连接远程 dev 库
  └── 开始开发记忆层

阶段 2：测试期
  ├── 购买云服务器（4C4G 起步）
  ├── 部署 Nginx + Vue + .NET API
  ├── 配置 CI/CD
  ├── 创建 musespace_staging 数据库
  └── 验证全流程

阶段 3：上线期
  ├── 升级服务器配置（4C8G）
  ├── 创建 musespace_prod 数据库
  ├── 配置域名 + SSL
  ├── 完善备份策略
  └── 监控与报警配置
```

---

*文档版本：v1.0 | 适用项目：MuseSpace | 最后更新：2026-04-18*