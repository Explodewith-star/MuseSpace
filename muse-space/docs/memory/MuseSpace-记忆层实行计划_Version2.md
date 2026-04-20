# MuseSpace 记忆层实行计划

> 基于 .NET Semantic Kernel + PostgreSQL + pgvector + Hybrid RAG + Multi-Agent + Neo4j GraphRAG
> 技术栈：Vue3 + TypeScript + ASP.NET Core + PostgreSQL 16 + pgvector + Redis + 对象存储 + Neo4j
> 文档版本：v2.0 | 最后更新：2026-04-19

---

## 修订记录

| 版本 | 日期 | 修订内容 |
|------|------|---------|
| v1.0 | 2026-04-19 | 初始版本，完整 5 阶段实行计划 |
| **v2.0** | **2026-04-19** | **新增 Neo4j + GraphRAG 集成方案；完善阶段三步骤 3.3（实体关系层引入 Neo4j 可选路径）；完善阶段四步骤 4.2（Hybrid RAG 增加图遍历第三路）；完善阶段五步骤 5.1（Graph RAG 完整落地方案）；新增三库协同架构说明；新增 Neo4j 引入决策标准** |

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
- **【v2.0 新增】Neo4j 按需引入，不强制上线，PostgreSQL 实体表是默认底座**

---

## 阶段总览

```
阶段一：基础设施搭建          → 开发环境、数据库、存储架构就绪
阶段二：知识构建管道           → 文档导入、切片、向量化、基础检索
阶段三：记忆层核心             → 记忆单元、实体关系、状态追踪、Hybrid RAG
                                 【v2.0】实体关系层新增 Neo4j 可选接入路径
阶段四：智能编排层             → Semantic Kernel 接入、Agent 框架、输出对齐
                                 【v2.0】Hybrid RAG 新增 Cypher 图遍历第三路检索
阶段五（扩展）：前沿能力       → Graph RAG、多 Agent 协作、长期记忆演化
                                 【v2.0】完整 Neo4j GraphRAG 落地方案
```

---

## 【v2.0 新增】三库协同架构总览

> 这是 v2.0 新增的全局架构说明，说明 PostgreSQL、pgvector、Neo4j 三者的分工与数据流转关系。

### 三库职责分工

```
┌─────────────────────────────────────────────────────────────────┐
│                         存储层总览                               │
│                                                                  │
│  PostgreSQL 16                                                   │
│  ─────────────────────────────────────────────────────          │
│  业务数据、文档、切片、记忆单元、风格规则、Agent记录             │
│  → 负责：结构化业务数据的事务型存储                              │
│                                                                  │
│  pgvector（PostgreSQL 扩展）                                     │
│  ─────────────────────────────────────────────────────          │
│  chunk_embeddings、memory_embeddings、entity_embeddings          │
│  → 负责：语义相似检索（"意思相近的内容"）                        │
│                                                                  │
│  Neo4j【v2.0，阶段三可选 / 阶段五必选】                          │
│  ─────────────────────────────────────────────────────          │
│  实体节点、关系边、主题概念网络、知识图谱                        │
│  → 负责：多跳关系推理（"结构上关联的实体链路"）                  │
│                                                                  │
│  Redis                                                           │
│  ─────────────────────────────────────────────────────          │
│  上下文缓冲、任务队列、热点缓存、分布式锁                        │
│  → 负责：高频读写的临时状态                                      │
└─────────────────────────────────────────────────────────────────┘
```

### 三库数据流转关系

```
文档导入
   ↓
解析 → 切片
   ↓              ↓
PostgreSQL      向量化（embedding）
document_chunks    ↓
                pgvector
                chunk_embeddings
                   ↓
            【v2.0】实体/关系抽取（LLM）
                   ↓
                Neo4j
                实体节点 + 关系边
                   ↓
         ┌─────────────────────┐
         ▼         ▼           ▼
      关键词检索  语义检索   图遍历检索
      pg_trgm   pgvector    Cypher
         │         │           │
         └────┬────┘           │
              ▼                │
           RRF 融合 ←──────────┘
              ↓
         注入 LLM Prompt
              ↓
          生成内容
```

### 【v2.0】Neo4j 引入决策标准

**先用 PostgreSQL 关系表，出现以下任一情况时再引入 Neo4j：**

| 触发条件 | 说明 |
|---------|------|
| 实体数量超过 10 万 | PostgreSQL 多跳 JOIN 开始变慢 |
| 经常做 3 跳以上关系遍历 | SQL 递归查询复杂且性能差 |
| 需要图算法 | PageRank、最短路径、社区发现等 |
| Graph RAG 成为核心检索手段 | 阶段五的主要场景 |
| 关系密度很高 | 每个实体平均关联 20+ 个其他实体 |

**性能与成本对比（基于 2025 实测数据）：**

| 指标 | Vector RAG（pgvector） | GraphRAG（Neo4j） | Hybrid 三路融合 |
|------|----------------------|------------------|----------------|
| 查询延迟 | ~300ms | ~1200ms | ~600~1500ms |
| 每次查询成本 | $0.002 | $0.012 | $0.006~0.015 |
| 复杂推理准确率 | 70% | 85%+ | 85~90%+ |
| 实现复杂度 | 低 | 高 | 中高 |
| 可解释性 | 低 | 高 | 高 |

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

**方案 B：远程托管数据库（推荐）**
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
- 开发 bucket 和生产 bucket 完全隔离

#### 交付内容
- [ ] 对象存储可用（云端或本地目录均可）
- [ ] 目录结构规范建立
- [ ] `IStorageService` 接口设计完成

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
  MuseSpace.Graph/            # 【v2.0 预留】图数据库模块（Neo4j）
  MuseSpace.Tests/            # 单元测试 / 集成测试
