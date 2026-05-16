using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseSpace.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterStoryOutlineId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. 新增 StoryOutlineId 列（先允许 NULL，待数据迁移后再设为 NOT NULL）
            migrationBuilder.Sql(@"
                ALTER TABLE characters
                ADD COLUMN IF NOT EXISTS ""StoryOutlineId"" uuid NULL;
            ");

            // 2. 将现有角色归属到项目的默认大纲（IsDefault = true）
            migrationBuilder.Sql(@"
                UPDATE characters c
                SET ""StoryOutlineId"" = (
                    SELECT o.""Id""
                    FROM story_outlines o
                    WHERE o.""StoryProjectId"" = c.""StoryProjectId""
                      AND o.""IsDefault"" = true
                    LIMIT 1
                )
                WHERE c.""StoryOutlineId"" IS NULL;
            ");

            // 3. 若有角色没有默认大纲匹配（极端情况），用项目下任意第一个大纲兜底
            migrationBuilder.Sql(@"
                UPDATE characters c
                SET ""StoryOutlineId"" = (
                    SELECT o.""Id""
                    FROM story_outlines o
                    WHERE o.""StoryProjectId"" = c.""StoryProjectId""
                    ORDER BY o.""CreatedAt""
                    LIMIT 1
                )
                WHERE c.""StoryOutlineId"" IS NULL;
            ");

            // 4. 设为 NOT NULL
            migrationBuilder.Sql(@"
                ALTER TABLE characters
                ALTER COLUMN ""StoryOutlineId"" SET NOT NULL;
            ");

            // 5. 建索引
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_characters_StoryOutlineId""
                ON characters(""StoryOutlineId"");
            ");

            // 6. 添加外键
            migrationBuilder.Sql(@"
                ALTER TABLE characters
                ADD CONSTRAINT ""FK_characters_story_outlines_StoryOutlineId""
                FOREIGN KEY (""StoryOutlineId"") REFERENCES story_outlines(""Id"")
                ON DELETE CASCADE;
            ");

            // 7. 移除旧 Category 列
            migrationBuilder.Sql(@"
                ALTER TABLE characters
                DROP COLUMN IF EXISTS ""Category"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 恢复 Category 列
            migrationBuilder.Sql(@"
                ALTER TABLE characters
                ADD COLUMN IF NOT EXISTS ""Category"" varchar(100) NULL;
            ");

            // 移除外键和索引
            migrationBuilder.Sql(@"
                ALTER TABLE characters
                DROP CONSTRAINT IF EXISTS ""FK_characters_story_outlines_StoryOutlineId"";
            ");
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_characters_StoryOutlineId"";
            ");

            // 移除列
            migrationBuilder.Sql(@"
                ALTER TABLE characters
                DROP COLUMN IF EXISTS ""StoryOutlineId"";
            ");
        }
    }
}
