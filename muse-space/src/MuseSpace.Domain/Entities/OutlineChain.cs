using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

/// <summary>
/// 故事链：将多个批次（StoryOutline）串联成一条连续的叙事线。
/// 例如"正传主线"包含第一部、第二部、第三部。
/// </summary>
public class OutlineChain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public GenerationMode Mode { get; set; } = GenerationMode.Original;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
