namespace MuseSpace.Domain.Enums;

/// <summary>
/// 伏笔/线索的可见性作用域，控制草稿生成时该伏笔是否注入到 Prompt。
/// </summary>
public enum PlotThreadVisibility
{
    /// <summary>
    /// 仅在埋设它的批次（StoryOutline）内可见。
    /// 适用于番外内部的局部悬念，不应影响主线或其他番外。
    /// </summary>
    ThisOutline = 0,

    /// <summary>
    /// 在整条故事链（OutlineChain）内可见，跨批次但不跨故事线。
    /// 这是默认值：大多数主线伏笔应在同一故事链的后续批次中持续追踪。
    /// </summary>
    Chain = 1,

    /// <summary>
    /// 在整个项目内可见，跨故事链。
    /// 适用于贯穿全书所有故事线的超级谜题，例如世界观核心秘密。
    /// </summary>
    Project = 2,
}
