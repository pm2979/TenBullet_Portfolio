using UnityEngine;

/// <summary>
/// 시작 체력·현재 체력과 피해·회복·자원 소모 규칙을 담당하는 순수 C# 클래스.
/// 무적 판정·피격 연출·이벤트 발행 등 Unity 경계는 Health(MonoBehaviour)가 담당한다
/// </summary>
public sealed class HealthState
{
    public int StartingHealth { get; private set; }
    public int CurrentHealth { get; private set; }

    /// <summary>
    /// 회복 상한이자 Heal 백분율 계산 기준이 되는 최대 체력.
    /// SetStartingHealth 호출 시 StartingHealth와 같은 값으로 초기화되며,
    /// 이후 SetMaxHealth로 StartingHealth와 독립적으로 바뀔 수 있다(예: Stats의 Final MaxHealth 연동)
    /// </summary>
    public int MaxHealth { get; private set; }

    /// <summary>
    /// SetStartingHealth가 유효한 값으로 호출되어 시작 체력이 확정되었는지 여부.
    /// false인 동안은 체력 비율(healthPercent)을 계산할 수 없다
    /// </summary>
    public bool HasStartingHealth { get; private set; }

    /// <summary>
    /// 시작 체력을 설정하고 현재 체력·최대 체력을 그 값으로 초기화한다.
    /// startingHealth가 0 이하면 잘못된 설정 데이터로 보고 무시하며 경고를 남긴다.
    /// 반환값은 실제로 값이 적용되었는지 여부다
    /// </summary>
    public bool SetStartingHealth(int startingHealth)
    {
        if (startingHealth <= 0)
        {
            Logger.LogWarning($"{nameof(HealthState)}.{nameof(SetStartingHealth)}: startingHealth({startingHealth})가 0 이하여서 무시합니다.");
            return false;
        }

        StartingHealth = startingHealth;
        CurrentHealth = startingHealth;
        MaxHealth = startingHealth;
        HasStartingHealth = true;

        return true;
    }

    /// <summary>
    /// 최대 체력을 StartingHealth와 독립적으로 변경한다(Stats의 Final MaxHealth 연동 등).
    /// newMaxHealth가 0 이하면 잘못된 값으로 보고 무시하며 경고를 남긴다.
    /// 증가 시에는 증가량만큼 CurrentHealth도 회복하고(새 MaxHealth를 넘지 않음),
    /// 감소 시에는 CurrentHealth가 새 MaxHealth를 넘지 않도록 Clamp만 한다.
    /// 반환값은 실제로 값이 적용되었는지 여부다
    /// </summary>
    public bool SetMaxHealth(int newMaxHealth)
    {
        if (newMaxHealth <= 0)
        {
            Logger.LogWarning($"{nameof(HealthState)}.{nameof(SetMaxHealth)}: newMaxHealth({newMaxHealth})가 0 이하여서 무시합니다.");
            return false;
        }

        int delta = newMaxHealth - MaxHealth;
        MaxHealth = newMaxHealth;

        if (delta > 0)
        {
            CurrentHealth += delta;
        }

        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }

        return true;
    }

    /// <summary>
    /// 피해를 현재 체력에 반영한다. 0 이하나 시작 체력을 넘는 과잉 피해도 그대로 반영한다(클램프하지 않는다)
    /// </summary>
    public void ApplyDamage(int damageAmount)
    {
        CurrentHealth -= damageAmount;
    }

    /// <summary>
    /// 피격 무적 판정 없이 체력을 소모한다(예: 총알 대신 체력을 사용하는 발사 비용)
    /// </summary>
    public void ConsumeHealth(int amount)
    {
        CurrentHealth -= amount;
    }

    /// <summary>
    /// 지정된 비율만큼 체력을 회복하되 최대 체력을 넘지 않는다
    /// </summary>
    public void AddHealth(int healthPercent)
    {
        int healthIncrease = Mathf.RoundToInt((MaxHealth * healthPercent) / 100f);
        int totalHealth = CurrentHealth + healthIncrease;

        CurrentHealth = totalHealth > MaxHealth ? MaxHealth : totalHealth;
    }

    /// <summary>
    /// 지정된 절대값만큼 체력을 회복하되 최대 체력을 넘지 않는다.
    /// AddHealth의 백분율 의미와 섞지 않기 위한 별도 진입점이며, 실제로 적용된 증가량을 반환한다
    /// </summary>
    public int RestoreHealth(int amount)
    {
        int previousHealth = CurrentHealth;
        int totalHealth = CurrentHealth + amount;

        CurrentHealth = totalHealth > MaxHealth ? MaxHealth : totalHealth;

        return CurrentHealth - previousHealth;
    }
}
