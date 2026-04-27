namespace MuseSpace.Domain.Enums;

/// <summary>
/// Agent 建议的生命周期状态。
/// </summary>
public enum SuggestionStatus
{
    /// <summary>Agent 刚产出，等待用户审阅。</summary>
    Pending = 0,

    /// <summary>用户确认接受。</summary>
    Accepted = 1,

    /// <summary>已写入正式业务表（Character / WorldRule 等）。</summary>
    Applied = 2,

    /// <summary>用户忽略 / 拒绝。</summary>
    Ignored = 3,
}
