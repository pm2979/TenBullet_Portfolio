using UnityEngine;

/// <summary>
/// 콘텐츠가 제공한 기본 피해에 공격력, 피해 배율, 치명타 보정을 적용한다.
/// 공격 시작 시 보정치를 고정하여 같은 공격에 포함된 모든 적중이 동일한 능력치 상태를 사용하게 한다.
/// </summary>
public static class CombatDamageMath
{
    // 치명타 확률이 0인 기본 상태에서는 결과에 영향을 주지 않는 예비 배율이다.
    private const float DefaultCriticalMultiplier = 1.5f;

    /// <summary>
    /// 한 공격이 끝날 때까지 재사용할 피해 보정 상태다.
    /// </summary>
    public readonly struct DamageSnapshot
    {
        public readonly float DamageMultiplier;
        public readonly float CriticalChance;
        public readonly float CriticalMultiplier;
        // 공격 중 능력치가 바뀌어도 이미 시작한 공격의 피해가 흔들리지 않도록 합계를 함께 보관한다.
        public readonly float AttackFlatSum;
        public readonly float AttackPercentSum;

        public DamageSnapshot(float damageMultiplier, float criticalChance, float criticalMultiplier, float attackFlatSum = 0f, float attackPercentSum = 0f)
        {
            DamageMultiplier = damageMultiplier;
            CriticalChance = criticalChance;
            CriticalMultiplier = criticalMultiplier;
            AttackFlatSum = attackFlatSum;
            AttackPercentSum = attackPercentSum;
        }
    }

    /// <summary>
    /// 공격자의 현재 능력치로 피해 스냅샷을 만든다.
    /// 능력치 컴포넌트가 없으면 콘텐츠 기본 피해를 유지하는 중립 스냅샷을 반환한다.
    /// </summary>
    public static DamageSnapshot CreateSnapshot(Component attacker)
    {
        Stats stats = attacker != null ? attacker.GetComponent<Stats>() : null;

        return CreateSnapshot(stats != null ? stats.RuntimeStats : null);
    }

    // 런타임 능력치의 피해 관련 보정치를 한 번 읽어 스냅샷으로 고정한다.
    public static DamageSnapshot CreateSnapshot(RuntimeStats runtimeStats)
    {
        if (runtimeStats == null)
        {
            return new DamageSnapshot(1f, 0f, DefaultCriticalMultiplier);
        }

        float damageMultiplier = runtimeStats.ApplyModifiers(StatType.DamageMultiplier, 1f);
        float criticalChance = runtimeStats.ApplyModifiers(StatType.CriticalChance, 0f);
        float criticalMultiplier = runtimeStats.ApplyModifiers(StatType.CriticalMultiplier, DefaultCriticalMultiplier);
        (float attackFlatSum, float attackPercentSum) = runtimeStats.GetModifierSums(StatType.Attack);

        return new DamageSnapshot(damageMultiplier, criticalChance, criticalMultiplier, attackFlatSum, attackPercentSum);
    }

    /// <summary>
    /// 스냅샷의 고정 보정과 비율 보정을 순서대로 적용하고 치명타 여부와 최종 피해를 반환한다.
    /// 모든 실수 연산을 마친 뒤 한 번만 반올림하며, 유효한 적중은 최소 1의 피해를 준다.
    /// </summary>
    public static int ApplyHit(int baseDamage, DamageSnapshot snapshot, out bool isCritical)
    {
        isCritical = Random.value < snapshot.CriticalChance;

        float attackAdjustedBaseDamage = (baseDamage + snapshot.AttackFlatSum) * (1f + snapshot.AttackPercentSum);
        attackAdjustedBaseDamage = StatTypeRules.ClampFinalValue(StatType.Attack, attackAdjustedBaseDamage);

        float multiplier = snapshot.DamageMultiplier * (isCritical ? snapshot.CriticalMultiplier : 1f);
        float finalDamage = attackAdjustedBaseDamage * multiplier;

        return Mathf.Max(1, Mathf.FloorToInt(finalDamage + 0.5f));
    }
}
