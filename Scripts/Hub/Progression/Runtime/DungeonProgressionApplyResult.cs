/// <summary>
/// Dungeon 결과 한 건이 Progression에 적용된 결과.
/// </summary>
public enum DungeonProgressionApplyResult
{
    Rejected = 0,
    ProgressIncreased = 1,
    BossUnlocked = 2,
    ChapterAdvanced = 3,
    CampaignCompleted = 4,
    ChapterRegressed = 5,
    BoardRefreshFailed = 6,
    ClearApplied = 7
}
