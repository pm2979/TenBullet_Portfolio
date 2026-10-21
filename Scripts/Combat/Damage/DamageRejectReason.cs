/// <summary>
/// 피해 요청을 적용하지 않은 이유를 구분한다.
/// </summary>
public enum DamageRejectReason
{
    None,
    PolicyBlocked,
    Invulnerable,
    SameFaction,
    Parried,
    ShieldBlocked
}
