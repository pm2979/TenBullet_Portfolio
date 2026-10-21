using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chapter와 WantedPoster의 ID·참조·게시판 슬롯 충족 가능 여부를 검증한다.
/// </summary>
public static class ProgressionDataValidator
{
    // Addressable 루트가 제공하는 Progression 정적 데이터 전체를 검증한다.
    public static DungeonDataValidationResult Validate(GameDatabaseSO database)
    {
        if (database == null)
        {
            DungeonDataValidationResult result = new DungeonDataValidationResult();
            result.AddError("GameDatabase가 null입니다.");
            return result;
        }

        return ValidateCollection(
            database.ChapterDefinitions,
            database.WantedPosters,
            database.DungeonDefinitions);
    }

    // GameDatabase가 보유할 Progression 정적 데이터 전체를 검증한다.
    public static DungeonDataValidationResult ValidateCollection(
        IReadOnlyList<ChapterDefinitionSO> chapters,
        IReadOnlyList<WantedPosterSO> wantedPosters,
        IReadOnlyList<DungeonDefinitionSO> dungeons)
    {
        DungeonDataValidationResult result = new DungeonDataValidationResult();
        HashSet<WantedPosterSO> posterCatalog = BuildPosterCatalog(wantedPosters, result);
        HashSet<DungeonDefinitionSO> dungeonCatalog = BuildDungeonCatalog(dungeons);

        ValidatePosters(wantedPosters, dungeonCatalog, result);
        ValidateChapters(chapters, posterCatalog, result);

        return result;
    }

    // null·중복 콘텐츠 ID를 검사하며 유효한 수배서 참조 집합을 만든다.
    private static HashSet<WantedPosterSO> BuildPosterCatalog(
        IReadOnlyList<WantedPosterSO> wantedPosters,
        DungeonDataValidationResult result)
    {
        HashSet<WantedPosterSO> catalog = new HashSet<WantedPosterSO>();
        Dictionary<string, int> idOccurrences = new Dictionary<string, int>();

        if (wantedPosters == null)
        {
            result.AddError("GameDatabase의 WantedPoster 목록이 null입니다.");
            return catalog;
        }

        for (int i = 0; i < wantedPosters.Count; i++)
        {
            WantedPosterSO poster = wantedPosters[i];

            if (poster == null)
            {
                result.AddError($"WantedPosters[{i}]가 null입니다.");
                continue;
            }

            catalog.Add(poster);
            CountContentId(poster.ContentId, idOccurrences);

            if (string.IsNullOrWhiteSpace(poster.ContentId))
            {
                result.AddError($"'{poster.name}'의 콘텐츠 ID가 비어 있습니다.", poster);
            }
        }

        AddDuplicateIdErrors("수배서", idOccurrences, result);
        return catalog;
    }

    // WantedPoster의 Dungeon 참조와 유형·슬롯 조합을 검증한다.
    private static void ValidatePosters(
        IReadOnlyList<WantedPosterSO> wantedPosters,
        HashSet<DungeonDefinitionSO> dungeonCatalog,
        DungeonDataValidationResult result)
    {
        if (wantedPosters == null)
        {
            return;
        }

        for (int i = 0; i < wantedPosters.Count; i++)
        {
            WantedPosterSO poster = wantedPosters[i];

            if (poster == null)
            {
                continue;
            }

            ValidatePosterSlot(poster, result);

            if (!Enum.IsDefined(typeof(DungeonDifficulty), poster.Difficulty))
            {
                result.AddError($"'{poster.name}'의 난이도 값이 유효하지 않습니다.", poster);
            }

            if (poster.DungeonDefinition == null)
            {
                result.AddError($"'{poster.name}'의 Dungeon 정의가 비어 있습니다.", poster);
            }
            else if (!dungeonCatalog.Contains(poster.DungeonDefinition))
            {
                result.AddError($"'{poster.name}'이 GameDatabase에 없는 Dungeon '{poster.DungeonDefinition.name}'을 참조합니다.", poster);
            }

            ValidateEnemyCandidates(poster, result);
            ValidateObjective(poster, result);
        }
    }

