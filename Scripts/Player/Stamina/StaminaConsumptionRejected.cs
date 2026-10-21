/// <summary>
/// 잔량 부족으로 거부된 스태미나 소비 요청을 나타낸다.
/// </summary>
public readonly struct StaminaConsumptionRejected
{
    public readonly float Cost;
    public readonly float CurrentStamina;
    public readonly float MaxStamina;

    public StaminaConsumptionRejected(float cost, float currentStamina, float maxStamina)
    {
        Cost = cost;
        CurrentStamina = currentStamina;
        MaxStamina = maxStamina;
    }
}
