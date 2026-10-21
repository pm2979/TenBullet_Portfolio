/// <summary>
/// 적중 종류와 실제 전투 결과를 함께 전달하는 불변 데이터
/// </summary>
public readonly struct WeaponHitEvent
{
    public readonly WeaponHitKind Kind;
    public readonly CombatResult CombatResult;

    /// <summary>
    /// 적중을 발생시킨 발사 식별자이며 0은 식별자 없음으로 예약
    /// </summary>
    public readonly int ShotId;

    public WeaponHitEvent(WeaponHitKind kind, CombatResult combatResult, int shotId = 0)
    {
        Kind = kind;
        CombatResult = combatResult;
        ShotId = shotId;
    }
}