```

#### 细节注意
- `Core` 层不依赖任何基础设施，只有接口和模型
- `Infrastructure` 层实现具体的数据访问、存储操作
- `Memory` 模块单独拆包，是这个项目的核心
- `Graph` 模块【v2.0 新增预留】：现在只建空项目占位，阶段三或阶段五再填充内容
  - 好处：接口提前定义好，未来接入 Neo4j 时上层代码不需要改
- `Worker` 单独部署，避免长时间任务阻塞 API 进程

#### 交付内容
- [ ] 解决方案结构搭建完成，含 `MuseSpace.Graph` 空项目占位
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
- [ ] Migration 命名规范文档写入 README

---

### 阶段一验收标准
- [ ] 任意开发电脑执行 `git pull` + `dotnet ef database update` 后，可立即开始开发
- [ ] 数据库连接正常，4 个扩展已启用
- [ ] 对象存储可上传下载文件
- [ ] 项目可编译运行，API 返回健康检查响应
- [ ] `MuseSpace.Graph` 空项目占位存在，`IGraphService` 接口已定义

---

## 阶段二：知识构建管道

### 目标
实现从"原始文档上传"到"向量化完成可检索"的完整流水线，包括文档摄入、切片、向量化、存储���基础检索。

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
    source_type   VARCHAR(50) DEFAULT 'upload',
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
- `status` 字段状态流转：
  ```
  pending → processing → indexed
                      ↘ failed
  ```
- `metadata` 使用 JSONB 存储灵活元信息，不要一开始就加太多列
- `file_hash` 用于后续去重，避免同一文件重复导入
- `source_type` 要从一开始就记录，后续多源接入时会用到

#### 交付内容
- [ ] 以上表结构通过 EF Core Migration 创建
- [ ] 对应的 C# 实体类和 DbContext 配置完成

---

### 步骤 2.2：实现文档上传接口

#### API 设计
```
POST /api/documents/upload
  - 接收文件（multipart/form-data）
  - 校验文件类型
  - 计算文件 Hash（SHA256）
  - 上传到对象存储
  - 创建 documents 记录，状态设为 pending
  - 投递到处理队列
  - 返回 document_id

GET /api/documents/{id}/status
  - 查询文档处理状态
```

#### 细节注意
- 文件大小限制要在 Nginx 和 API 层都配置
- 文件类型要做双重校验：Content-Type 和文件 magic bytes
- 计算 Hash 后，先查库是否已存在相同 Hash 的文档（去重）
- 推荐：先写数据库（pending 状态），再上传对象存储，失败后台可重试

#### 交付内容
- [ ] 文���上传接口可用
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
- 解析结果保存到对象存储的 `parsed/` 目录作为缓存
- 加密 PDF 返回错误状态，扫描版 PDF 标记为 `requires_ocr`
- 解析失败时更新状态为 `failed`，记录失败原因到 `metadata.error`

#### 交付内容
- [ ] 至少支持 TXT 和 Markdown 的解析
- [ ] PDF 解析（非扫描版）
- [ ] 解析失败有明确的错误记录

---

### 步骤 2.4：实现文本切片（Chunking）模块

#### 推荐切片策略
```
chunk_size    = 512 tokens（或约 800 字符）
overlap_size  = 64 tokens（或约 100 字符）
```

#### 细节注意
- Chunk 不能太短（< 100 字符）：语义太少
- Chunk 不能太长（> 2000 字符）：语义混杂
- Overlap 保留跨 chunk 边界的上下文
- 切片策略设计成可配置

#### 交付内容
- [ ] 固定大小 + 重叠滑窗切片实现
- [ ] Chunk 写入 `document_chunks` 表
- [ ] Token/字符统计记录

---

### 步骤 2.5：建立向量表并实现 Embedding 生成

#### 建表

```sql
CREATE TABLE memory.chunk_embeddings (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chunk_id      UUID NOT NULL REFERENCES document_chunks(id) ON DELETE CASCADE,
    workspace_id  UUID NOT NULL REFERENCES workspaces(id),
    model_name    VARCHAR(200) NOT NULL,
    model_version VARCHAR(100),
    embedding     vector(1536),
    created_at    TIMESTAMPTZ DEFAULT NOW()
);

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

#### 细节注意
- Embedding 生成是异步后台任务
- 每个 Chunk 生成后立即写入，不要批量攒完再写
- 要记录 `model_name`，换模型时旧 embedding 要重新生成
- HNSW 参数：`m=16, ef_construction=64` 是初期合理默认值

#### 交付内容
- [ ] `chunk_embeddings` 表和 HNSW 索引创建
- [ ] Embedding 生成 Worker 实现
- [ ] 向量成功写入并可查询

---

### 步骤 2.6：实现基础向量检索

```sql
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
- `<=>` 是余弦距离运算符，`1 - 距离` 转为相似度
- 必须加 `workspace_id` 过滤，防止跨工作空间数据泄露
- 检索时传入正确的 `model_name`

#### 交付内容
- [ ] 基础向量检索接口可用
- [ ] 给定一段文本，能返回最相关的 chunk 列表

---

### 步骤 2.7：实现 Hybrid RAG（混合检索）

#### 混合检索策略

```sql
-- 关键词检索
SELECT id, content, similarity(content, $query) AS text_score
FROM document_chunks
WHERE workspace_id = $workspace_id
  AND content % $query
