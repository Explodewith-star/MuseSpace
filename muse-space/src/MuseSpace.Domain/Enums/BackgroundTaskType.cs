namespace MuseSpace.Domain.Enums;

/// <summary>后台任务类型</summary>
public enum BackgroundTaskType
{
    NovelImport = 0,
    AssetExtraction = 1,
    ChapterDraftGeneration = 2,
    BatchDraftGeneration = 3,
    OutlinePlanning = 4,
    ConsistencyCheck = 5,
    NovelEndingSummary = 6,
    CharacterExtraction = 7,
    ChapterPlanning = 8,
    OutlineAdjust = 9,
}
