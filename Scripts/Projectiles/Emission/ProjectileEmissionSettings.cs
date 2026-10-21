using UnityEngine;

/// <summary>
/// 한 번의 발사에 사용할 투사체 정의와 발수·간격·분산각을 묶은 계획 입력입니다.
/// </summary>
[System.Serializable]
public class ProjectileEmissionSettings
{
    public ProjectileDataSO Definition;
    public GameObject[] ProjectilePrefabs;
    public int SpawnCountMin = 1;
    public int SpawnCountMax = 1;
    public float SpawnIntervalMin = 0f;
    public float SpawnIntervalMax = 0f;

    /// <summary>첫 탄부터 마지막 탄까지 중심 조준각을 기준으로 펼칠 전체 각도입니다.</summary>
    public float SpreadAngle = 0f;

    /// <summary>
    /// 적 공격 설정의 발수와 간격을 계획 입력으로 변환합니다.
    /// 적의 방향 패턴은 호출자가 별도로 계산하므로 분산각은 적용하지 않습니다.
    /// </summary>
    public static ProjectileEmissionSettings FromEnemyAction(ProjectileAttackActionSO action)
    {
        return new ProjectileEmissionSettings
        {
            Definition = action.ProjectileDefinition,
            ProjectilePrefabs = action.ProjectileDefinition.projectilePrefabArray,
            SpawnCountMin = action.SpawnCountMin,
            SpawnCountMax = action.SpawnCountMax,
            SpawnIntervalMin = action.SpawnIntervalMin,
            SpawnIntervalMax = action.SpawnIntervalMax,
        };
    }

    /// <summary>
    /// 무기의 발수·간격·분산각과 투사체의 프리팹을 하나의 계획 입력으로 변환합니다.
    /// </summary>
    public static ProjectileEmissionSettings FromWeapon(WeaponDataSO weaponDetails, ProjectileDataSO definition)
    {
        return new ProjectileEmissionSettings
        {
            Definition = definition,
            ProjectilePrefabs = definition.projectilePrefabArray,
            SpawnCountMin = weaponDetails.weaponProjectileCount,
            SpawnCountMax = weaponDetails.weaponProjectileCount,
            SpawnIntervalMin = weaponDetails.weaponSpawnIntervalMin,
            SpawnIntervalMax = weaponDetails.weaponSpawnIntervalMax,
            SpreadAngle = weaponDetails.weaponSpreadAngle,
        };
    }
}