ORDER BY text_score DESC
LIMIT 20;
```

应用层 RRF 融合：
```
RRF_score(doc) = Σ 1 / (k + rank_i)
k = 60（常用值）
```

#### 细节注意
- 向量检索擅长语义理解，关键词检索擅长精确匹配，两者互补
- RRF 融合不需要对两路分数做归一化
- 融合后去重，最终返回给 LLM 的 chunk 建议 5～10 个

#### 交付内容
- [ ] 关键词检索实现
- [ ] RRF 融合逻辑实现
- [ ] Hybrid RAG 接口可用

---

### 阶段二验收标准
- [ ] 上传一个 PDF/TXT 文档，系统能自动完成解析→切片→向量化
- [ ] 给定一个自然语言问题，能返回最相关的文档片段
- [ ] Hybrid RAG 检索结果比纯向量检索有明显改善

---

## 阶段三：记忆层核心

### 目标
在文档检索的基础上，建立持久化的记忆单元、实体关系、时序状态追踪，形成真正的"记忆层"。

> **【v2.0】本阶段新增步骤 3.3-B：Neo4j 实体关系可选接入路径。**
> 默认仍使用 PostgreSQL 关系表（步骤 3.3-A），当达到引入标准时迁移到 Neo4j。

### 时间参考
2～4 周

---

### 步骤 3.1：建立记忆单元表

```sql
CREATE TABLE memory.memory_items (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id),
    source_type   VARCHAR(50) NOT NULL,
    source_id     UUID,
    content       TEXT NOT NULL,
    summary       TEXT,
    importance    FLOAT DEFAULT 0.5 CHECK (importance >= 0 AND importance <= 1),
    access_count  INT DEFAULT 0,
    last_accessed TIMESTAMPTZ,
    expires_at    TIMESTAMPTZ,
    metadata      JSONB DEFAULT '{}',
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW()
);

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
- `importance` 影响检索优先级，可以根据访问频率自动更新
- `access_count` 和 `last_accessed` 用于实现记忆衰减逻辑
- `expires_at` 用于短期记忆（对话上下文）和长期记忆分层
- `source_type` 从一开始设计清楚，方便按来源过滤

#### 交付内容
- [ ] 以上表结构通过 Migration 创建
- [ ] 基础 CRUD 接口完成

---

### 步骤 3.2：实现自动记忆提取

从文档 Chunk 提取记忆单元：

1. 按 chunk 重要性评分（长度 + 关键词密度 + 语义多样性）
2. 对高分 chunk 生成摘要（调用 LLM）
3. 将摘要作为 memory_item 存入，source_type = `chunk`

#### 细节注意
- 不是每个 chunk 都要变成 memory_item，避免记忆库膨胀
- 摘要生成有成本，要控制生成频率
- 初期可以简化：直接把重要 chunk 内容复制为 memory_item

#### 交付内容
- [ ] 文档处理完成后，自动生成若干 memory_items
- [ ] memory_items 有对应的向量

---

### 步骤 3.3-A：实体关系层（PostgreSQL 默认方案）

> **默认采用此方案。当达到 Neo4j 引入标准时，执行步骤 3.3-B 进行迁移。**

```sql
CREATE TABLE memory.entities (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id),
    name          VARCHAR(500) NOT NULL,
    entity_type   VARCHAR(100),
    description   TEXT,
    aliases       TEXT[],
    attributes    JSONB DEFAULT '{}',
    created_at    TIMESTAMPTZ DEFAULT NOW(),
    updated_at    TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE (workspace_id, name, entity_type)
);

CREATE TABLE memory.entity_embeddings (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_id    UUID NOT NULL REFERENCES memory.entities(id) ON DELETE CASCADE,
    workspace_id UUID NOT NULL,
    model_name   VARCHAR(200) NOT NULL,
    embedding    vector(1536),
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE memory.entity_relations (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id   UUID NOT NULL,
    from_entity_id UUID NOT NULL REFERENCES memory.entities(id),
    to_entity_id   UUID NOT NULL REFERENCES memory.entities(id),
    relation_type  VARCHAR(200) NOT NULL,
    description    TEXT,
    weight         FLOAT DEFAULT 1.0,
    source_chunk   UUID REFERENCES document_chunks(id),
    created_at     TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT no_self_relation CHECK (from_entity_id != to_entity_id)
);
```

#### 细节注意
- `aliases` 数组用于存同义词/别名
- `UNIQUE (workspace_id, name, entity_type)` 防止实体重复录入
- `source_chunk` 记录关系来自哪个 chunk，可追溯来源
- 关系类型要有预定义列表，避免自由文本导致查不到

#### 接口抽象（为未来迁移 Neo4j 做准备）

```csharp
// Core 层定义接口，不绑定具体实现
public interface IEntityGraphService
{
    Task<Entity> UpsertEntityAsync(Entity entity);
    Task UpsertRelationAsync(EntityRelation relation);
    Task<IEnumerable<Entity>> FindRelatedEntitiesAsync(
        string entityName,
        string workspaceId,
        int maxHops = 2);
    Task<IEnumerable<EntityRelation>> GetEntityRelationsAsync(
        Guid entityId);
}

// Infrastructure 层：PostgreSQL 实现
public class PostgresEntityGraphService : IEntityGraphService { ... }

// Graph 层预留：Neo4j 实现（阶段三-B 或阶段五填充）
public class Neo4jEntityGraphService : IEntityGraphService { ... }
```

#### 细节注意
- **接口抽象是关键**：通过 `IEntityGraphService` 隔离，上层业务代码调用接口而非具体实现
- 这样从 PostgreSQL 切换到 Neo4j 时，只需要：
  1. 实现 `Neo4jEntityGraphService`
  2. 修改 DI 注册
  3. 上层代码零改动
- 现在注册 PostgreSQL 实现，以后换 Neo4j 只改一行注册代码

#### 交付内容
- [ ] 实体和关系表创建（PostgreSQL）
- [ ] `IEntityGraphService` 接口定义
- [ ] `PostgresEntityGraphService` 实现
- [ ] `Neo4jEntityGraphService` 空类占位（返回 NotImplementedException）
- [ ] 可以手动录入实体和关系（API 接口）
- [ ] 实体向量生成

---

