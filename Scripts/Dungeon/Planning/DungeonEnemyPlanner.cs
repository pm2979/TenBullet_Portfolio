using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WantedPoster snapshot과 생성된 Spawn Point snapshot으로 결정적인 DungeonEnemyPlan을 만든다.
/// MonoBehaviour, PoolManager, A*, Edgar 타입에 의존하지 않는다.
/// </summary>
public sealed class DungeonEnemyPlanner
{
    private const int WeightResolution = 1_000_000;

    // Seed와 WantedPoster 구성으로 일반·Target·Boss Spawn 배정을 모두 포함한 계획을 만든다.
    public DungeonEnemyPlanResult Create(
        WantedPosterSO poster,
        IReadOnlyList<DungeonSpawnPointSnapshot> spawnPoints,
        int seed)
    {
        if (poster == null)
        {
            return DungeonEnemyPlanResult.Failed(DungeonEnemyPlanFailure.InvalidPoster, "WantedPoster가 없습니다.");
        }

        if (poster.ObjectiveType == DungeonObjectiveType.Target && poster.TargetEnemyArchetype == null)
        {
            return DungeonEnemyPlanResult.Failed(
                DungeonEnemyPlanFailure.InvalidPoster, "Target 목표에 TargetEnemyArchetype이 없습니다.");
        }

        if (poster.ObjectiveType == DungeonObjectiveType.Boss && poster.BossEnemyArchetype == null)
        {
            return DungeonEnemyPlanResult.Failed(
                DungeonEnemyPlanFailure.InvalidPoster, "Boss 목표에 BossEnemyArchetype이 없습니다.");
        }

        if (!TryBuildSortedPoints(
                spawnPoints,
                out List<DungeonSpawnPointSnapshot> regularPoints,
                out List<DungeonSpawnPointSnapshot> targetPoints,
                out List<DungeonSpawnPointSnapshot> bossPoints,
                out string pointFailureReason))
        {
            return DungeonEnemyPlanResult.Failed(DungeonEnemyPlanFailure.InvalidSpawnPointSnapshot, pointFailureReason);
        }

        int regularCount = regularPoints.Count;

        if (regularCount == 0)
        {
            return DungeonEnemyPlanResult.Failed(
                DungeonEnemyPlanFailure.NoRegularEnemyPoint, "일반 Enemy Spawn Point가 없습니다.");
        }

        int baseSeed = DungeonPlanRules.CombineSeed(seed, poster.ContentId);
        int eliteCount = Mathf.Clamp(Mathf.RoundToInt(regularCount * poster.EliteRatio), 0, regularCount);
        HashSet<int> eliteSlotIndices = BuildEliteSlotIndices(regularCount, eliteCount, baseSeed);

        List<WeightedCandidate> normalCandidates = BuildCandidatePool(poster.EnemyCandidates, MonsterGrade.Normal);
        List<WeightedCandidate> eliteCandidates = BuildCandidatePool(poster.EnemyCandidates, MonsterGrade.Elite);

        List<DungeonEnemySpawnEntry> entries = new List<DungeonEnemySpawnEntry>(regularCount + 1);
        DungeonPlanRandom candidateRandom = new DungeonPlanRandom(DungeonPlanRules.CombineSeed(baseSeed, "slot_candidate"));

        for (int i = 0; i < regularCount; i++)
        {
            DungeonSpawnPointSnapshot point = regularPoints[i];
            bool isElite = eliteSlotIndices.Contains(i);
            List<WeightedCandidate> pool = isElite ? eliteCandidates : normalCandidates;

            if (pool.Count == 0)
            {
                return DungeonEnemyPlanResult.Failed(
                    DungeonEnemyPlanFailure.NoEligibleCandidate,
                    $"{(isElite ? "Elite" : "Normal")} 등급의 양수 Weight 후보가 없습니다.");
            }

            EnemyArchetypeSO archetype = SelectWeighted(pool, ref candidateRandom);
            entries.Add(new DungeonEnemySpawnEntry(
                point.RoomId, point.SpawnPointId, DungeonSpawnPointKind.RegularEnemy, archetype));
        }

        if (poster.ObjectiveType == DungeonObjectiveType.Target)
        {
            if (targetPoints.Count == 0)
            {
                return DungeonEnemyPlanResult.Failed(
                    DungeonEnemyPlanFailure.TargetPointUnavailable, "Target Spawn Point가 없습니다.");
            }

            DungeonPlanRandom targetRandom = new DungeonPlanRandom(DungeonPlanRules.CombineSeed(baseSeed, "target_select"));
            DungeonSpawnPointSnapshot targetPoint = targetPoints[targetRandom.Next(targetPoints.Count)];
            entries.Add(new DungeonEnemySpawnEntry(
                targetPoint.RoomId, targetPoint.SpawnPointId, DungeonSpawnPointKind.Target, poster.TargetEnemyArchetype));
        }
        else if (poster.ObjectiveType == DungeonObjectiveType.Boss)
        {
            if (bossPoints.Count != 1)
            {
                return DungeonEnemyPlanResult.Failed(
                    DungeonEnemyPlanFailure.BossPointInvalid,
                    $"Boss Spawn Point는 정확히 1개여야 합니다(실제 {bossPoints.Count}개).");
            }

            DungeonSpawnPointSnapshot bossPoint = bossPoints[0];
            entries.Add(new DungeonEnemySpawnEntry(
                bossPoint.RoomId, bossPoint.SpawnPointId, DungeonSpawnPointKind.Boss, poster.BossEnemyArchetype));
        }

        return DungeonEnemyPlanResult.Success(new DungeonEnemyPlan(entries));
    }

