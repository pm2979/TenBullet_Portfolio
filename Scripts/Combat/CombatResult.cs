using UnityEngine;

/// <summary>
/// 공격 1회와 대상 1개의 피해 결과 및 공격 문맥을 함께 전달하는 불변 값 타입이다.
/// </summary>
public readonly struct CombatResult
{
    public readonly IDamageable Target;
    public readonly Weapon SourceWeapon;
    public readonly int RequestedDamage;
    public readonly DamageResult DamageResult;
    public readonly bool IsCritical;
    public readonly Vector2 HitPosition;

    // Health의 피해 판정 결과를 공격 문맥과 함께 상위 소비자가 해석할 수 있는 값으로 구성한다.
    public CombatResult(
        IDamageable target,
        Weapon sourceWeapon,
        int requestedDamage,
        DamageResult damageResult,
        bool isCritical,
        Vector2 hitPosition)
    {
        Target = target;
        SourceWeapon = sourceWeapon;
        RequestedDamage = requestedDamage;
        DamageResult = damageResult;
        IsCritical = isCritical;
        HitPosition = hitPosition;
    }
}
