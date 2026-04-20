# MuseSpace 记忆层实行计划

> 基于 .NET Semantic Kernel + PostgreSQL + pgvector + Hybrid RAG + Multi-Agent
> 技术栈：Vue3 + TypeScript + ASP.NET Core + PostgreSQL 16 + pgvector + Redis + 对象存储
> 文档版本：v1.0 | 最后更新：2026-04-19

---

## 文档说明

本文档将 MuseSpace 记忆层从零到完整的开发过程，拆分为 **4 个阶段 + 1 个扩展阶段**，每个阶段包含：
- 明确的目标与边界
- 具体实施步骤
- 每步的细节注意事项
- 阶段性交付内容清单
- 阶段完成的验收标准

**核心原则：**
- 底座成熟优先，前沿能力后置
- 数据模型自己掌握，框架能力按需嫁接
- 每阶段可独立运行、可验收
- 不过度设计，不提前优化

---

## 阶段总览

```
阶段一：基础设施搭建          → 开发环境、数据库、存储架构就绪
阶段二：知识构建管道           → 文档导入、切片、向量化、基础检索
阶段三：记忆层核心             → 记忆单元、实体关系、状态追踪、Hybrid RAG
阶段四：智能编排层             → Semantic Kernel 接入、Agent 框架、输出对齐
阶段五（扩展）：前沿能力       → Graph RAG、多 Agent 协作、长期记忆演化
```

---

## 阶段一：基础设施搭建

### 目标
建立稳定可用的开发环境与存储架构，使所有开发电脑能无缝接入同一套开发数据，为后续所有模块提供底座。

### 时间参考
2～4 天

---

### 步骤 1.1：确定并搭建开发环境方案

#### 选择以下方案之一

**方案 A：本地 Docker（低成本起步）**
- 每台开发电脑本地运行 PostgreSQL + Redis
- 换电脑时用快照脚本迁移
- 参考：`MuseSpace-本地双电脑切换开发方案.md`

**��案 B：远程托管数据库（推荐）**
- 注册 Neon 或 Supabase，创建远程开发库
- 多台开发电脑共用同一个 `musespace_dev`
- 参考：`MuseSpace-远程部署方案.md`

#### 细节注意
- 无论选哪种方案，开发库 / 测试库 / 生产库必须从一开始就分开，不能混用
- 数据库连接串必须通过 `.env` 或 C# User Secrets 管理，绝不进 Git
- 远程数据库必须开启 SSL，并设置 IP 白名单
- 如果选方案 B，建议选支持 pgvector 的平台（Neon / Supabase 均支持）

#### 交付内容
- [ ] 开发数据库可连接（`musespace_dev`）
- [ ] 测试库占位创建（`musespace_staging`）
- [ ] `.gitignore` 正确屏蔽敏感配置
- [ ] 本地或远程 Redis 可用
- [ ] 所有开发电脑均可连接到同一个开发数据库

---

### 步骤 1.2：启用 PostgreSQL 扩展

```sql
-- 在 musespace_dev 中执行
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pgcrypto;
```

#### 细节注意
- 执行前确认 PostgreSQL 版本 ≥ 16，pgvector ≥ 0.7.0
- 如果使用托管数据库（Neon/Supabase），在控制台或通过 SQL 执行，权限通常已够
- 如果使用自建 PostgreSQL，需要先安装 pgvector 扩展包
- 扩展启用命令需要超级用户或对应权限，连接时注意使用管理员账户

#### 验证
```sql
SELECT * FROM pg_extension WHERE extname IN ('vector', 'pg_trgm', 'uuid-ossp');
-- 应返回 3 条记录
```

#### 交付内容
- [ ] 4 个扩展全部启用并验证

---

### 步骤 1.3：创建 Schema 分层结构

```sql
CREATE SCHEMA IF NOT EXISTS memory;
CREATE SCHEMA IF NOT EXISTS audit;
```

#### 细节注意
- `public` schema：业务实体层（用户、文档、会话、消息）
- `memory` schema：记忆层核心表（memory items、embeddings、entities）
- `audit` schema：操作日志、变更记录（暂时可以只建占位，后续填充）
- Schema 分层的意义在于：逻辑清晰，权限可以精细控制，未来可以按 schema 做数据隔离

#### 交付内容
- [ ] 3 个 schema 创建完成

---

### 步骤 1.4：搭建对象存储

#### 选择以下方案之一

**方案 A：云对象存储（推荐）**
- 阿里云 OSS / 腾讯云 COS / MinIO（自托管）
- 创建两个 bucket：`musespace-dev` 和 `musespace-prod`
- 设置为私有读，通过后端签名 URL 访问

**方案 B：本地文件目录（过渡方案）**
- 本地 `dev-data/files/` 目录模拟对象存储
- 后续迁移时只需改配置，业务代码通过接口抽象不变