### 步骤 3.3-B：【v2.0 新增】实体关系层迁移到 Neo4j（按需执行）

> **⚠️ 此步骤不是必须立即执行的。当满足"Neo4j 引入决策标准"时才执行。**
> 执行本步骤前，步骤 3.3-A 必须已完成且稳定运行。

#### Neo4j 本地部署（Docker）

```yaml
# 加入 infra/docker-compose.yml
services:
  neo4j:
    image: neo4j:5.18-community
    container_name: musespace_neo4j
    restart: unless-stopped
    environment:
      NEO4J_AUTH: neo4j/dev_password_change_me
      NEO4J_PLUGINS: '["apoc", "graph-data-science"]'
      NEO4J_dbms_memory_heap_initial__size: 512m
      NEO4J_dbms_memory_heap_max__size: 1G
    ports:
      - "7474:7474"   # HTTP（浏览器管理界面）
      - "7687:7687"   # Bolt（应用连接）
    volumes:
      - musespace_neo4j_data:/data
      - musespace_neo4j_logs:/logs

volumes:
  musespace_neo4j_data:
  musespace_neo4j_logs:
```

#### 安装 .NET 驱动

```xml
<PackageReference Include="Neo4j.Driver" Version="5.*" />
```

#### Neo4j 图数据模型设计

```cypher
// 节点类型
(:Entity {
    id: UUID,
    workspace_id: UUID,
    name: String,
    entity_type: String,     // person / place / concept / event / product
    description: String,
    aliases: [String],
    created_at: DateTime
})

(:MemoryItem {
    id: UUID,
    workspace_id: UUID,
    content: String,
    summary: String,
    importance: Float,
    source_type: String
})

(:Document {
    id: UUID,
    workspace_id: UUID,
    title: String
})

(:Chunk {
    id: UUID,
    document_id: UUID,
    chunk_index: Int,
    content_preview: String   // 只存预览，完整内容仍在 PostgreSQL
})
```

```cypher
// 关系类型（预定义）
[:RELATES_TO]               // 通用关联
[:BELONGS_TO]               // 从属关系
[:DEPENDS_ON]               // 依赖关系
[:MENTIONED_IN]             // 实体在文档/chunk 中被提及
[:DERIVED_FROM]             // 记忆来源于
[:CONFLICTS_WITH]           // 冲突关系
[:SIMILAR_TO]               // 语义相似（可用于记忆合并）
```

#### Neo4j 实现 `IEntityGraphService`

```csharp
public class Neo4jEntityGraphService : IEntityGraphService
{
    private readonly IDriver _driver;

    public Neo4jEntityGraphService(IDriver driver)
    {
        _driver = driver;
    }

    public async Task<Entity> UpsertEntityAsync(Entity entity)
    {
        await using var session = _driver.AsyncSession();
        await session.RunAsync(@"
            MERGE (e:Entity {id: $id, workspace_id: $workspaceId})
            SET e.name = $name,
                e.entity_type = $entityType,
                e.description = $description,
                e.aliases = $aliases,
                e.updated_at = datetime()
            RETURN e",
            new {
                id = entity.Id.ToString(),
                workspaceId = entity.WorkspaceId.ToString(),
                name = entity.Name,
                entityType = entity.EntityType,
                description = entity.Description,
                aliases = entity.Aliases
            });
        return entity;
    }

    public async Task<IEnumerable<Entity>> FindRelatedEntitiesAsync(
        string entityName,
        string workspaceId,
        int maxHops = 2)
    {
        await using var session = _driver.AsyncSession();
        // Cypher 多跳遍历
        var result = await session.RunAsync(@"
            MATCH (start:Entity {name: $name, workspace_id: $workspaceId})
            MATCH (start)-[*1..$maxHops]-(related:Entity)
            WHERE related.workspace_id = $workspaceId
            RETURN DISTINCT related.id AS id,
                            related.name AS name,
                            related.entity_type AS entityType,
                            related.description AS description
            LIMIT 30",
            new { name = entityName, workspaceId, maxHops });

        var records = await result.ToListAsync();
        return records.Select(r => new Entity
        {
            Id = Guid.Parse(r["id"].As<string>()),
            Name = r["name"].As<string>(),
            EntityType = r["entityType"].As<string>(),
            Description = r["description"].As<string>()
        });
    }
}
```

#### 数据迁移：从 PostgreSQL 迁移到 Neo4j

```csharp
// 迁移服务：将 PostgreSQL 中的实体关系数据同步到 Neo4j
public class EntityGraphMigrationService
{
    public async Task MigrateAsync()
    {
        // 1. 读取 PostgreSQL 中所有实体
        var entities = await _postgresRepo.GetAllEntitiesAsync();

        // 2. 批量写入 Neo4j
        foreach (var batch in entities.Chunk(500))
        {
            await _neo4jService.BulkUpsertEntitiesAsync(batch);
        }

        // 3. 读取所有关系并写入
        var relations = await _postgresRepo.GetAllRelationsAsync();
        foreach (var batch in relations.Chunk(500))
        {
            await _neo4jService.BulkUpsertRelationsAsync(batch);
        }

        // 4. 可选：保留 PostgreSQL 实体表作为备份
        //    修改 DI 注册，将 IEntityGraphService 切换为 Neo4jEntityGraphService
    }
}
```

#### 双写过渡策略（推荐）

迁移时不要一次性切换，而是采用**双写策略**：

```csharp
public class DualWriteEntityGraphService : IEntityGraphService
{
    // 同时写入 PostgreSQL 和 Neo4j
    // 读取优先从 Neo4j 读（验证质量）
    // 确认 Neo4j 质量稳定后，停止写入 PostgreSQL 实体表
}
```

