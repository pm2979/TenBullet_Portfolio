/// <summary>
/// 게시판 슬롯 한 칸의 수배서와 Dungeon Seed를 묶는 불변 값.
/// </summary>
public readonly struct WantedBoardSlot
{
    public WantedPosterSlotType SlotType { get; }
    public WantedPosterSO Poster { get; }
    public int DungeonSeed { get; }

    public WantedBoardSlot(WantedPosterSlotType slotType, WantedPosterSO poster, int dungeonSeed)
    {
        SlotType = slotType;
        Poster = poster;
        DungeonSeed = dungeonSeed;
    }
}
