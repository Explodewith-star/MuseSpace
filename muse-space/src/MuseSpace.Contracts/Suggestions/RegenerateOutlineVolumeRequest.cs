namespace MuseSpace.Contracts.Suggestions;

/// <summary>单卷重做请求。</summary>
public sealed class RegenerateOutlineVolumeRequest
{
    /// <summary>用户附加要求（例如"加强卷末高潮"等）。</summary>
    public string? ExtraInstruction { get; init; }
}