#### 细节注意
- Neo4j 中的节点只存**关键字段和检索字段**，完整业务数据仍在 PostgreSQL
  - 原因：Neo4j 不擅长存大文本，也没有 PostgreSQL 的事务保障
  - 典型做法：Neo4j 存 id、name、type、description；完整数据通过 id 回查 PostgreSQL
- `workspace_id` 必须作为所有节点的属性，查询时必须带上，防止跨空间数据泄露
- 关系类型要用预定义常量，不允许自由文本
- 自动实体抽取的质量依赖 LLM，初期要有人工审核机制
- Neo4j Community 版足够个人/小团队使用；Enterprise 版有集群和细粒度权限控制
- APOC 插件提供很多实用的图操作函数，建议安装

#### 交付内容
- [ ] Neo4j 容器运行正常，浏览器管理界面可访问（http://localhost:7474）
- [ ] `Neo4jEntityGraphService` 实现完成
- [ ] DI 注册切换到 Neo4j 实现（或双写模式）
- [ ] PostgreSQL 数据成功迁移到 Neo4j
- [ ] 多跳查询测试通过（给定一个实体名，能返回 2 跳内的关联实体）

---

### 步骤 3.4：建立时序状态追踪

```sql
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
- `timeline_events` 是不可变事件流，只写入不修改
- `agent_states` 用乐观锁（version 字段）防并发问题
- `event_time` 是事件实际时间，`created_at` 是入库时间，两者分开

#### 交付内容
- [ ] 时间线事件表创建
- [ ] 重要操作自动记录 timeline

---

### 步骤 3.5：实现风格规则与输出模板库

```sql
CREATE TABLE memory.style_rules (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL,
    rule_type    VARCHAR(100),
    content      TEXT NOT NULL,
    priority     INT DEFAULT 0,
    active       BOOLEAN DEFAULT TRUE,
    created_at   TIMESTAMPTZ DEFAULT NOW()
);

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
- `priority` 用于控制规则优先级，冲突时高优先级胜出
- `active` 支持规则启用/禁用，不需要删除
- 风格规则最终在生成 Prompt 时拼入，需要设计好插入位置

#### 交付内容
- [ ] 风格规则表和模板表创建
- [ ] 管理接口完成
- [ ] 在生成输出时，能引入风格规则

---

### 阶段三验收标准
- [ ] 文档导入后，能看到自动生成的 memory_items
- [ ] 实体和关系可以维护（API 或手动）
- [ ] 时间线事件自动记录
- [ ] 风格规则能影响最终输出
- [ ] 记忆检索接口：给定问题，能返回相关 memory_items + chunks 的混合结果
- [ ] `IEntityGraphService` 接口已定义，PostgreSQL 实现运行正常
- [ ] 【v2.0 可选】如已执行 3.3-B：Neo4j 多跳查询测试通过

---

## 阶段四：智能编排层

### 目标
接入 Semantic Kernel，实现 Agent 编排、Kernel Memory 集成、多模块协同生成。

> **【v2.0】步骤 4.2 新增图遍历作为 Hybrid RAG 的第三路检索。**

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
- Semantic Kernel 版本更新快，锁定大版本（`1.*`）
- LLM 和 Embedding 模型设计成可配置可切换
- 标注 `[Experimental]` 的 API 要加 `#pragma warning disable`，并明确知道可能变化

#### 交付内容
- [ ] Semantic Kernel 成功集成
- [ ] 能通过 Kernel 调用 LLM 完成基础对话

---

### 步骤 4.2：【v2.0 更新】实现 Semantic Kernel Plugins（含图检索）

将记忆层能力封装为 SK Plugin，v2.0 新增 `GraphPlugin`：

```csharp
public class MemoryPlugin
{
    [KernelFunction, Description("搜索相关记忆和文档片段")]
    public async Task<string> SearchMemoryAsync(
        [Description("搜索查询")] string query,
        [Description("工作空间ID")] string workspaceId,
        int topK = 5)
    { ... }
}

public class StylePlugin
{
    [KernelFunction, Description("获取当前工作空间的风格规则")]
    public async Task<string> GetStyleRulesAsync(
        string workspaceId)
    { ... }
}

// 【v2.0 新增】
public class GraphPlugin
{
    private readonly IEntityGraphService _graphService;
    // 通过接口注入，底层可以是 PostgreSQL 或 Neo4j
    // 无需修改 Plugin 代码

    [KernelFunction, Description("通过知识图谱查找与指定实体相关联的实体和关系")]
    public async Task<string> FindRelatedEntitiesAsync(
        [Description("起始实体名称")] string entityName,
        [Description("工作空间ID")] string workspaceId,
        [Description("最大关联跳数，建议 1~3")] int maxHops = 2)
    {
        var related = await _graphService.FindRelatedEntitiesAsync(
            entityName, workspaceId, maxHops);

        if (!related.Any())
            return $"未找到与"{entityName}"相关的实体。";

        var sb = new StringBuilder();
        sb.AppendLine($"与"{entityName}"相关联的实体（{maxHops}跳以内）：");
        foreach (var entity in related)
        {
            sb.AppendLine($"- [{entity.EntityType}] {entity.Name}: {entity.Description}");
        }
        return sb.ToString();
    }

    [KernelFunction, Description("获取实体的直接关系列表")]
    public async Task<string> GetEntityRelationsAsync(
        [Description("实体ID")] string entityId,
        string workspaceId)
    {
        var relations = await _graphService.GetEntityRelationsAsync(
            Guid.Parse(entityId));

        return JsonSerializer.Serialize(relations.Select(r => new {
            from = r.FromEntityName,
            relation = r.RelationType,
            to = r.ToEntityName,
            weight = r.Weight
        }));
    }
}
```

#### 【v2.0 新增】三路 Hybrid RAG 融合

在阶段二的两路融合基础上，加入图遍历作为第三路：

