# EF Core 迁移使用规范

> 本规范约束项目中所有对数据库结构的修改行为，AI 辅助开发时同样严格遵守。

---

## 一、核心原则

1. **迁移文件是数据库的"变更历史账本"**，必须完整、可重放、可回滚。
2. **已应用到任何环境（开发机/服务器）的迁移文件绝对禁止修改**。
3. **绝不绕过 EF 直接在数据库建表或加列**——所有结构变更必须通过迁移文件落地。
4. **快照文件（`MuseSpaceDbContextModelSnapshot.cs`）由工具自动维护，人工不得直接编辑**。

---

## 二、标准工作流

每次修改实体（新建/修改/删除属性或导航属性），必须完整执行以下三步：

```
Step 1  修改 Domain 实体
Step 2  生成迁移文件
Step 3  应用到数据库
```

### 完整命令（在 muse-space/ 目录下执行）

```bash
# Step 2 - 生成迁移
dotnet ef migrations add <MigrationName> \
  --project src/MuseSpace.Infrastructure \
  --startup-project src/MuseSpace.Api \
  --output-dir Migrations

# Step 3 - 应用迁移
dotnet ef database update \
  --project src/MuseSpace.Infrastructure \
  --startup-project src/MuseSpace.Api
```

> **Step 2 和 Step 3 必须在同一次操作中完成，不允许只生成不应用。**

---

## 三、迁移命名规范

格式：`<动词><目标对象>`，PascalCase，见名知意。

| 场景 | 示例名 |
| --- | --- |
| 新建实体/表 | `CreateCanonFactsTable` |
| 添加字段 | `AddGenerationRecordTokenFields` |
| 删除字段 | `DropObsoleteChapterFields` |
| 修改字段类型/长度 | `AlterCharacterNameMaxLength` |
| 添加索引/外键 | `AddChapterProjectIndexes` |
| 多实体批量变更 | `Phase3CanonLayerSchema` |

**禁止**使用无意义的名字：`Update1`、`Fix`、`Test`、`Temp`。

---

## 四、生成迁移后的必检项

生成迁移后，在应用前必须人工检查 `Up()` 内容：

- [ ] 只包含本次预期的变更，没有意外的 `CreateTable` / `AddColumn`
- [ ] `Down()` 能完整回滚 `Up()` 的所有操作
- [ ] 迁移中不包含数据迁移逻辑（数据迁移应使用独立的数据种子脚本）

如果 `Up()` 包含意外内容 → 见第六节"快照漂移处理"。

---

## 五、唯一允许手动编辑迁移文件的场景

手动编辑迁移文件有严格前提：

**前提：该迁移尚未应用到任何环境（开发机/服务器均未 `database update`）。**

允许的编辑：
- 补充自定义 SQL（如初始化种子数据、创建 PostgreSQL 扩展）
- 调整生成的列约束（如修改默认值）
- 修正 EF 生成的索引名

编辑后必须同步更新对应的 `.Designer.cs` 中相同的逻辑（或直接删除该迁移重新生成）。

**绝对禁止**：
- 删除/注释掉 `Up()` 中的操作以"跳过"某些变更 → 这会导致快照与数据库永久不一致
- 修改已应用的迁移

---

## 六、快照漂移的正确处理方式

**快照漂移**：实体类与数据库实际结构一致，但 EF 快照不知道某些表/列的存在（因为历史上绕过了迁移直接操作数据库）。

症状：新生成的迁移 `Up()` 里出现意外的 `CreateTable`，执行时报 `already exists`。

### 正确处理步骤

```
❌ 错误做法：直接删掉 Up() 里"多出来的"代码
✅ 正确做法：补一个"补记迁移"来修复快照
```

#### 具体操作

```bash
# 1. 先删除包含意外内容的迁移（未应用时可直接删除）
dotnet ef migrations remove \
  --project src/MuseSpace.Infrastructure \
  --startup-project src/MuseSpace.Api

# 2. 生成"补记迁移"，专门让快照"补课"
dotnet ef migrations add FixSnapshotDrift_<描述> \
  --project src/MuseSpace.Infrastructure \
  --startup-project src/MuseSpace.Api \
  --output-dir Migrations

# 3. 将补记迁移的 Up() 清空（保留 Down() 的 DropTable 作为回滚依据）
#    ↑ 这是唯一允许手动编辑迁移的合法场景

# 4. 把该迁移标记为"已应用"而不执行（因为数据库里已经有了）
dotnet ef database update FixSnapshotDrift_<描述> \
  --project src/MuseSpace.Infrastructure \
  --startup-project src/MuseSpace.Api
# 注意：由于 Up() 是空的，此命令只会在 __EFMigrationsHistory 里插一条记录

# 5. 再生成真正想要的功能迁移
dotnet ef migrations add <FeatureMigrationName> ...
dotnet ef database update ...
```

#### 补记迁移模板

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 数据库中这些表/列已存在，此迁移仅用于同步 EF 快照记录，不执行任何 DDL。
    // Created by: <开发者> on <日期>
    // Reason: 历史遗留，<表名> 通过手动 SQL 创建，现补录至迁移历史。
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // 如需回滚，需手动删除这些表
    migrationBuilder.DropTable(name: "xxx");
}
```

---

## 七、禁止直接操作数据库结构

以下操作**必须通过迁移文件进行**，禁止直接在数据库客户端（如 pgAdmin、psql）执行：

- `CREATE TABLE`
- `ALTER TABLE ADD COLUMN`
- `DROP TABLE / DROP COLUMN`
- `CREATE INDEX`

> **唯一例外**：插入初始化数据（seed data），可以直接执行，但推荐用 `HasData()` 或数据种子脚本管理。

---

## 八、本项目历史遗留问题说明

历史上曾存在以下快照漂移（直接 SQL 操作未走迁移），已于 `20260508054422_BackfillHistoricalDrift` 迁移补记完成：

| 表/列 | 补记状态 |
| --- | --- |
| `chapter_batch_draft_runs` | ✅ 已补记 |
| `feature_flags` | ✅ 已补记 |
| `plot_threads` | ✅ 已补记 |
| `agent_runs.InputFull` | ✅ 已补记 |
| `agent_runs.OutputFull` | ✅ 已补记 |

补记迁移采用 `CREATE TABLE IF NOT EXISTS` / `ADD COLUMN IF NOT EXISTS` 的幂等 SQL，对已有数据库为 no-op，对全量重建数据库能正确建出对应对象。**自此项目迁移历史完整，全量重建数据库不会再丢失任何对象。**

---

## 九、回滚操作

```bash
# 回滚到指定迁移（数据库降级）
dotnet ef database update <TargetMigrationName> \
  --project src/MuseSpace.Infrastructure \
  --startup-project src/MuseSpace.Api

# 删除最新一条未应用的迁移文件（代码回滚）
dotnet ef migrations remove \
  --project src/MuseSpace.Infrastructure \
  --startup-project src/MuseSpace.Api
```

> `migrations remove` 只能删除**最新一条**迁移，且该迁移必须**未应用**。

---

## 十、AI 辅助开发约定

AI（Copilot/GitHub Copilot Chat）在执行数据库相关操作时必须遵守：

1. 修改实体后，**同一轮操作内**必须完成 `migrations add` + `database update`
2. `migrations add` 后必须检查生成内容是否只含预期变更
3. 若发现快照漂移，**不允许删减 `Up()` 内容**，必须按第六节流程处理
4. 不允许手工编写迁移文件（除第六节的补记迁移模板）
5. 不允许建议用 `dotnet ef database update --force` 强制跳过错误
