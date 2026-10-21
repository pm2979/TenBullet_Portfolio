/// <summary>
/// 능력치 정의 에셋의 값이 런타임 계산 규칙에 맞는지 검사한다.
/// 잘못된 제작 데이터를 자동 수정하지 않고 호출자가 확인할 수 있는 오류로 수집한다.
/// </summary>
public static class StatsSOValidator
{
    // 정의 에셋 하나의 모든 기본 능력치를 검사해 오류를 한 결과에 모은다.
    public static StatsValidationResult Validate(StatsSO statsSO)
    {
        StatsValidationResult result = new StatsValidationResult();

        if (statsSO == null)
        {
            result.AddError("StatsSO가 null입니다.");
            return result;
        }

        if (!StatTypeRules.IsValidBaseValue(StatType.MaxHealth, statsSO.MaxHealth))
        {
            result.AddError($"{nameof(StatType.MaxHealth)}({statsSO.MaxHealth})가 허용 범위를 벗어났습니다.", statsSO);
        }

        if (!StatTypeRules.IsValidBaseValue(StatType.MoveSpeed, statsSO.MoveSpeed))
        {
            result.AddError($"{nameof(StatType.MoveSpeed)}({statsSO.MoveSpeed})가 허용 범위를 벗어났습니다.", statsSO);
        }

        if (statsSO.ShieldStackCount < 0)
        {
            result.AddError($"{nameof(StatsSO.ShieldStackCount)}({statsSO.ShieldStackCount})는 0 이상이어야 합니다.", statsSO);
        }

        if (!StatTypeRules.IsValidBaseValue(StatType.KnockbackResistance, statsSO.KnockbackResistance))
        {
            result.AddError($"{nameof(StatType.KnockbackResistance)}({statsSO.KnockbackResistance})가 허용 범위를 벗어났습니다.", statsSO);
        }

        return result;
    }
}