```csharp
public class HybridRagService
{
    public async Task<IEnumerable<RetrievalResult>> RetrieveAsync(
        string query,
        string workspaceId,
        int topK = 10)
    {
        // 第一路：向量语义检索（pgvector）
        var vectorResults = await _vectorSearch.SearchAsync(
            query, workspaceId, topK: 20);

        // 第二路：关键词检索（pg_trgm）
        var keywordResults = await _keywordSearch.SearchAsync(
            query, workspaceId, topK: 20);

        // 【v2.0】第三路：图遍历检索（IEntityGraphService）
        // 先从查询中提取关键实体
        var entities = await _entityExtractor.ExtractEntitiesAsync(query);
        var graphResults = new List<RetrievalResult>();
        foreach (var entity in entities.Take(3))  // 最多取 3 个实体做图遍历
        {
            var related = await _graphService.FindRelatedEntitiesAsync(
                entity, workspaceId, maxHops: 2);
            // 将图遍历结果转换为检索结果格式
            graphResults.AddRange(related.Select(e => new RetrievalResult
            {
                Content = $"[图关联] {e.Name}（{e.EntityType}）: {e.Description}",
                Source = RetrievalSource.Graph,
                Score = 0.7f  // 图遍历结果给固定基础分
            }));
        }

        // RRF 三路融合
        return RrfFusion(vectorResults, keywordResults, graphResults, k: 60)
            .Take(topK);
    }

    private IEnumerable<RetrievalResult> RrfFusion(
        IEnumerable<RetrievalResult> vector,
        IEnumerable<RetrievalResult> keyword,
        IEnumerable<RetrievalResult> graph,
        int k = 60)
    {
        // 对每路结果按排名打分，再合并去重
        var scores = new Dictionary<string, float>();

        void AddScores(IEnumerable<RetrievalResult> results)
        {
            int rank = 1;
            foreach (var r in results)
            {
                var key = r.UniqueKey;
                scores.TryAdd(key, 0);
                scores[key] += 1.0f / (k + rank++);
            }
        }

        AddScores(vector);
        AddScores(keyword);
        AddScores(graph);  // 【v2.0】图遍历路

        return scores
            .OrderByDescending(kv => kv.Value)
            .Select(kv => GetResultByKey(kv.Key));
    }
}
```

#### 细节注意
- `GraphPlugin` 通过 `IEntityGraphService` 接口注入，**底层是 PostgreSQL 还是 Neo4j 对 Plugin 完全透明**
- 图遍历结果的分数计算策略与向量/关键词不同（固定基础分 + 关系权重调整），需要根据实际效果调整
- 图检索在 Neo4j 尚未接入时，`PostgresEntityGraphService` 的多跳查询能力有限（2跳以内勉强可用）；接入 Neo4j 后效果明显提升
- 从查询中提取实体这一步本身也是 LLM 调用，有成本；初期可以简化为关键词匹配

#### 交付内容
- [ ] `GraphPlugin` 实现（通过 `IEntityGraphService` 接口）
- [ ] `MemoryPlugin` 实现
- [ ] `StylePlugin` 实现
- [ ] 三路 Hybrid RAG 融合实现
- [ ] Plugin 单元测试通过

---

### 步骤 4.3：实现 Agent 框架（4 类 Agent）

#### 全局上下文校验 Agent
- 校验当前上下文是否完整
- 检查关键实体是否有足够信息
- 【v2.0】可调用 `GraphPlugin` 验证实体间关系是否一致

#### 实体状态跟踪 Agent
- 追踪实体在当前任务中的状态变化
- 更新 `agent_states` 表
- 【v2.0】当检测到实体状态与图谱中已有关系冲突时发出警告

#### 逻辑合规校对 Agent
- 校验生成内容是否符合规则和约束
- 检查时序逻辑
- 依赖一致性检查

#### 输出对齐 Agent
- 对生成内容应用风格规则
- 格式化输出，对齐模板
- 最终质量评分

#### 细节注意
- 先从单 Agent 开始验证逻辑，不要一开始做 4 个并行
- 每个 Agent 执行都要写入 `agent_runs` 表
- 设计超时和失败降级策略
- Agent 之间通过共享 `KernelArguments` 传递，不用全局变量

#### 交付内容
- [ ] 4 个 Agent 基础实现
- [ ] Agent 编排流程端到端可运行
- [ ] 每次 Agent 执行有完整日志

---

### 步骤 4.4：实现实时上下文缓冲

```csharp
public class ContextBuffer
{
    // Key: context:{workspaceId}:{sessionId}
    // Value: 当前上下文的 JSON 摘要
    // TTL: 根据业务设定（如 24 小时）
}
```

#### 细节注意
- 上下文缓冲存 Redis，不存 PostgreSQL（高频读写、有 TTL 需求）
- 上下文缓冲超过大小时，让 LLM 总结后替换原内容
- 会话结束时，将重要上下文提取为 memory_items（短期→长期）

#### 交付内容
- [ ] Redis 上下文缓冲实现
- [ ] 上下文压缩逻辑
- [ ] 会话结束时自动生成 memory_items

---

### 步骤 4.5：实现内容合成与交付层

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

[【v2.0】知识图谱关联实体]
{从 GraphPlugin 获取的相关实体和关系}

[当前上下文]
{从 ContextBuffer 获取的当前状态}

