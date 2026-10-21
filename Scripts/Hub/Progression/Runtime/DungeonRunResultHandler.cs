using System;

/// <summary>
/// Dungeon 결과를 챕터 진행도·보스 해금·챕터 이동·실패 회귀 중 하나로 반영한다.
/// </summary>
public sealed class DungeonRunResultHandler
{
    private readonly ChapterProgressState _progress;
    private readonly WantedBoardState _board;
    private readonly WantedBoardRefresher _refresher;

    public event Action<WantedPosterSO> ClearRewardRequested;
    public event Action TemporaryProgressResetRequested;

    public DungeonRunResultHandler(
        ChapterProgressState progress,
        WantedBoardState board,
        WantedBoardRefresher refresher)
    {
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _refresher = refresher ?? throw new ArgumentNullException(nameof(refresher));
    }

    public DungeonProgressionApplyResult Apply(
        DungeonRunResult result,
        WantedPosterSO playedPoster,
        int boardRefreshSeed)
    {
        if (_progress.IsCampaignCompleted || !_board.IsReady || playedPoster == null ||
            !_board.Contains(playedPoster))
        {
            return DungeonProgressionApplyResult.Rejected;
        }

        switch (result)
        {
            case DungeonRunResult.Cleared:
                return ApplyCleared(playedPoster, boardRefreshSeed);
            case DungeonRunResult.Failed:
                return ApplyFailed(boardRefreshSeed);
            default:
                return DungeonProgressionApplyResult.Rejected;
        }
    }

    private DungeonProgressionApplyResult ApplyCleared(WantedPosterSO playedPoster, int boardRefreshSeed)
    {
        if (playedPoster.PosterType == WantedPosterType.Boss)
        {
            return ApplyBossCleared(playedPoster, boardRefreshSeed);
        }

        bool willUnlockBoss = _progress.WouldUnlockBoss();

        if (willUnlockBoss && !_refresher.ActivateBoss(_board, _progress.CurrentChapter, boardRefreshSeed))
        {
            return DungeonProgressionApplyResult.BoardRefreshFailed;
        }

        _progress.AddClearProgress();
        ClearRewardRequested?.Invoke(playedPoster);

        if (willUnlockBoss)
        {
            return DungeonProgressionApplyResult.BossUnlocked;
        }

        return _progress.IsBossUnlocked
            ? DungeonProgressionApplyResult.ClearApplied
            : DungeonProgressionApplyResult.ProgressIncreased;
    }

    private DungeonProgressionApplyResult ApplyBossCleared(WantedPosterSO playedPoster, int boardRefreshSeed)
    {
        if (!_progress.IsBossUnlocked || playedPoster != _progress.CurrentChapter.BossWantedPoster)
        {
            return DungeonProgressionApplyResult.Rejected;
        }

        int nextChapterIndex = _progress.CurrentChapterIndex + 1;
        bool completesCampaign = nextChapterIndex >= _progress.Chapters.Count;
        System.Collections.Generic.List<WantedBoardSlot> nextSlots = null;

        if (!completesCampaign && !_refresher.TryBuildSlots(
            _progress.Chapters[nextChapterIndex],
            false,
            boardRefreshSeed,
            out nextSlots))
        {
            return DungeonProgressionApplyResult.BoardRefreshFailed;
        }

        if (!_progress.AdvanceChapter())
        {
            return DungeonProgressionApplyResult.Rejected;
        }

        ClearRewardRequested?.Invoke(playedPoster);

        if (completesCampaign)
        {
            _board.Clear();
            return DungeonProgressionApplyResult.CampaignCompleted;
        }

        _board.ReplaceAll(nextSlots);
        return DungeonProgressionApplyResult.ChapterAdvanced;
    }

    private DungeonProgressionApplyResult ApplyFailed(int boardRefreshSeed)
    {
        if (!_refresher.TryBuildSlots(
            _progress.CurrentChapter,
            false,
            boardRefreshSeed,
            out System.Collections.Generic.List<WantedBoardSlot> resetSlots))
        {
            return DungeonProgressionApplyResult.BoardRefreshFailed;
        }

        _progress.ResetCurrentChapter();
        _board.ReplaceAll(resetSlots);
        TemporaryProgressResetRequested?.Invoke();
        return DungeonProgressionApplyResult.ChapterRegressed;
    }
}