    // 일반 배치 Enemy 후보의 null·중복·양수 Weight와 Normal/Elite 후보 존재를 검증한다.
    private static void ValidateEnemyCandidates(WantedPosterSO poster, DungeonDataValidationResult result)
    {
        IReadOnlyList<WantedPosterEnemyCandidate> candidates = poster.EnemyCandidates;

        if (candidates == null || candidates.Count == 0)
        {
            result.AddError($"'{poster.name}'의 Enemy 후보가 비어 있습니다.", poster);
            return;
        }

        HashSet<EnemyArchetypeSO> distinctArchetypes = new HashSet<EnemyArchetypeSO>();
        bool hasDeployableGrade = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            WantedPosterEnemyCandidate candidate = candidates[i];

            if (candidate == null || candidate.Archetype == null)
            {
                result.AddError($"'{poster.name}'의 EnemyCandidates[{i}]가 비어 있습니다.", poster);
                continue;
            }

            if (!distinctArchetypes.Add(candidate.Archetype))
            {
                result.AddError($"'{poster.name}'의 Enemy 후보에 '{candidate.Archetype.name}'이(가) 중복 등록되었습니다.", poster);
            }

            if (candidate.Weight <= 0f)
            {
                result.AddError($"'{poster.name}'의 EnemyCandidates[{i}] Weight는 0보다 커야 합니다.", poster);
            }

            hasDeployableGrade |= candidate.Archetype.MonsterGrade == MonsterGrade.Normal ||
                candidate.Archetype.MonsterGrade == MonsterGrade.Elite;
        }

        if (!hasDeployableGrade)
        {
            result.AddError($"'{poster.name}'의 Enemy 후보에 Normal·Elite 등급이 하나도 없습니다.", poster);
        }

