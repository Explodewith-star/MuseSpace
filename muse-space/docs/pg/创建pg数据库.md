# MuseSpace 数据库环境搭建笔记

这份文档记录了 MuseSpace 记忆层项目中 PostgreSQL 数据库环境的搭建流程，并补充了每一步的用途说明，方便后续复用和排障。

## 一、Linux 基础环境准备

在 Linux 中，先把配置和数据目录分开，是后续备份和迁移的基础。

### 1. 创建项目目录

```bash
mkdir -p ~/musespace/infra            # 存放配置文件，例如 docker-compose.yml
mkdir -p ~/musespace/dev-data/postgres # 存放数据库持久化数据
```

用途：实现“配置”和“数据”分离。后续备份或迁移服务器时，只需要处理 `dev-data` 目录即可。

## 二、编写 Docker 配置文件

Docker 通过 `docker-compose.yml` 来定义数据库服务的运行方式。

### 1. 创建并编辑配置文件

```bash
cd ~/musespace/infra
nano docker-compose.yml
```

### 2. 核心配置模板

```yaml
services:
  db:
    # 使用带 pgvector 扩展的官方优化镜像
    image: pgvector/pgvector:pg16
    container_name: musespace_db

    # 服务器开机后容器自动启动
    restart: always

    environment:
      POSTGRES_DB: musespace_dev
      POSTGRES_USER: msadmin
      POSTGRES_PASSWORD: your_password

    ports:
      - "6286:5432"
      # 宿主机端口:容器内部端口
      # 外部通过 6286 访问，流量会转发到容器内的 5432

    volumes:
      - ../dev-data/postgres:/var/lib/postgresql/data
      # 将容器内的数据库文件映射到宿主机目录，避免容器删除后数据丢失
```

## 三、启动与验证

### 1. 启动服务

```bash
docker compose up -d
```

`-d` 表示后台运行。

### 2. 验证容器状态

```bash
docker ps
```

### 3. 查看日志

```bash
docker logs musespace_db
```

如果 DBeaver 连不上，优先看这里是否有报错。

## 四、数据库内部初始化

连接成功后，需要在数据库中开启项目所需的扩展和 Schema。

### 1. 开启扩展

```sql
-- 开启向量存储支持，是长文本语义检索的核心能力
CREATE EXTENSION IF NOT EXISTS vector;

-- 开启三元组近似匹配，用于人名、地名等模糊搜索
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- 开启 UUID 支持，用于生成唯一的文档或实体 ID
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
```

### 2. 创建业务 Schema

```sql
-- memory 模式：存放向量片段、人物关系图谱等
CREATE SCHEMA IF NOT EXISTS memory;

-- audit 模式：存放 AI 提取日志、审核记录等
CREATE SCHEMA IF NOT EXISTS audit;
```

## 五、常见坑点

| 坑点类型 | 详细描述 | 预防措施 |
| --- | --- | --- |
| 端口映射 | 容器内固定是 5432，宿主机端口可以自定义，例如 6286。 | 记住“宿主机端口:5432”这个映射关系。 |
| 密码更新 | 修改 yml 里的密码后，如果是初始化阶段，通常需要清空 `dev-data/postgres` 才能让新密码生效。 | 开发初期可以重建数据目录，后期则使用 `ALTER USER` 修改。 |
| 云端限制 | 即使 Docker 配好了，云服务器控制台如果不放行 6286，外网也无法访问。 | 在腾讯云安全组或防火墙中放行对应端口。 |
| 内存限制 | 85 万字的长文切片会占用较多内存。 | 4G 服务器在 DBeaver 调试时不要一次性全表扫描。 |

## 六、补充说明

- 该文档偏向开发环境和排障记录，不是生产环境上线手册。
- 如果后续数据库结构发生变化，建议同步更新这份文档中的 SQL 片段和目录说明。