namespace MuseSpace.Domain.Entities;

public class UserLlmPreference
{
    public Guid UserId { get; set; }
    /// <summary>OpenRouter | DeepSeek</summary>
    public string Provider { get; set; } = "OpenRouter";
    /// <summary>具体模型 ID，null 表示使用 appsettings 默认值</summary>
    public string? ModelId { get; set; }

    public User User { get; set; } = null!;
}
