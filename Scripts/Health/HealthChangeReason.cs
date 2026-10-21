/// <summary>
/// 체력 변경이 발생한 원인을 나타내는 열거형
/// </summary>
public enum HealthChangeReason
{
    Initialize,
    Damage,
    ResourceCost,
    Heal,
    MaxHealthChanged
}
