using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 현재 챕터와 보스 해금 진행도의 단일 런타임 권위.
/// </summary>
public sealed class ChapterProgressState
{
    private readonly ReadOnlyCollection<ChapterDefinitionSO> _chapters;

    public IReadOnlyList<ChapterDefinitionSO> Chapters => _chapters;
    public int CurrentChapterIndex { get; private set; }
    public ChapterDefinitionSO CurrentChapter => IsCampaignCompleted
        ? null
        : _chapters[CurrentChapterIndex];
    public int BossUnlockProgress { get; private set; }
    public bool IsBossUnlocked { get; private set; }
    public bool IsCampaignCompleted { get; private set; }

    public ChapterProgressState(IReadOnlyList<ChapterDefinitionSO> chapters)
    {
        if (chapters == null || chapters.Count == 0)
        {
            throw new ArgumentException("Chapter 목록이 비어 있습니다.", nameof(chapters));
        }

        List<ChapterDefinitionSO> copy = new List<ChapterDefinitionSO>(chapters.Count);

        for (int i = 0; i < chapters.Count; i++)
        {
            if (chapters[i] == null)
            {
                throw new ArgumentException($"Chapter 목록의 {i}번 항목이 null입니다.", nameof(chapters));
            }

            copy.Add(chapters[i]);
        }

        _chapters = new ReadOnlyCollection<ChapterDefinitionSO>(copy);
    }

    // 상태를 바꾸지 않고 다음 클리어 1회로 보스가 해금될지만 확인한다.
    internal bool WouldUnlockBoss()
    {
        if (IsCampaignCompleted || IsBossUnlocked || CurrentChapter.BossWantedPoster == null)
        {
            return false;
        }

        return BossUnlockProgress + 1 >= CurrentChapter.BossUnlockRequiredClearCount;
    }

    // 일반·조건부 던전 클리어 1회를 반영하고 이번 호출에서 보스가 해금됐는지 반환한다.
    // BossWantedPoster가 없는 챕터는 진행도만 쌓이고 해금되지 않는다(보스 콘텐츠 추가 전까지 게시판이 계속 순환).
    internal bool AddClearProgress()
    {
        if (IsCampaignCompleted || IsBossUnlocked)
        {
            return false;
        }

        BossUnlockProgress++;

        if (CurrentChapter.BossWantedPoster == null || BossUnlockProgress < CurrentChapter.BossUnlockRequiredClearCount)
        {
            return false;
        }

        BossUnlockProgress = CurrentChapter.BossUnlockRequiredClearCount;
        IsBossUnlocked = true;
        return true;
    }

    // 현재 보스 클리어를 반영해 다음 챕터로 이동하거나 전체 완료를 확정한다.
    internal bool AdvanceChapter()
    {
        if (IsCampaignCompleted || !IsBossUnlocked)
        {
            return false;
        }

        BossUnlockProgress = 0;
        IsBossUnlocked = false;

        if (CurrentChapterIndex + 1 >= _chapters.Count)
        {
            IsCampaignCompleted = true;
            return true;
        }

        CurrentChapterIndex++;
        return true;
    }

    // 실패 회귀로 현재 챕터의 임시 해금 상태를 초기화한다.
    internal void ResetCurrentChapter()
    {
        if (IsCampaignCompleted)
        {
            return;
        }

        BossUnlockProgress = 0;
        IsBossUnlocked = false;
    }
}