#### 目录结构规范
```
{bucket}/
  raw/           # 上传的原始文档
  parsed/        # 解析产物（chunk JSON、结构化文本）
  exports/       # 输出报告、导出结果
  assets/        # 静态资源、封面图
```

#### 细节注意
- 数据库中只存文件的逻辑 Key（如 `raw/ws-001/doc-001.pdf`），绝不存绝对路径
- 后端统一通过 `IStorageService` 接口操作文件，底层实现可替换
- Access Key 必须用环境变量注入，不进代码和 Git
- 开发 bucket 和生产 bucket 完全隔离，误操作风险最小化

#### 交付内容
- [ ] 对象存储可用（云端或本地目录均可）
- [ ] 目录结构规范建立
- [ ] `IStorageService` 接口设计完成（哪怕先用本地实现）

---

### 步骤 1.5：初始化项目代码结构

#### 后端推荐结构
```
MuseSpace.sln
  MuseSpace.Api/              # Web API 入口
  MuseSpace.Worker/           # 后台任务（embedding、chunking）
  MuseSpace.Core/             # 领域模型、接口定义
  MuseSpace.Infrastructure/   # 数据库、存储、外部服务实现
  MuseSpace.Memory/           # 记忆层专属模块
  MuseSpace.Tests/            # 单元测试 / 集成测试
```

#### 细节注意
- `Core` 层不依赖任何基础设施（无 EF Core、无 HTTP 客户端），只有接口和模型
- `Infrastructure` 层实现具体的数据访问、存储操作
- `Memory` 模块单独拆包，是这个项目的核心，后续会最重
- `Worker` 单独部署，避免长时间任务阻塞 API 进程

#### 交付内容
- [ ] 解决方案结构搭建完成
- [ ] 各项目可独立编译
- [ ] EF Core 配置完成，可连接数据库

---

### 步骤 1.6：建立 EF Core Migration 规范

```bash
# 初始化 Migration
dotnet ef migrations add InitialCreate --project MuseSpace.Infrastructure

# 应用到数据库
dotnet ef database update --project MuseSpace.Infrastructure
```

#### 细节注意
- 所有表结构变更必须通过 Migration，严禁手动改数据库结构
- Migration 文件必须进入 Git 仓库
- 每个 Migration 命名要有意义：`AddMemoryItemsTable`、`AddVectorIndex` 等
- 每次换电脑或新成员加入，第一件事是 `dotnet ef database update`

#### 交付内容
- [ ] 初始 Migration 文件生成并应用
- [ ] Migration 命名规范文档（一句话即可，写在 README 里）

---

### 阶段一验收标准
- [ ] 任意开发电脑执行 `git pull` + `dotnet ef database update` 后，可立即开始开发
- [ ] 数据库连接正常，4 个扩展已启用
- [ ] 对象存储可上传下载文件
- [ ] 项目可编译运行，API 返回健康检查响应

---

## 阶段二：知识构建管道

### 目标
实现从"原始文档上传"到"向量化完成可检索"的完整流水线，包括文档摄入、切片、向量化、存储、基础检索。

### 时间参考
1～3 周

---

### 步骤 2.1：设计并建立核心数据表

#### 公共业务层（public schema）

```sql
-- 工作空间
CREATE TABLE workspaces (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name         VARCHAR(255) NOT NULL,
    description  TEXT,
    settings     JSONB DEFAULT '{}',
    created_at   TIMESTAMPTZ DEFAULT NOW(),
    updated_at   TIMESTAMPTZ DEFAULT NOW()
);

-- 文档主表
CREATE TABLE documents (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    title         VARCHAR(500) NOT NULL,
    file_key      VARCHAR(1000),
    file_hash     VARCHAR(64),
    mime_type     VARCHAR(100),
    file_size     BIGINT,
    status        VARCHAR(50) DEFAULT 'pending',
    -- pending / processing / indexed / failed
    source_type   VARCHAR(50) DEFAULT 'upload',
    -- upload / url / api / manual
    metadata      JSONB DEFAULT '{}',
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW()
);

-- 文档切片表
CREATE TABLE document_chunks (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id   UUID NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
    workspace_id  UUID NOT NULL REFERENCES workspaces(id),
    chunk_index   INT NOT NULL,
    content       TEXT NOT NULL,
    token_count   INT,
    char_count    INT,
    start_offset  INT,
    end_offset    INT,
    metadata      JSONB DEFAULT '{}',
    created_at    TIMESTAMPTZ DEFAULT NOW()
);
```

#### 细节注意
- `status` 字段是后续异步处理的关键，必须设计清楚状态流转：
  ```
  pending → processing → indexed
                      ↘ failed
  ```
