using System;

// 장착 무기 데이터와 실제 피해가 적용된 적중 통지를 소유
public class Weapon
{
    public WeaponDataSO weaponDetails;

    /// <summary>
    /// 이 무기에서 발생해 실제 적용된 적중 결과 통지
    /// </summary>
    public event Action<WeaponHitEvent> Hit;

    /// <summary>
    /// 출처와 실제 피해 적용을 검증한 적중만 동기 통지
    /// </summary>
    /// <returns>적중 통지 여부</returns>
    public bool TryPublishHit(WeaponHitEvent hitEvent)
    {
        if (!ReferenceEquals(hitEvent.CombatResult.SourceWeapon, this))
        {
            return false;
        }

        if (!hitEvent.CombatResult.DamageResult.WasApplied || hitEvent.CombatResult.DamageResult.AppliedDamage < 1)
        {
            return false;
        }

        Hit?.Invoke(hitEvent);
        return true;
    }
}