[任务]
{用户的具体要求}
```

#### 细节注意
- 总 Token 数控制在模型上下文长度的 70%，留出生成空间
- 最相关的内容放靠近任务描述的位置（LLM 对末尾内容更敏感）
- 【v2.0】图谱关联实体部分要控制长度，避免稀释其他检索结果
- 每次生成要记录使用的 chunks、memory_items、风格规则（可追溯）

#### 交付内容
- [ ] Prompt 构造逻辑完整实现（含图谱关联实体部分）
- [ ] 端到端全流程通
- [ ] 生成结果有追溯道路

---

### 阶段四验收标准
- [ ] Semantic Kernel 接入，能通过 Plugin 调用记忆层
- [ ] 4 类 Agent 可以协同运行
- [ ] 三路 Hybrid RAG 融合可用（向量 + 关键词 + 图遍历）
- [ ] 全流程：检索相关记忆 → 校验上下文 → 生成内容 → 输出对齐
- [ ] 所有 Agent 执行有完整日志，可追溯

---

## 阶段五（扩展）：前沿能力

### 目标
在前四个阶段稳定运行后，逐步引入完整 Graph RAG、复杂多 Agent 协作、长期记忆演化等前沿能力。

> ⚠️ 这些能力部分还处于技术演进中，不要在阶段一到四尚未稳定时就做这个阶段。
>
> **【v2.0】步骤 5.1 更新为完整 Neo4j GraphRAG 落地方案。**

### 时间参考
按需推进，无固定时间

---

### 步骤 5.1：【v2.0 完整更新】Graph RAG 完整落地（Neo4j 主战场）

> 如果步骤 3.3-B 已执行，Neo4j 已接入，本步骤在此基础上做深度扩展。
> 如果步骤 3.3-B 未执行，本步骤包含从头引入 Neo4j 的完整工作。

#### 5.1.1 自动实体与关系抽取管道

在文档处理流水线中增加实体抽取步骤：

```csharp
public class EntityExtractionWorker
{
    // 文档切片完成后，自动触发实体抽取
    public async Task ExtractAndStoreAsync(DocumentChunk chunk)
    {
        // 调用 LLM 进行 NER（命名实体识别）+ 关系抽取
        var prompt = $"""
            从以下文本中提取实体和关系，以 JSON 格式返回。
            
            实体类型：person（人物）、place（地点）、concept（概念）、
                     event（事件）、product（产品/作品）、organization（组织）
            
            输出格式：
            {{
              "entities": [
                {{"name": "实体名", "type": "实体类型", "description": "简短描述"}}
              ],
              "relations": [
                {{"from": "实体A", "relation": "关系类型", "to": "实体B"}}
              ]
            }}
            
            文本：
            {chunk.Content}
            """;

        var result = await _kernel.InvokePromptAsync(prompt);

        // 解析并写入图谱
        var extracted = JsonSerializer.Deserialize<ExtractionResult>(result.ToString());

        foreach (var entity in extracted.Entities)
        {
            await _graphService.UpsertEntityAsync(new Entity
            {
                WorkspaceId = chunk.WorkspaceId,
                Name = entity.Name,
                EntityType = entity.Type,
                Description = entity.Description
            });
        }

        foreach (var relation in extracted.Relations)
        {
            await _graphService.UpsertRelationAsync(new EntityRelation
            {
                WorkspaceId = chunk.WorkspaceId,
                FromEntityName = relation.From,
                ToEntityName = relation.To,
                RelationType = relation.Relation,
                SourceChunkId = chunk.Id
            });
        }
    }
}
```

#### 细节注意
- 自动抽取有一定错误率，要有**人工审核队列**：抽取结果先进审核队列，可以选择自动通过或人工确认
- 实体去重是难点：同一个实体可能有多种写法（"鲁迅" vs "周树人"），需要实体消歧逻辑
  - 初期方案：完全匹配 + aliases 匹配
  - 进阶方案：向量相似度判断是否为同一实体
- 关系类型要严格控制在预定义列表中，LLM 有时会生成不在列表中的关系类型，要做后处理归一化
- 大批量文档抽取时，LLM API 成本会很高，要做以下控制：
  - 只对重要度高的 chunk 做抽取
  - 批量处理，不要每个 chunk 单独调用
  - 设置每日抽取配额

#### 5.1.2 Neo4j 图算法（GDS 插件）

安装 Graph Data Science（GDS）插件后，可以使用图算法：

```cypher
// PageRank：找到知识图谱中最重要的实体
CALL gds.pageRank.stream('entity-graph')
YIELD nodeId, score
RETURN gds.util.asNode(nodeId).name AS name, score
ORDER BY score DESC LIMIT 20;

// 社区发现：识别主题聚类
CALL gds.louvain.stream('entity-graph')
YIELD nodeId, communityId
RETURN communityId,
       collect(gds.util.asNode(nodeId).name) AS entities
ORDER BY size(entities) DESC;

// 最短路径：找两个实体间的关联路径
MATCH (a:Entity {name: 'EntityA', workspace_id: $wsId}),
      (b:Entity {name: 'EntityB', workspace_id: $wsId})
CALL gds.shortestPath.dijkstra.stream(
    'entity-graph',
    {sourceNode: a, targetNode: b})
YIELD path
RETURN [node in nodes(path) | node.name] AS pathNodes;
```

#### 应用场景
- **PageRank**：识别知识库中的"核心概念"，优先展示给用户
- **社区发现**：自动发现主题聚类，生成知识图谱的主题导航
- **最短路径**：回答"A 和 B 是如何关联的？"这类问题

#### 5.1.3 完整 GraphRAG 检索流程

```
用户查询
   ↓
实体识别（从查询中提取关键实体）
   ↓
图遍历（Neo4j Cypher，1~3跳）
   ↓
获取子图（相关实体 + 关系链路）
   ↓                ↓
向量检索（pgvector） 关键词检索（pg_trgm）
   ↓                ↓
         RRF 三路融合
              ↓
      构建结构化上下文
      ├── 相关文档片段（chunks）
      ├── 相关记忆单元（memory_items）
      └── 实体关系图谱摘要（graph context）
              ↓
         注入 LLM Prompt
              ↓
          生成最终内容