- `metadata` 使用 JSONB 存储灵活元信息（页码、章节、标签等），不要一开始就加太多列
- `file_hash` 用于后续去重，避免同一文件重复导入
- `source_type` 要从一开始就记录，后续多源接入时会用到
- `chunk_index` 表示该 chunk 在文档中的顺序，检索结果展示时需要

#### 交付内容
- [ ] 以上表结构通过 EF Core Migration 创建
- [ ] 对应的 C# 实体类和 DbContext 配置完成

---

### 步骤 2.2：实现文档上传接口

#### API 设计
```
POST /api/documents/upload
  - 接收文件（multipart/form-data）
  - 校验文件类型（PDF、Word、TXT、Markdown 等）
  - 计算文件 Hash（SHA256）
  - 上传到对象存储
  - 创建 documents 记录，状态设为 pending
  - 投递到处理队列
  - 返回 document_id

GET /api/documents/{id}/status
  - 查询文档处理状态
```

#### 细节注意
- 文件大小限制要在 Nginx 和 API 层都配置（Nginx 的 `client_max_body_size`）
- 文件类型要做双重校验：Content-Type 和文件内容的 magic bytes，防止伪装上传
- 计算 Hash 后，先查库是否已存在相同 Hash 的文档（去重逻辑）
- 上传对象存储和写数据库这两步，要考虑事务问题：
  - 推荐：先写数据库（pending 状态），再上传对象存储，失败了后台可重试
  - 不推荐：先上传存储、存储成功但数据库写入失败，会有孤儿文件
- 投递队列可以用 Redis 的 List 或 Channel（初期可以简化为直接调用，但要设计成可异步）

#### 交付内容
- [ ] 文档上传接口可用
- [ ] 文件存到对象存储，数据库记录创建成功
- [ ] 重复文件上传有提示

---

### 步骤 2.3：实现文档解析模块

#### 支持的文件类型（按优先级）
1. **TXT / Markdown**：直接读取文本
2. **PDF**：推荐使用 `PdfPig` 或 `iTextSharp`
3. **Word（.docx）**：使用 `DocumentFormat.OpenXml`
4. **HTML**：使用 `HtmlAgilityPack` 提取正文

#### 细节注意
- 文档解析是异步任务，不在 API 请求中同步执行
- 解析结果（纯文本）可以先保存到对象存储的 `parsed/` 目录，作为中间产物缓存
- PDF 解析要处理各种异常：加密 PDF、扫描版 PDF（纯图片）、乱码问题
  - 加密 PDF：返回错误状态并告知用户
  - 扫描版 PDF：暂时标记为 `requires_ocr`，后续再接入 OCR
- 解析时要记录原始文档的总字符数、总页数，写入 `documents.metadata`
- 解析失败时更新状态为 `failed`，并记录失败原因到 `metadata.error`

#### 交付内容
- [ ] 至少支持 TXT 和 Markdown 的解析
- [ ] PDF 解析（非扫描版）
- [ ] 解析失败有明确的错误记录

---

### 步骤 2.4：实现文本切片（Chunking）模块

#### 推荐切片策略（按复杂度递进）

**初期：固定大小 + 重叠滑窗**
```
chunk_size    = 512 tokens（或约 800 字符）
overlap_size  = 64 tokens（或约 100 字符）
```

**进阶：语义感知切片**
- 按段落边界切片
- 按标题层级切片（Markdown / Word 标题）
- 避免在句子中间截断

#### 细节注意
- Token 计数和字符计数要都记录，因为不同 embedding 模型有不同的 token 限制
- Chunk 不能太短（< 100 字符）：语义太少，检索召回质量差
- Chunk 不能太长（> 2000 字符）：超出 embedding 模型上下文，或语义混杂
- Overlap（重叠）的意义在于保留跨 chunk 边界的上下文，检索时不会丢失语义
- 切片结果写入 `document_chunks` 表，更新 `documents.status` 为下一个状态
- 切片策略要设计成可配置的（放在 `documents.metadata` 或系统配置中），以后可以按文档类型选不同策略

#### 交付内容
- [ ] 固定大小 + 重叠滑窗切片实现
- [ ] Chunk 写入 `document_chunks` 表
- [ ] Token/字符统计记录

---

### 步骤 2.5：建立向量表并实现 Embedding 生成

#### 建表

```sql
-- 向量存储表（memory schema）
CREATE TABLE memory.chunk_embeddings (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chunk_id      UUID NOT NULL REFERENCES document_chunks(id) ON DELETE CASCADE,
    workspace_id  UUID NOT NULL REFERENCES workspaces(id),
    model_name    VARCHAR(200) NOT NULL,
    model_version VARCHAR(100),
    embedding     vector(1536),
    created_at    TIMESTAMPTZ DEFAULT NOW()
);

-- HNSW 向量索引
CREATE INDEX idx_chunk_embeddings_hnsw
    ON memory.chunk_embeddings
    USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
```

