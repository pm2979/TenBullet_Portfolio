/// <summary>
/// 발사자가 가진 능력치 보정을 콘텐츠 기본값에 적용해 발사 시점의 최종값을 계산합니다.
/// </summary>
public static class ProjectileFireSnapshotResolver
{
    /// <summary>최종 사거리를 계산하며 능력치가 없으면 콘텐츠 기본값을 반환합니다.</summary>
    public static float ResolveRange(UnityEngine.Component attacker, float contentBaseRange)
    {
        RuntimeStats runtimeStats = GetRuntimeStats(attacker);

        return runtimeStats != null ? runtimeStats.ApplyModifiers(StatType.Range, contentBaseRange) : contentBaseRange;
    }

    /// <summary>최종 추가 관통 횟수를 계산하고 게임 규칙에 맞는 정수로 확정합니다.</summary>
    public static int ResolvePierceCount(UnityEngine.Component attacker, int contentBasePierceCount)
    {
        RuntimeStats runtimeStats = GetRuntimeStats(attacker);

        if (runtimeStats == null)
        {
            return contentBasePierceCount;
        }

        float finalValue = runtimeStats.ApplyModifiers(StatType.PierceCount, contentBasePierceCount);

        return StatTypeRules.ResolvePierceCount(finalValue);
    }

    /// <summary>최종 투사체 속도를 계산하며 능력치가 없으면 콘텐츠 기본값을 반환합니다.</summary>
    public static float ResolveProjectileSpeed(UnityEngine.Component attacker, float contentBaseSpeed)
    {
        RuntimeStats runtimeStats = GetRuntimeStats(attacker);

        return runtimeStats != null ? runtimeStats.ApplyModifiers(StatType.ProjectileSpeed, contentBaseSpeed) : contentBaseSpeed;
    }

    /// <summary>발사 중 이동 속도 배율을 계산하며 능력치가 없으면 콘텐츠 기본값을 반환합니다.</summary>
    public static float ResolveFiringMoveSpeedMultiplier(UnityEngine.Component attacker, float contentBaseMultiplier)
    {
        RuntimeStats runtimeStats = GetRuntimeStats(attacker);

        return runtimeStats != null ? runtimeStats.ApplyModifiers(StatType.FiringMoveSpeedMultiplier, contentBaseMultiplier) : contentBaseMultiplier;
    }

    private static RuntimeStats GetRuntimeStats(UnityEngine.Component attacker)
    {
        Stats stats = attacker != null ? attacker.GetComponent<Stats>() : null;

        return stats != null ? stats.RuntimeStats : null;
    }
}
