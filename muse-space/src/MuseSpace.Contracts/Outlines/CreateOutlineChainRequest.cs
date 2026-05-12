namespace MuseSpace.Contracts.Outlines;

public sealed class CreateOutlineChainRequest
{
    public string Name { get; init; } = string.Empty;
    public string Mode { get; init; } = "Original";
}