#### Embedding 模型选择

| 方案 | 适合场景 | 维度 | 成本 |
|------|---------|------|------|
| OpenAI `text-embedding-3-small` | 快速起步 | 1536 | 按 token 付费 |
| OpenAI `text-embedding-3-large` | 更高精度 | 3072 | 较贵 |
| 本地模型（Ollama + nomic-embed-text） | 零成本，隐私 | 768 | 需要 GPU/CPU |

**推荐：开发阶段用 OpenAI small 版本，后续可切换本地模型**

#### 细节注意
- Embedding 生成是异步后台任务，不在 API 线程中做
- 每个 Chunk 生成 Embedding 后，立即写入 `chunk_embeddings`，不要批量攒完再写（防中断丢失）
- 要记录 `model_name` 和 `model_version`：当你换模型时，旧 embedding 要标记为失效，重新生成
- HNSW 索引参数说明：
  - `m = 16`：每个节点的最大连接数，越大精度越高但内存越多
  - `ef_construction = 64`：建索引时的搜索范围，越大精度越高但建索引越慢
  - 初期用这个默认值，数据量大了再调整
- Embedding API 有速率限制，要有重试逻辑和限速控制（每秒请求数）

#### 交付内容
- [ ] `chunk_embeddings` 表和 HNSW 索引创建
- [ ] Embedding 生成 Worker 实现
- [ ] 向量成功写入并可查询

---

### 步骤 2.6：实现基础向量检索

#### 基础向量检索 SQL
```sql
-- 余弦相似度检索，返回最相似的 N 个 chunk
SELECT
    dc.id,
    dc.content,
    dc.document_id,
    1 - (ce.embedding <=> $query_embedding) AS similarity
FROM memory.chunk_embeddings ce
JOIN document_chunks dc ON dc.id = ce.chunk_id
WHERE ce.workspace_id = $workspace_id
  AND ce.model_name = $model_name
ORDER BY ce.embedding <=> $query_embedding
LIMIT 10;
```

#### 细节注意
- `<=>` 是 pgvector 的余弦距离运算符，值越小越相似，所以要 `1 - 距离` 转成相似度
- 一定要加 `workspace_id` 过滤，防止跨工作空间的数据泄露
- 结果数量（`LIMIT`）建议设成可配置参数，不要写死
- 检索时要传入正确的 `model_name`，只检索同模型生成的向量

#### 交付内容
- [ ] 基础向量检索接口可用
- [ ] 给定一段文本，能返回最相关的 chunk 列表

---

### 步骤 2.7：实现 Hybrid RAG（混合检索）

#### 混合检索策略
将向量检索 + 关键词检索结合：

```sql
-- 关键词检索（pg_trgm 模糊匹配）
SELECT
    id,
    content,
    similarity(content, $query) AS text_score
FROM document_chunks
WHERE workspace_id = $workspace_id
  AND content % $query
ORDER BY text_score DESC
LIMIT 20;
```

然后在应用层融合两路结果（RRF 倒数排名融合算法）：

```
RRF_score(doc) = Σ 1 / (k + rank_i)
k = 60（常用值）
rank_i = 该文档在第 i 路检索中的排名
```

#### 细节注意
- 向量检索擅长语义理解，关键词检索擅长精确匹配，两者互补
- RRF 融合不需要对两路分数做归一化，实现简单且效果好
- 关键词检索的 `%` 运算符需要 `pg_trgm` 扩展（已在步骤 1.2 启用）
- 融合后的结果要去重（同一个 chunk 可能在两路都出现）
- 最终返回给 LLM 的 chunk 数量建议控制在 5～10 个，太多会超出上下文窗口

#### 交付内容
- [ ] 关键词检索实现
- [ ] RRF 融合逻辑实现
- [ ] Hybrid RAG 接口可用，可返回混合检索结果

---

### 阶段二验收标准
- [ ] 上传一个 PDF/TXT 文档，系统能自动完成解析→切片→向量化
- [ ] 给定一个自然语言问题，能返回最相关的文档片段
- [ ] Hybrid RAG 检索结果比纯向量检索有明显改善

---

## 阶段三：记忆层核心

### 目标
在文档检索的基础上，建立持久化的记忆单元、实体关系、时序状态追踪，形成真正的"记忆层"而不只是"检索层"。

### 时间参考
2～4 周

---

### 步骤 3.1：建立记忆单元表