    // Spawn Point를 Kind별로 분류하고 RoomId→SpawnPointId 안정 순서로 정렬하며, null·중복 전달을 실패로 처리한다.
    private static bool TryBuildSortedPoints(
        IReadOnlyList<DungeonSpawnPointSnapshot> spawnPoints,
        out List<DungeonSpawnPointSnapshot> regularPoints,
        out List<DungeonSpawnPointSnapshot> targetPoints,
        out List<DungeonSpawnPointSnapshot> bossPoints,
        out string failureReason)
    {
        regularPoints = new List<DungeonSpawnPointSnapshot>();
        targetPoints = new List<DungeonSpawnPointSnapshot>();
        bossPoints = new List<DungeonSpawnPointSnapshot>();
        failureReason = null;

        if (spawnPoints == null)
        {
            return true;
        }

        HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            DungeonSpawnPointSnapshot point = spawnPoints[i];

            if (point == null)
            {
                failureReason = $"SpawnPoints[{i}]가 null입니다.";
                return false;
            }

            string key = point.RoomId + "::" + point.SpawnPointId;

            if (!seenKeys.Add(key))
            {
                failureReason = $"Spawn Point '{key}'가 중복 전달됐습니다.";
                return false;
            }

            switch (point.Kind)
            {
                case DungeonSpawnPointKind.RegularEnemy:
                    regularPoints.Add(point);
                    break;
                case DungeonSpawnPointKind.Target:
                    targetPoints.Add(point);
                    break;
                case DungeonSpawnPointKind.Boss:
                    bossPoints.Add(point);
                    break;
            }
        }

        regularPoints.Sort(ComparePoints);
        targetPoints.Sort(ComparePoints);
        bossPoints.Sort(ComparePoints);
        return true;
    }

    private static int ComparePoints(DungeonSpawnPointSnapshot left, DungeonSpawnPointSnapshot right)
    {
        int roomCompare = string.CompareOrdinal(left.RoomId, right.RoomId);
        return roomCompare != 0 ? roomCompare : string.CompareOrdinal(left.SpawnPointId, right.SpawnPointId);
    }

    // RegularEnemy 슬롯 인덱스를 Seed 기반으로 섞어 앞에서부터 eliteCount개를 Elite 슬롯으로 고정한다.
    private static HashSet<int> BuildEliteSlotIndices(int regularCount, int eliteCount, int baseSeed)
    {
        List<int> slotIndices = new List<int>(regularCount);

        for (int i = 0; i < regularCount; i++)
        {
            slotIndices.Add(i);
        }

        DungeonPlanRules.Shuffle(slotIndices, DungeonPlanRules.CombineSeed(baseSeed, "slot_shuffle"));

        HashSet<int> eliteSlotIndices = new HashSet<int>();

        for (int i = 0; i < eliteCount; i++)
        {
            eliteSlotIndices.Add(slotIndices[i]);
        }

        return eliteSlotIndices;
    }

    // 후보 목록에서 지정 등급·양수 Weight 항목만 ContentId 기준 안정 정렬로 모은다.
    private static List<WeightedCandidate> BuildCandidatePool(
        IReadOnlyList<WantedPosterEnemyCandidate> candidates, MonsterGrade grade)
    {
        List<WeightedCandidate> pool = new List<WeightedCandidate>();

        if (candidates == null)
        {
            return pool;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            WantedPosterEnemyCandidate candidate = candidates[i];

            if (candidate != null && candidate.Archetype != null && candidate.Weight > 0f &&
                candidate.Archetype.MonsterGrade == grade)
            {
                pool.Add(new WeightedCandidate(candidate.Archetype, candidate.Weight));
            }
        }

        pool.Sort((left, right) => string.CompareOrdinal(left.Archetype.ContentId, right.Archetype.ContentId));
        return pool;
    }

    // 고정 해상도 정수 Roll을 Weight 누적 구간에 매핑해 결정적으로 하나를 선택한다.
    private static EnemyArchetypeSO SelectWeighted(IReadOnlyList<WeightedCandidate> candidates, ref DungeonPlanRandom random)
    {
        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += candidates[i].Weight;
        }

        int roll = random.Next(WeightResolution);
        float target = totalWeight * roll / WeightResolution;
        float cumulative = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += candidates[i].Weight;

            if (target < cumulative)
            {
                return candidates[i].Archetype;
            }
        }

        return candidates[candidates.Count - 1].Archetype;
    }

    private readonly struct WeightedCandidate
    {
        public readonly EnemyArchetypeSO Archetype;
        public readonly float Weight;

        public WeightedCandidate(EnemyArchetypeSO archetype, float weight)
        {
            Archetype = archetype;
            Weight = weight;
        }
    }
}
