namespace MuseSpace.Contracts.Outlines;

public sealed class AdjustOutlineRequest
{
    /// <summary>自然语言调整指令，例如"把第3章扩展为10章，重点铺垫感情线"</summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>目标章节编号列表（要展开或合并的章节）</summary>
    public List<int> TargetChapterNumbers { get; set; } = new();

    /// <summary>期望结果章节数（Expand 时填写；Merge 时可不填）</summary>
    public int? TargetCount { get; set; }
}