```sql
-- 记忆单元（memory schema）
CREATE TABLE memory.memory_items (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id),
    source_type   VARCHAR(50) NOT NULL,
    -- chunk / entity / summary / manual / agent_output
    source_id     UUID,
    content       TEXT NOT NULL,
    summary       TEXT,
    importance    FLOAT DEFAULT 0.5 CHECK (importance >= 0 AND importance <= 1),
    access_count  INT DEFAULT 0,
    last_accessed TIMESTAMPTZ,
    expires_at    TIMESTAMPTZ,        -- 可选：记忆过期时间
    metadata      JSONB DEFAULT '{}',
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW()
);

-- 记忆向量
CREATE TABLE memory.memory_embeddings (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    memory_item_id  UUID NOT NULL REFERENCES memory.memory_items(id) ON DELETE CASCADE,
    workspace_id    UUID NOT NULL,
    model_name      VARCHAR(200) NOT NULL,
    embedding       vector(1536),
    created_at      TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_memory_embeddings_hnsw
    ON memory.memory_embeddings
    USING hnsw (embedding vector_cosine_ops);

-- 记忆关联
CREATE TABLE memory.memory_links (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    from_id       UUID NOT NULL REFERENCES memory.memory_items(id),
    to_id         UUID NOT NULL REFERENCES memory.memory_items(id),
    relation_type VARCHAR(100),
    weight        FLOAT DEFAULT 1.0,
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT no_self_link CHECK (from_id != to_id)
);
```

#### 细节注意
- `importance` 字段是记忆层的核心设计之一：它决定了记忆的"权重"，影响检索优先级
  - 可以手动设置（用户标记重要）
  - 也可以根据访问频率自动更新（被检索越多，importance 越高）
- `access_count` 和 `last_accessed` 用于实现"记忆衰减"逻辑（访问少的记忆重要性慢慢降低）
- `expires_at` 为可选字段，用于实现"短期记忆"（如对话上下文）和"长期记忆"分层
- `source_type` 要从一开始设计清楚，方便后续按来源过滤记忆

#### 交付内容
- [ ] 以上表结构通过 Migration 创建
- [ ] 基础的 CRUD 接口完成

---

### 步骤 3.2：实现自动记忆提取

#### 从文档 Chunk 提取记忆
文档处理完成后，自动从重要 chunk 提取记忆单元：

策略：
1. 按 chunk 重要性评分（长度 + 关键词密度 + 语义多样性）
2. 对高分 chunk 生成摘要（调用 LLM）
3. 将摘要作为 memory_item 存入，source_type = `chunk`

#### 细节注意
- 不是每个 chunk 都要变成 memory_item，避免记忆库过度膨胀
- 摘要生成是 LLM 调用，有成本，要控制生成频率和批次
- 要记录从哪个 chunk 来（`source_id`），方便追溯原始文本
- 初期可以简化：不做自动摘要，直接把重要 chunk 的内容复制为 memory_item

#### 交付内容
- [ ] 文档处理完成后，自动生成若干 memory_items
- [ ] memory_items 有对应的向量

---

### 步骤 3.3：建立实体与关系表

```sql
-- 实体
CREATE TABLE memory.entities (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id),
    name          VARCHAR(500) NOT NULL,
    entity_type   VARCHAR(100),
    -- person / place / concept / event / product ...
    description   TEXT,
    aliases       TEXT[],              -- 别名数组
    attributes    JSONB DEFAULT '{}',
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (workspace_id, name, entity_type)
);

-- 实体向量
CREATE TABLE memory.entity_embeddings (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_id    UUID NOT NULL REFERENCES memory.entities(id) ON DELETE CASCADE,
    workspace_id UUID NOT NULL,
    model_name   VARCHAR(200) NOT NULL,
    embedding    vector(1536),
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- 实体关系
CREATE TABLE memory.entity_relations (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID NOT NULL,
    from_entity_id UUID NOT NULL REFERENCES memory.entities(id),
    to_entity_id   UUID NOT NULL REFERENCES memory.entities(id),
    relation_type  VARCHAR(200) NOT NULL,
    -- belongs_to / depends_on / related_to / opposite_of ...
    description    TEXT,
    weight         FLOAT DEFAULT 1.0,
    source_chunk   UUID REFERENCES document_chunks(id),
    created_at     TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT no_self_relation CHECK (from_entity_id != to_entity_id)
);
```

#### 细节注意
- `aliases` 数组用于存同义词/别名，检索时用别名也能匹配到实体
- 实体去重很关键：`UNIQUE (workspace_id, name, entity_type)` 防止同一实体重复录入
  - 但别名和不同写法会导致重复，需要在应用层做实体合并（Entity Resolution）
  - 初期可以简化：先不做自动合并，接受少量重复实体
- `relation_type` 要有一套预定义的关系类型，避免每次都是自由文本导致查不到
- `source_chunk` 记录关系来自哪个 chunk，可追溯来源，也可在原文中高亮展示

#### 交付内容
- [ ] 实体和关系表创建
- [ ] 可以手动录入实体和关系（API 接口）
- [ ] 实体向量生成

---

### 步骤 3.4：实现时序状态追踪

