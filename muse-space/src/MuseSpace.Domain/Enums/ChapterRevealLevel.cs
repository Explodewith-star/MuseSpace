namespace MuseSpace.Domain.Enums;

/// <summary>
/// Controls how far a chapter is allowed to reveal or escalate.
/// </summary>
public enum ChapterRevealLevel
{
    DailyOnly = 0,
    ForeshadowOnly = 1,
    DirectAnomaly = 2,
    Confrontation = 3,
    ResolutionOrReveal = 4,
}
