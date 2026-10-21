/// <summary>
/// 체력 변경 한 건의 스냅샷을 나타내는 불변 값 타입.
/// 구독자에게 GC 할당 없이 전달하기 위해 class가 아닌 struct로 정의한다
/// </summary>
public readonly struct HealthChange
{
    public readonly int PreviousHealth;
    public readonly int CurrentHealth;
    public readonly int MaxHealth;
    public readonly int Delta;
    public readonly HealthChangeReason Reason;

    public HealthChange(int previousHealth, int currentHealth, int maxHealth, int delta, HealthChangeReason reason)
    {
        PreviousHealth = previousHealth;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        Delta = delta;
        Reason = reason;
    }
}