```sql
-- 时间线事件
CREATE TABLE memory.timeline_events (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id),
    event_type   VARCHAR(100) NOT NULL,
    subject_id   UUID,
    subject_type VARCHAR(100),
    description  TEXT,
    event_time   TIMESTAMPTZ NOT NULL,
    metadata     JSONB DEFAULT '{}',
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- Agent 执行状态
CREATE TABLE memory.agent_states (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL,
    session_id   UUID,
    agent_type   VARCHAR(100) NOT NULL,
    state        JSONB NOT NULL DEFAULT '{}',
    checksum     VARCHAR(64),
    version      INT DEFAULT 1,
    created_at   TIMESTAMPTZ DEFAULT NOW(),
    updated_at   TIMESTAMPTZ DEFAULT NOW()
);
```

#### 细节注意
- `timeline_events` 是不可变的事件流，只写入不修改，用于回溯历史
- `agent_states` 存当前状态快照，每次更新要用乐观锁（version 字段）防并发问题
- `event_time` 和 `created_at` 分开：`event_time` 是事件实际发生时间，`created_at` 是记录入库时间
- `metadata` 存灵活的上下文：比如某个文档被处理时，记录用了什么参数、耗时多少等

#### 交付内容
- [ ] 时间线事件表创建
- [ ] 重要操作（文档导入、实体更新、Agent 执行）自动记录 timeline

---

### 步骤 3.5：实现风格规则与输出模板库

```sql
-- 风格规则
CREATE TABLE memory.style_rules (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL,
    rule_type    VARCHAR(100),
    -- tone / format / vocabulary / structure ...
    content      TEXT NOT NULL,
    priority     INT DEFAULT 0,
    active       BOOLEAN DEFAULT TRUE,
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

-- 输出模板
CREATE TABLE memory.output_templates (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL,
    name          VARCHAR(255) NOT NULL,
    description   TEXT,
    template_body TEXT NOT NULL,
    variables     JSONB DEFAULT '{}',
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW()
);
```

#### 细节注意
- `priority` 字段用于控制规则优先级，当规则冲突时，高优先级规则胜出
- `active` 字段支持规则的启用/禁用，不用删除就可以临时关闭某条规则
- 模板的 `variables` 存储模板变量定义（名称、类型、默认值），方便前端展示变量输入框
- 风格规则最终在生成 Prompt 时被拼入，需要设计好 Prompt 模板中风格规则的插入位置

#### 交付内容
- [ ] 风格规则表和模板表创建
- [ ] 管理接口（增删改查）完成
- [ ] 在生成输出时，能引入风格规则

---

### 阶段三验收标准
- [ ] 文档导入后，能看到自动生成的 memory_items
- [ ] 可以手动维护实体和关系
- [ ] 时间线事件自动记录
- [ ] 风格规则能影响最终输出内容
- [ ] 记忆检索接口：给定问题，能返回相关 memory_items + chunks 的混合结果

---

## 阶段四：智能编排层

### 目标
接入 Semantic Kernel，实现 Agent 编排、Kernel Memory 集成、多模块协同生成，完成从"检索系统"到"智能编排系统"的跨越。

### 时间参考
3～6 周

---

### 步骤 4.1：接入 Semantic Kernel

#### 安装依赖
```xml
<PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.*" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.Postgres" Version="1.*" />
```

#### 基础配置
```csharp
var builder = Kernel.CreateBuilder();

builder.AddOpenAIChatCompletion(
    modelId: "gpt-4o",
    apiKey: configuration["OpenAI:ApiKey"]);

builder.AddOpenAITextEmbeddingGeneration(
    modelId: "text-embedding-3-small",
    apiKey: configuration["OpenAI:ApiKey"]);

var kernel = builder.Build();
```

#### 细节注意
- Semantic Kernel 版本更新很频繁，要锁定大版本（`1.*`），不要用 `*` 通配
- LLM 和 Embedding 模型要设计成可配置、可切换，不要硬编码
  - 今天用 OpenAI，明天可能要换本地 Ollama
  - 通过配置切换，业务代码不变
- Semantic Kernel 的一些 Memory / Agent 功能标注为 `[Experimental]`，使用时要加
  ```csharp
  #pragma warning disable SKEXP0001
  ```
  并且要明确知道这些 API 未来可能变化

#### 交付内容
- [ ] Semantic Kernel 成功集成
- [ ] 能通过 Kernel 调用 LLM 完成基础对话

---

### 步骤 4.2：实现 Semantic Kernel Plugins

将记忆层能力封装为 SK Plugin，供 Agent 调用：

