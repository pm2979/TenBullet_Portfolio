/// <summary>
/// 하나의 능력치에 적용할 런타임 보정 내용을 나타낸다.
/// 보정 공급자의 수명과 무관하게 값으로 전달되어 RuntimeStats가 등록 상태를 소유한다.
/// </summary>
public readonly struct StatModifier
{
    public readonly StatType StatType;
    public readonly StatModifierType ModifierType;
    public readonly float Value;

    public StatModifier(StatType statType, StatModifierType modifierType, float value)
    {
        StatType = statType;
        ModifierType = modifierType;
        Value = value;
    }
}
