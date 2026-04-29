namespace MuseSpace.Contracts.Chapters;

public sealed class BatchDeleteChaptersRequest
{
    public List<Guid> ChapterIds { get; set; } = [];
}
