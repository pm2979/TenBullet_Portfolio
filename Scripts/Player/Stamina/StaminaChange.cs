/// <summary>
/// 스태미나 변경 전후 값과 변경 원인을 전달하는 불변 스냅샷이다.
/// </summary>
public readonly struct StaminaChange
{
    public readonly float PreviousStamina;
    public readonly float CurrentStamina;
    public readonly float MaxStamina;
    public readonly float Delta;
    public readonly StaminaChangeReason Reason;

    public StaminaChange(float previousStamina, float currentStamina, float maxStamina, float delta, StaminaChangeReason reason)
    {
        PreviousStamina = previousStamina;
        CurrentStamina = currentStamina;
        MaxStamina = maxStamina;
        Delta = delta;
        Reason = reason;
    }
}
