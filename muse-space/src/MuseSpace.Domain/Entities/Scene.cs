namespace MuseSpace.Domain.Entities;

public class Scene
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public int Sequence { get; set; }
    public string? Goal { get; set; }
    public string? Conflict { get; set; }
    public string? EmotionCurve { get; set; }
    public string? DraftText { get; set; }
    public string? FinalText { get; set; }
    public List<Guid> InvolvedCharacterIds { get; set; } = [];
}