```csharp
public class MemoryPlugin
{
    [KernelFunction, Description("搜索相关记忆和文档片段")]
    public async Task<string> SearchMemoryAsync(
        [Description("搜索查询")] string query,
        [Description("工作空间ID")] string workspaceId,
        int topK = 5)
    { ... }

    [KernelFunction, Description("获取实体信息")]
    public async Task<string> GetEntityInfoAsync(
        [Description("实体名称")] string entityName,
        string workspaceId)
    { ... }

    [KernelFunction, Description("获取风格规则")]
    public async Task<string> GetStyleRulesAsync(
        string workspaceId)
    { ... }
}
```

#### 细节注意
- Plugin 的 `Description` 是 LLM 决定"要不要调用这个函数"的关键，描述要清晰准确
- 每个 Plugin 方法要职责单一，不要一个方法干太多事
- Plugin 内部调用数据库要注意性能：加缓存，避免 Agent 每次调用都打数据库
- Plugin 返回值要是 LLM 易于理解的格式（JSON 字符串或自然语言描述），不要返回复杂对象

#### 交付内容
- [ ] MemoryPlugin 实现
- [ ] EntityPlugin 实现
- [ ] StylePlugin 实现
- [ ] Plugin 单元测试通过

---

### 步骤 4.3：实现 Agent 框架（4 类 Agent）

#### 全局上下文校验 Agent
职责：
- 在生成内容前，校验当前上下文是否完整
- 检查关键实体是否有足够信息
- 识别上下文冲突或矛盾点

#### 实体状态跟踪 Agent
职责：
- 追踪实体在当前任务中的状态变化
- 更新 `agent_states` 表
- 当实体状态与已有记忆冲突时发出警告

#### 逻辑合规校对 Agent
职责：
- 校验生成内容是否符合设定的规则和约束
- 检查时序逻辑是否合理
- 依赖一致性检查

#### 输出对齐 Agent
职责：
- 对生成内容应用风格规则
- 格式化输出，对齐模板
- 最终质量评分

#### 细节注意
- **先从单 Agent 开始验证逻辑**，不要一开始就做 4 个 Agent 并行
- Agent 的编排顺序要有明确的依赖关系，避免循环依赖
- 每个 Agent 执行都要写入 `agent_runs` 表（输入、输出、耗时、状态）
  - 这不是可选项，是 Debug 和优化的基础
- Agent 之间的通信通过共享的 `KernelArguments` 传递，不要用全局变量
- 要设计超时和失败降级策略：某个 Agent 失败时，整体流程怎么处理

#### 交付内容
- [ ] 4 个 Agent 基础实现
- [ ] Agent 编排流程可以端到端运行
- [ ] 每次 Agent 执行有完整日志

---

### 步骤 4.4：实现实时上下文缓冲

```csharp
// Redis 存储当前会话上下文
public class ContextBuffer
{
    // Key: context:{workspaceId}:{sessionId}
    // Value: 当前上下文的 JSON 摘要
    // TTL: 根据业务设定（如 24 小时）
}
```

#### 细节注意
- 上下文缓冲存在 Redis，不是 PostgreSQL，原因：
  - 频繁读写
  - 有 TTL 需求
  - 不需要持久化（或按需持久化）
- 当上下文缓冲超过一定大小时，要做"压缩"（让 LLM 总结后替换原内容）
- 会话结束时，将重要上下文内容提取为 memory_items 写入 PostgreSQL（短期记忆 → 长期记忆）

#### 交付内容
- [ ] Redis 上下文缓冲实现
- [ ] 上下文压缩逻辑
- [ ] 会话结束时自动生成 memory_items

---

### 步骤 4.5：实现内容合成与交付层

#### 核心能力
1. 接收检索结果 + 上下文 + 风格规则
2. 构造最终 Prompt
3. 调用 LLM 生成
4. 输出对齐 Agent 做最终校验
5. 返回结构化结果

#### Prompt 构造规范
```
[系统指令]
你是 MuseSpace 的内容生成引擎...

[风格规则]
{从 StylePlugin 获取的规则列表}

[相关记忆]
{从 MemoryPlugin 检索的 memory_items}

[相关文档片段]
{从 Hybrid RAG 检索的 chunks}

[当前上下文]
{从 ContextBuffer 获取的当前状态}

[任务]
{用户的具体要求}
```

#### 细节注意
- Prompt 的各个部分都要有长度限制，防止超出 LLM 的上下文窗口
  - 总 Token 数控制在模型上下文长度的 70%，留出生成空间
- 检索结果要按相关性排序，最相关的放在靠近任务描述的位置（LLM 对末尾内容更敏感）
- 每次生成要记录：使用的 chunks、使用的 memory_items、使用的风格规则（可追溯）
- 生成结果要存到数据库（哪怕作为草稿），不要只在内存中流转

#### 交付内容
- [ ] Prompt 构造逻辑完整实现
- [ ] 端到端：上传文档 → 向量化 → 检索 → 生成，全流程通
- [ ] 生成结果有追溯链路

---

