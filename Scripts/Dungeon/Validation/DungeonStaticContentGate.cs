using System.Collections.Generic;

/// <summary>
/// Dungeon 입장 전 Database 참조 그래프와 선택한 WantedPoster가 참조하는 모든 Enemy Prefab의 Pool 등록
/// 여부를 한 번에 확인한다. 일반 배치 Enemy 수는 생성 결과(Spawn Point 수)가 결정하지만, 어떤 Enemy가
/// 등장할 수 있는지는 WantedPoster가 소유하므로 Poster 기준으로 확인한다.
/// </summary>
public static class DungeonStaticContentGate
{
    // Database 그래프 검증과 Enemy Prefab Pool 등록 확인을 하나의 결과로 합친다.
    public static DungeonDataValidationResult Validate(
        GameDatabaseSO database,
        PoolManager poolManager,
        WantedPosterSO poster)
    {
        DungeonDataValidationResult result = DungeonDataValidator.Validate(database);
        DungeonDataValidationResult progressionResult = ProgressionDataValidator.Validate(database);

        for (int i = 0; i < progressionResult.Errors.Count; i++)
        {
            result.AddError(progressionResult.Errors[i].Message, progressionResult.Errors[i].Context);
        }

        ValidateEnemyPrefabRegistration(poster, poolManager, result);

        return result;
    }

    // 선택한 WantedPoster가 참조하는 모든 Enemy(일반 후보·Target·Boss)의 Prefab이 Pool에 등록됐는지 확인한다.
    private static void ValidateEnemyPrefabRegistration(
        WantedPosterSO poster,
        PoolManager poolManager,
        DungeonDataValidationResult result)
    {
        if (poster == null || poolManager == null)
        {
            return;
        }

        HashSet<EnemyArchetypeSO> checkedArchetypes = new HashSet<EnemyArchetypeSO>();

        if (poster.EnemyCandidates != null)
        {
            for (int i = 0; i < poster.EnemyCandidates.Count; i++)
            {
                ValidateEnemyPrefab(poster.EnemyCandidates[i]?.Archetype, poster, poolManager, checkedArchetypes, result);
            }
        }

        ValidateEnemyPrefab(poster.TargetEnemyArchetype, poster, poolManager, checkedArchetypes, result);
        ValidateEnemyPrefab(poster.BossEnemyArchetype, poster, poolManager, checkedArchetypes, result);
    }

    // Enemy Archetype 하나의 Prefab이 Pool에 등록됐는지 확인한다.
    private static void ValidateEnemyPrefab(
        EnemyArchetypeSO archetype,
        WantedPosterSO poster,
        PoolManager poolManager,
        HashSet<EnemyArchetypeSO> checkedArchetypes,
        DungeonDataValidationResult result)
    {
        if (archetype == null || !checkedArchetypes.Add(archetype))
        {
            return;
        }

        if (!poolManager.IsPrefabRegistered(archetype.EnemyPrefab))
        {
            result.AddError(
                $"Enemy '{archetype.ContentId}'(수배서 '{poster.ContentId}')의 Prefab이 PoolManager에 등록되지 않았습니다.",
                archetype);
        }
    }
}
