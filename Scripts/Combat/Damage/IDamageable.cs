/// <summary>
/// 공격 주체와 피해량을 포함한 피해 요청을 처리하는 대상의 공통 계약이다.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// 이 대상이 속한 진영으로, 자기 자신과 아군 피해를 구분할 때 사용한다.
    /// </summary>
    Faction Faction { get; }

    /// <summary>
    /// 피해 적용을 시도하고 실제 적용 결과 또는 거부 사유를 반환한다.
    /// </summary>
    DamageResult TryTakeDamage(DamageInfo damageInfo);
}