### 阶段四验收标准
- [ ] Semantic Kernel 接入，能通过 Plugin 调用记忆层
- [ ] 4 类 Agent 可以协同运行
- [ ] 给定一个任务，能完成：检索相关记忆 → 校验上下文 → 生成内容 → 输出对齐
- [ ] 所有 Agent 执行有完整日志，可追溯

---

## 阶段五（扩展）：前沿能力

### 目标
在前四个阶段稳定运行后，逐步引入 Graph RAG、复杂多 Agent 协作、长期记忆演化等前沿能力。

> ⚠️ 这些能力部分还处于技术演进中，不要在阶段一到四尚未稳定时就做这个阶段

### 时间参考
按需推进，无固定时间

---

### 步骤 5.1：Graph RAG（全局本体知识库）

#### 建设路径
1. 完善实体抽取（NER）：接入 LLM 自动从文档中抽取实体
2. 完善关系抽取：从文档中自动识别实体间关系
3. 实现图遍历检索：给定一个实体，找到其 N 跳关联实体
4. 将图遍历结果融入 Hybrid RAG

#### 细节注意
- 自动实体/关系抽取质量取决于 LLM 能力，要有人工审核纠错机制
- 图遍历不要超过 3 跳，否则结果质量快速下降、性能也下降
- pgvector + 关系表的组合可以做到一定程度的 Graph RAG，不一定非要上专用图数据库（Neo4j 等），除非图规模很大

---

### 步骤 5.2：长期记忆演化机制

- 实现记忆重要性自动衰减（访问少的记忆降低 importance）
- 实现记忆合并（语义相似的 memory_items 自动合并）
- 实现记忆蒸馏（定期将多个相关记忆提炼成更高层次的摘要）

---

### 步骤 5.3：本地大模型接入

- 接入 Ollama（本地 LLM 服务）
- 接入本地 Embedding 模型（nomic-embed-text / bge-m3）
- 实现 LLM 提供者可配置切换（云端 API / 本地模型）

---

### 步骤 5.4：动态记忆更新（企业级反馈）

- 实现用户反馈接口（对生成内容的好/差评）
- 根据反馈自动调整 memory_items 的 importance
- 实现增量索引更新（新文档进来不重建全部索引）

---

## 全局注意事项

### 代码规范
- 所有接口必须有对应的 `I` 前缀接口类，实现类可替换
- 异步方法必须用 `async/await`，禁止 `.Result` 阻塞
- LLM 调用必须有超时设置和重试逻辑
- 数据库操作必须有事务保护（涉及多表的写操作）

### 测试规范
- 每个核心 Service 要有单元测试
- 端到端流程（上传→切片→向量化→检索）要有集成测试
- LLM 相关测试要能 Mock，不要每次测试都真实调用 API

### 监控规范
- 每个 Agent 执行耗时要记录
- Embedding 生成成功率要监控
- 检索质量指标（用户反馈）要收集
- 数据库慢查询要定期检查

### 安全规范
- 工作空间隔离：所有查询必须带 `workspace_id` 过滤
- API 鉴权：所有接口必须有认证
- 敏感数据不进日志：用户内容、API Key 等不打印到日志

---

## 交付内容总览

| 阶段 | 核心交付 |
|------|---------|
| **阶段一** | 开发环境、数据库、Schema、对象存储、代码结构、Migration 规范 |
| **阶段二** | 文档上传、解析、切片、向量化、基础检索、Hybrid RAG |
| **阶段三** | 记忆单元、实体关系、时序追踪、风格规则、模板库 |
| **阶段四** | Semantic Kernel 接入、4 类 Agent、上下文缓冲、内容合成交付 |
| **阶段五** | Graph RAG、长期记忆演化、本地模型、动态反馈更新 |

---

## 技术栈成熟度速查

| 技术 | 成熟度 | 是否主流 | 备注 |
|------|--------|---------|------|
| PostgreSQL 16 | ⭐⭐⭐⭐⭐ | ✅ | 可放心作为底座 |
| pgvector | ⭐⭐⭐⭐ | ✅ | 活跃更新，主流向量方案之一 |
| ASP.NET Core | ⭐⭐⭐⭐⭐ | ✅ | 非常成熟 |
| Semantic Kernel | ⭐⭐⭐ | ✅ | 主流但仍快速演进 |
| Hybrid RAG | ⭐⭐⭐⭐ | ✅ | 当前最推荐的检索方案 |
| SK Agent Framework | ⭐⭐ | ✅前沿 | 部分 API 为实验性 |
| Graph RAG | ⭐⭐ | ✅前沿 | 成熟度低，建议后置 |
| 本地大模型（Ollama） | ⭐⭐⭐ | ✅ | 活跃，适合个人项目 |

---

*文档版本：v1.0 | 适用项目：MuseSpace | 最后更新：2026-04-19*