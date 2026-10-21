using System.Collections.Generic;

/// <summary>
/// 현재 챕터와 Seed로 일반 2개·특수 1개 게시판을 결정적으로 구성한다.
/// </summary>
public sealed class WantedBoardRefresher
{
    public bool Refresh(WantedBoardState board, ChapterProgressState progress, int refreshSeed)
    {
        if (board == null || progress == null || progress.IsCampaignCompleted ||
            !TryBuildSlots(progress.CurrentChapter, progress.IsBossUnlocked, refreshSeed, out List<WantedBoardSlot> slots))
        {
            return false;
        }

        board.ReplaceAll(slots);
        return true;
    }

    public bool ReissueSeed(WantedBoardState board, int slotIndex, int retrySeed)
    {
        if (board == null || !board.IsReady || slotIndex < 0 || slotIndex >= board.Slots.Count)
        {
            return false;
        }

        WantedBoardSlot slot = board.Slots[slotIndex];
        int nextSeed = DungeonPlanRules.CombineSeed(retrySeed, slot.Poster.ContentId, slot.DungeonSeed);

        if (nextSeed == slot.DungeonSeed)
        {
            nextSeed = unchecked(nextSeed + 1);
        }

        board.ReplaceSeed(slotIndex, nextSeed);
        return true;
    }

    internal bool ActivateBoss(WantedBoardState board, ChapterDefinitionSO chapter, int seed)
    {
        if (board == null || chapter == null || !board.IsReady || chapter.BossWantedPoster == null)
        {
            return false;
        }

        WantedPosterSO boss = chapter.BossWantedPoster;
        board.ReplaceSpecial(new WantedBoardSlot(
            WantedPosterSlotType.Special,
            boss,
            CreateDungeonSeed(seed, boss, WantedBoardState.SpecialSlotIndex)));
        return true;
    }

    internal bool TryBuildSlots(
        ChapterDefinitionSO chapter,
        bool isBossUnlocked,
        int refreshSeed,
        out List<WantedBoardSlot> slots)
    {
        slots = null;

        if (chapter == null || chapter.NormalWantedPosters == null || chapter.NormalWantedPosters.Count < 2)
        {
            return false;
        }

        List<WantedPosterSO> normalCandidates = CopyValidPosters(
            chapter.NormalWantedPosters,
            WantedPosterType.Normal);

        if (normalCandidates.Count < 2)
        {
            return false;
        }

        normalCandidates.Sort(ComparePosterId);
        DungeonPlanRules.Shuffle(
            normalCandidates,
            DungeonPlanRules.CombineSeed(refreshSeed, chapter.ContentId, 0));

        WantedPosterSO special;

        if (isBossUnlocked)
        {
            special = chapter.BossWantedPoster;
        }
        else
        {
            List<WantedPosterSO> conditionalCandidates = CopyValidPosters(
                chapter.ConditionalWantedPosters,
                WantedPosterType.Conditional);

            if (conditionalCandidates.Count == 0)
            {
                return false;
            }

            conditionalCandidates.Sort(ComparePosterId);
            DungeonPlanRandom random = new DungeonPlanRandom(
                DungeonPlanRules.CombineSeed(refreshSeed, chapter.ContentId, 1));
            special = conditionalCandidates[random.Next(conditionalCandidates.Count)];
        }

        if (special == null || special.SlotType != WantedPosterSlotType.Special)
        {
            return false;
        }

        slots = new List<WantedBoardSlot>(WantedBoardState.TotalSlotCount);

        for (int i = 0; i < WantedBoardState.NormalSlotCount; i++)
        {
            WantedPosterSO poster = normalCandidates[i];
            slots.Add(new WantedBoardSlot(
                WantedPosterSlotType.Normal,
                poster,
                CreateDungeonSeed(refreshSeed, poster, i)));
        }

        slots.Add(new WantedBoardSlot(
            WantedPosterSlotType.Special,
            special,
            CreateDungeonSeed(refreshSeed, special, WantedBoardState.SpecialSlotIndex)));
        return true;
    }

    private static List<WantedPosterSO> CopyValidPosters(
        IReadOnlyList<WantedPosterSO> source,
        WantedPosterType requiredType)
    {
        List<WantedPosterSO> result = new List<WantedPosterSO>();

        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            WantedPosterSO poster = source[i];

            if (poster != null && poster.PosterType == requiredType && poster.SlotType ==
                (requiredType == WantedPosterType.Normal ? WantedPosterSlotType.Normal : WantedPosterSlotType.Special) &&
                !result.Contains(poster))
            {
                result.Add(poster);
            }
        }

        return result;
    }

    private static int ComparePosterId(WantedPosterSO left, WantedPosterSO right)
    {
        return string.CompareOrdinal(left.ContentId, right.ContentId);
    }

    private static int CreateDungeonSeed(int refreshSeed, WantedPosterSO poster, int slotIndex)
    {
        return DungeonPlanRules.CombineSeed(refreshSeed, poster.ContentId, slotIndex + 1);
    }
}