```

#### 交付内容
- [ ] 自动实体/关系抽取管道运行
- [ ] 人工审核队列界面（哪怕是简单的管理后台）
- [ ] Neo4j GDS 图算法可调用
- [ ] PageRank 结果可以影响检索排序
- [ ] 完整 GraphRAG 流程端到端测试通过

---

### 步骤 5.2：长期记忆演化机制

- 实现记忆重要性自动衰减（访问少的记忆降低 importance）
- 实现记忆合并（语义相似的 memory_items 自动合并）
  - 【v2.0】借助 Neo4j `SIMILAR_TO` 关系标记相似记忆，辅助合并决策
- 实现记忆蒸馏（定期将多个相关记忆提炼成更高层次的摘要）

---

### 步骤 5.3：本地大模型接入

- 接入 Ollama（本地 LLM 服务）
- 接入本地 Embedding 模型（nomic-embed-text / bge-m3）
- 实现 LLM 提供者可配置切换（���端 API / 本地模型）
- 【v2.0】本地模型也可用于实体抽取，降低实体/关系抽取的 API 成本

---

### 步骤 5.4：动态记忆更新（企业级反馈）

- 实现用户反馈接口（对生成内容的好/差评）
- 根据反馈自动调整 memory_items 的 importance
- 实现增量索引更新
- 【v2.0】用户反馈可以触发图谱更新：确认或否定某个实体关系

---

## 全局注意事项

### 代码规范
- 所有接口必须有 `I` 前缀接口类，实现类可替换
- 异步方法必须用 `async/await`，禁止 `.Result` 阻塞
- LLM 调用必须有超时设置和重试逻辑
- 数据库操作必须有事务保护

### 测试规范
- 每个核心 Service 要有单元测试
- 端到端流程要有集成测试
- LLM 相关测试要能 Mock
- 【v2.0】`IEntityGraphService` 接口实现（PostgreSQL 和 Neo4j）都要有单元测试

### 监控规范
- 每个 Agent 执行耗时要记录
- Embedding 生成成功率要监控
- 检索质量指标要收集
- 【v2.0】图遍历查询耗时要单独监控（区分 PostgreSQL 实现和 Neo4j 实现）
- 数据库慢查询要定期检查

### 安全规范
- 工作空间隔离：所有查询必须带 `workspace_id` 过滤
  - 【v2.0】包括 Neo4j 查询，节点属性中必须有 `workspace_id` 且查询时必须过滤
- API 鉴权：所有接口必须有认证
- 敏感数据不进日志

---

## 交付内容总览

| 阶段 | 核心交付 |
|------|---------|
| **阶段一** | 开发环境、数据库、Schema、对象存储、代码结构（含 Graph 模块占位）、Migration 规范 |
| **阶段二** | 文档上传、解析、切片、向量化、基础检索、Hybrid RAG（两路） |
| **阶段三** | 记忆单元、实体关系（PostgreSQL 默认 + Neo4j 可选）、时序追踪、风格规则、模板库 |
| **阶段四** | Semantic Kernel 接入、GraphPlugin、三路 Hybrid RAG、4 类 Agent、上下文缓冲、内容合成 |
| **阶段五** | 完整 GraphRAG 落地、自动实体抽取、图算法、长期记忆演化、本地模型、动态反馈更新 |

---

## 技术栈成熟度速查

| 技术 | 成熟度 | 是否主流 | 引入时机 | 备注 |
|------|--------|---------|---------|------|
| PostgreSQL 16 | ⭐⭐⭐⭐⭐ | ✅ | 阶段一 | 可放心作为底座 |
| pgvector | ⭐⭐⭐⭐ | ✅ | 阶段二 | 活跃更新，主流向量方案 |
| ASP.NET Core | ⭐⭐⭐⭐⭐ | ✅ | 阶段一 | 非常成熟 |
| Redis | ⭐⭐⭐⭐⭐ | ✅ | 阶段四 | 非常成熟 |
| Semantic Kernel | ⭐⭐⭐ | ✅ | 阶段四 | 主流但仍快速演进 |
| Hybrid RAG（两路） | ⭐⭐⭐⭐ | ✅ | 阶段二 | 当前最推荐的检索方案 |
| **Neo4j** | **⭐⭐⭐⭐** | **✅** | **阶段三-B 或阶段五** | **【v2.0 新增】按需引入** |
| **GraphRAG（三路融合）** | **⭐⭐⭐** | **✅前沿** | **阶段四~五** | **【v2.0 新增】效果好但成本高** |
| SK Agent Framework | ⭐⭐ | ✅前沿 | 阶段四 | 部分 API 为实验性 |
| 图算法（GDS） | ⭐⭐⭐ | ✅ | 阶段五 | 需要 Neo4j GDS 插件 |
| 本地大模型（Ollama） | ⭐⭐⭐ | ✅ | 阶段五 | 活跃，适合个人项目 |

---

## 【v2.0 新增】Neo4j 快速决策树

```
你的实体关系表数据量大了吗？
├── 否 → 继续用 PostgreSQL 关系表（步骤 3.3-A 已够）
└── 是 → 多跳查询（>2跳）变慢了吗？
         ├── 否 → 继续用 PostgreSQL，优化索引
         └── 是 → 需要图算法（PageRank等）吗？
                  ├── 否 → 评估是否值得引入 Neo4j 的运维成本
                  └── 是 → 引入 Neo4j（执行步骤 3.3-B）
                            因为 IEntityGraphService 接口已就绪
                            切换成本很低
```

---

*文档版本：v2.0 | 适用项目：MuseSpace | 最后更新：2026-04-19*
*v2.0 修订：新增 Neo4j + GraphRAG 完整集成方案，涉及阶段一步骤 1.5、阶段三步骤 3.3-B、阶段四步骤 4.2、阶段五步骤 5.1*