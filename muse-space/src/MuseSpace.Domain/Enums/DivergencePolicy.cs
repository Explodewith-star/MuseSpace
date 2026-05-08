namespace MuseSpace.Domain.Enums;

/// <summary>
/// 偏离原著的许可程度（配合 GenerationMode.ContinueFromOriginal / SideStoryFromOriginal 使用）。
/// </summary>
public enum DivergencePolicy
{
    /// <summary>严格正典：不得改写原著已确定的事实、结局和关系。</summary>
    StrictCanon = 0,

    /// <summary>软正典：允许局部补写与细节扩展，但不得推翻关键设定。</summary>
    SoftCanon = 1,

    /// <summary>平行线：允许偏离，生成结果被明确标记为平行宇宙/架空情节。</summary>
    AlternateTimeline = 2,
}
