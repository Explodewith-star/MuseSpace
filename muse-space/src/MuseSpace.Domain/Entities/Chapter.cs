using MuseSpace.Domain.Enums;

namespace MuseSpace.Domain.Entities;

public class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoryProjectId { get; set; }
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Goal { get; set; }
    public ChapterStatus Status { get; set; } = ChapterStatus.Planned;
    public string? Summary { get; set; }
    public string? DraftText { get; set; }
    public string? FinalText { get; set; }
}