        if (poster.EliteRatio < 0f || poster.EliteRatio > 1f)
        {
            result.AddError($"'{poster.name}'의 Elite 비율은 0~1 범위여야 합니다.", poster);
        }
    }

    // Objective Type별로 필요한 Target/Boss 참조만 존재하는지, Dungeon Type과 일관되는지 검증한다.
    private static void ValidateObjective(WantedPosterSO poster, DungeonDataValidationResult result)
    {
        if (!Enum.IsDefined(typeof(DungeonObjectiveType), poster.ObjectiveType))
        {
            result.AddError($"'{poster.name}'의 ObjectiveType 값이 유효하지 않습니다.", poster);
            return;
        }

        switch (poster.ObjectiveType)
        {
            case DungeonObjectiveType.AllEnemies:
                if (poster.TargetEnemyArchetype != null || poster.BossEnemyArchetype != null)
                {
                    result.AddError($"'{poster.name}'은(는) AllEnemies 목표에 Target·Boss 참조를 가질 수 없습니다.", poster);
                }

                break;

            case DungeonObjectiveType.Target:
                if (poster.BossEnemyArchetype != null)
                {
                    result.AddError($"'{poster.name}'은(는) Target 목표에 Boss 참조를 가질 수 없습니다.", poster);
                }

                if (poster.TargetEnemyArchetype == null)
                {
                    result.AddError($"'{poster.name}'의 Target 목표에 TargetEnemyArchetype이 비어 있습니다.", poster);
                }

                ValidateTargetPointAvailability(poster, result);
                break;

            case DungeonObjectiveType.Boss:
                if (poster.TargetEnemyArchetype != null)
                {
                    result.AddError($"'{poster.name}'은(는) Boss 목표에 Target 참조를 가질 수 없습니다.", poster);
                }

                if (poster.BossEnemyArchetype == null)
                {
                    result.AddError($"'{poster.name}'의 Boss 목표에 BossEnemyArchetype이 비어 있습니다.", poster);
                }
                else if (poster.BossEnemyArchetype.MonsterGrade != MonsterGrade.Boss)
                {
                    result.AddError($"'{poster.name}'의 BossEnemyArchetype '{poster.BossEnemyArchetype.name}'은(는) Boss 등급이 아닙니다.", poster);
                }

                if (poster.DungeonDefinition != null && poster.DungeonDefinition.DungeonType != DungeonType.Boss)
                {
                    result.AddError($"'{poster.name}'은(는) Boss 목표에 Boss Dungeon을 참조해야 합니다.", poster);
                }

                break;
        }
    }

    // Target 목표가 선택될 수 있는 물리 Room 중 Target Spawn 지점이 하나 이상 있는지 검증한다.
    private static void ValidateTargetPointAvailability(WantedPosterSO poster, DungeonDataValidationResult result)
    {
        if (poster.DungeonDefinition == null)
        {
            return;
        }

        IReadOnlyList<RoomArchetypeSO> roomPool = poster.DungeonDefinition.RoomPool;

        if (HasTargetSpawnPoint(roomPool) ||
            HasTargetSpawnPoint(poster.DungeonDefinition.BossArenaRoom))
        {
            return;
        }

        result.AddError(
            $"'{poster.name}'의 Dungeon '{poster.DungeonDefinition.name}'에 Target Spawn 지점을 가진 Room이 없습니다.",
            poster);
    }

    private static bool HasTargetSpawnPoint(IReadOnlyList<RoomArchetypeSO> rooms)
    {
        if (rooms == null)
        {
            return false;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            if (HasTargetSpawnPoint(rooms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTargetSpawnPoint(RoomArchetypeSO room)
    {
        if (room == null || room.SpawnPoints == null)
        {
            return false;
        }

        for (int i = 0; i < room.SpawnPoints.Count; i++)
        {
            if (room.SpawnPoints[i] != null && room.SpawnPoints[i].Kind == DungeonSpawnPointKind.Target)
            {
                return true;
            }
        }

        return false;
    }

    // 수배서 유형이 고정된 게시판 슬롯 규칙과 일치하는지 검증한다.
    private static void ValidatePosterSlot(WantedPosterSO poster, DungeonDataValidationResult result)
    {
        WantedPosterSlotType requiredSlot = poster.PosterType == WantedPosterType.Normal
            ? WantedPosterSlotType.Normal
            : WantedPosterSlotType.Special;

        if (poster.SlotType != requiredSlot)
        {
            result.AddError(
                $"'{poster.name}'의 유형 '{poster.PosterType}'은(는) '{requiredSlot}' 슬롯을 사용해야 합니다.",
                poster);
        }

        if (poster.PosterType == WantedPosterType.Boss &&
            poster.DungeonDefinition != null &&
            poster.DungeonDefinition.DungeonType != DungeonType.Boss)
        {
            result.AddError($"보스 수배서 '{poster.name}'은(는) Boss Dungeon을 참조해야 합니다.", poster);
        }

        if (poster.PosterType != WantedPosterType.Boss &&
            poster.DungeonDefinition != null &&
            poster.DungeonDefinition.DungeonType == DungeonType.Boss)
        {
            result.AddError($"일반·조건부 수배서 '{poster.name}'은(는) Boss Dungeon을 참조할 수 없습니다.", poster);
        }
    }

    // 챕터 ID와 일반 2개·특수 1개 슬롯을 구성할 수 있는 후보를 검증한다.
    private static void ValidateChapters(
        IReadOnlyList<ChapterDefinitionSO> chapters,
        HashSet<WantedPosterSO> posterCatalog,
        DungeonDataValidationResult result)
    {
        if (chapters == null)
        {
            result.AddError("GameDatabase의 Chapter 목록이 null입니다.");
            return;
        }

        Dictionary<string, int> idOccurrences = new Dictionary<string, int>();
        Dictionary<WantedPosterSO, ChapterDefinitionSO> owners = new Dictionary<WantedPosterSO, ChapterDefinitionSO>();

        for (int i = 0; i < chapters.Count; i++)
        {
            ChapterDefinitionSO chapter = chapters[i];

            if (chapter == null)
            {
                result.AddError($"ChapterDefinitions[{i}]가 null입니다.");
                continue;
            }

            CountContentId(chapter.ContentId, idOccurrences);

            if (string.IsNullOrWhiteSpace(chapter.ContentId))
            {
                result.AddError($"'{chapter.name}'의 콘텐츠 ID가 비어 있습니다.", chapter);
            }

            ValidateChapter(chapter, posterCatalog, owners, result);
        }

        AddDuplicateIdErrors("챕터", idOccurrences, result);
    }

    // 챕터 하나의 게시판 후보 수·유형·카탈로그 소속과 보스 해금 값을 검증한다.
    private static void ValidateChapter(
        ChapterDefinitionSO chapter,
        HashSet<WantedPosterSO> posterCatalog,
        Dictionary<WantedPosterSO, ChapterDefinitionSO> owners,
        DungeonDataValidationResult result)
    {
        HashSet<WantedPosterSO> normalPosters = ValidatePosterPool(
            chapter,
            chapter.NormalWantedPosters,
            WantedPosterType.Normal,
            "normalWantedPosters",
            posterCatalog,
            owners,
            result);

        if (normalPosters.Count < 2)
        {
            result.AddError($"'{chapter.name}'은(는) 일반 슬롯 두 개를 채울 서로 다른 일반 수배서가 부족합니다.", chapter);
        }

        HashSet<WantedPosterSO> conditionalPosters = ValidatePosterPool(
            chapter,
            chapter.ConditionalWantedPosters,
            WantedPosterType.Conditional,
            "conditionalWantedPosters",
            posterCatalog,
            owners,
            result);

        if (conditionalPosters.Count < 1)
        {
            result.AddError($"'{chapter.name}'은(는) 보스 해금 전 특수 슬롯을 채울 조건부 수배서가 없습니다.", chapter);
        }

        // bossWantedPoster가 비어 있으면 "보스 콘텐츠 미정" 상태로 허용한다(ChapterProgressState가 보스 해금을 비활성화함).
        WantedPosterSO bossPoster = chapter.BossWantedPoster;

        if (bossPoster != null)
        {
            ValidatePosterReference(chapter, bossPoster, WantedPosterType.Boss, posterCatalog, owners, result);
        }

        if (chapter.BossUnlockRequiredClearCount <= 0)
        {
            result.AddError($"'{chapter.name}'의 보스 해금 요구 횟수는 0보다 커야 합니다.", chapter);
        }
    }

    // 챕터의 수배서 후보 배열을 중복 없이 검증하고 유효 참조 집합을 반환한다.
    private static HashSet<WantedPosterSO> ValidatePosterPool(
        ChapterDefinitionSO chapter,
        IReadOnlyList<WantedPosterSO> posters,
        WantedPosterType requiredType,
        string fieldName,
        HashSet<WantedPosterSO> posterCatalog,
        Dictionary<WantedPosterSO, ChapterDefinitionSO> owners,
        DungeonDataValidationResult result)
    {
        HashSet<WantedPosterSO> distinctPosters = new HashSet<WantedPosterSO>();

        if (posters == null)
        {
            result.AddError($"'{chapter.name}'의 {fieldName}가 null입니다.", chapter);
            return distinctPosters;
        }

        for (int i = 0; i < posters.Count; i++)
        {
            WantedPosterSO poster = posters[i];

            if (poster == null)
            {
                result.AddError($"'{chapter.name}'의 {fieldName}[{i}]가 null입니다.", chapter);
                continue;
            }

            if (!distinctPosters.Add(poster))
            {
                result.AddError($"'{chapter.name}'의 {fieldName}에 '{poster.name}'이(가) 중복 등록되었습니다.", chapter);
                continue;
            }

            ValidatePosterReference(chapter, poster, requiredType, posterCatalog, owners, result);
        }

        return distinctPosters;
    }

    // 챕터가 참조한 수배서의 카탈로그 소속·유형·단일 챕터 소유를 검증한다.
    private static void ValidatePosterReference(
        ChapterDefinitionSO chapter,
        WantedPosterSO poster,
        WantedPosterType requiredType,
        HashSet<WantedPosterSO> posterCatalog,
        Dictionary<WantedPosterSO, ChapterDefinitionSO> owners,
        DungeonDataValidationResult result)
    {
        if (!posterCatalog.Contains(poster))
        {
            result.AddError($"'{chapter.name}'이 GameDatabase에 없는 수배서 '{poster.name}'을 참조합니다.", chapter);
        }

        if (poster.PosterType != requiredType)
        {
            result.AddError($"'{chapter.name}'의 '{poster.name}' 유형은 '{requiredType}'이어야 합니다.", chapter);
        }

        if (owners.TryGetValue(poster, out ChapterDefinitionSO owner) && owner != chapter)
        {
            result.AddError($"수배서 '{poster.name}'이 '{owner.name}'과(와) '{chapter.name}'에 중복 소속되었습니다.", poster);
        }
        else
        {
            owners[poster] = chapter;
        }
    }

    // null을 제외한 Dungeon 참조 집합을 만든다.
    private static HashSet<DungeonDefinitionSO> BuildDungeonCatalog(IReadOnlyList<DungeonDefinitionSO> dungeons)
    {
        HashSet<DungeonDefinitionSO> catalog = new HashSet<DungeonDefinitionSO>();

        if (dungeons == null)
        {
            return catalog;
        }

        for (int i = 0; i < dungeons.Count; i++)
        {
            if (dungeons[i] != null)
            {
                catalog.Add(dungeons[i]);
            }
        }

        return catalog;
    }

    // 콘텐츠 ID가 비어 있지 않으면 발생 횟수를 누적한다.
    private static void CountContentId(string contentId, Dictionary<string, int> occurrences)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return;
        }

        occurrences.TryGetValue(contentId, out int count);
        occurrences[contentId] = count + 1;
    }

    // 두 번 이상 사용된 콘텐츠 ID를 오류로 추가한다.
    private static void AddDuplicateIdErrors(
        string label,
        Dictionary<string, int> occurrences,
        DungeonDataValidationResult result)
    {
        foreach (KeyValuePair<string, int> occurrence in occurrences)
        {
            if (occurrence.Value > 1)
            {
                result.AddError($"{label} 콘텐츠 ID '{occurrence.Key}'가 {occurrence.Value}개에서 중복 사용되었습니다.");
            }
        }
    }
}
