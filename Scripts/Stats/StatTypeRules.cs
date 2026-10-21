using System;

/// <summary>
/// 능력치별 값 종류, 기본값 범위, 비율 보정 허용 여부와 최종값 범위를 정의한다.
/// 잘못된 제작 데이터는 예외로 드러내고 런타임 보정 결과만 안전 범위로 제한한다.
/// </summary>
public static class StatTypeRules
{
    // 능력치에 적용할 수 있는 수치 형식을 반환한다.
    public static StatValueKind GetValueKind(StatType statType)
    {
        switch (statType)
        {
            case StatType.MaxHealth:
            case StatType.ShieldStackCount:
                return StatValueKind.Integer;
            case StatType.MoveSpeed:
            case StatType.FireInterval:
            case StatType.ProjectileSpeed:
            case StatType.DamageMultiplier:
            case StatType.CriticalChance:
            case StatType.CriticalMultiplier:
            case StatType.Range:
            case StatType.PierceCount:
            case StatType.FiringMoveSpeedMultiplier:
            case StatType.WeakPointDamageMultiplier:
            case StatType.WeakPointReveal:
            case StatType.Attack:
            case StatType.KnockbackResistance:
            case StatType.MaxStamina:
            case StatType.StaminaRecoveryRate:
            case StatType.DodgeDistance:
            case StatType.DodgeStaminaCost:
            case StatType.DodgeInvulnerabilityDuration:
            case StatType.ParryWindow:
            case StatType.ParryStaminaCost:
                return StatValueKind.Float;
            default:
                throw new ArgumentOutOfRangeException(nameof(statType), statType, $"{nameof(StatTypeRules)}: 정의되지 않은 StatType입니다.");
        }
    }

    // 기본값이 능력치별 제작 규칙을 만족하는지 확인한다.
    public static bool IsValidBaseValue(StatType statType, float value)
    {
        switch (statType)
        {
            case StatType.MaxHealth:
            case StatType.MaxStamina:
                return value > 0f;
            case StatType.MoveSpeed:
            case StatType.ProjectileSpeed:
            case StatType.DamageMultiplier:
            case StatType.CriticalMultiplier:
            case StatType.WeakPointDamageMultiplier:
            case StatType.WeakPointReveal:
            case StatType.Attack:
            case StatType.ShieldStackCount:
            case StatType.StaminaRecoveryRate:
            case StatType.DodgeStaminaCost:
            case StatType.DodgeInvulnerabilityDuration:
            case StatType.ParryStaminaCost:
                return value >= 0f;
            case StatType.FireInterval:
            case StatType.DodgeDistance:
            case StatType.ParryWindow:
                return value > 0f;
            case StatType.CriticalChance:
                return value >= 0f && value <= 1f;
            case StatType.Range:
                return value > 0f;
            case StatType.PierceCount:
                return value >= 0f;
            case StatType.FiringMoveSpeedMultiplier:
                // 0은 완전 정지, 1은 감속 없음을 뜻하므로 기본값을 이 범위로 제한한다.
                return value >= 0f && value <= 1f;
            case StatType.KnockbackResistance:
                // 0은 완전 저항, 1은 저항 없음을 뜻한다.
                return value >= 0f && value <= 1f;
            default:
                throw new ArgumentOutOfRangeException(nameof(statType), statType, $"{nameof(StatTypeRules)}: 정의되지 않은 StatType입니다.");
        }
    }

    // 잘못된 제작 값을 자동 보정하지 않도록 허용 범위를 벗어나면 예외를 던진다.
    public static void ValidateBaseValue(StatType statType, float value)
    {
        if (IsValidBaseValue(statType, value))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, $"{statType}의 base 값({value})이 허용 범위를 벗어났습니다.");
    }

    // 정수 능력치는 비율 계산의 반올림 오차를 피하기 위해 고정 보정만 허용한다.
    public static bool SupportsPercentModifier(StatType statType)
    {
        return GetValueKind(statType) != StatValueKind.Integer;
    }

    // 런타임 보정으로 계산된 값을 능력치가 안전하게 소비할 수 있는 범위로 제한한다.
    public static float ClampFinalValue(StatType statType, float value)
    {
        switch (statType)
        {
            case StatType.MaxHealth:
                return value < 1f ? 1f : value;
            case StatType.MaxStamina:
                // 최대 자원량이 의미를 잃지 않도록 최소 1을 유지한다.
                return value < 1f ? 1f : value;
            case StatType.MoveSpeed:
            case StatType.ProjectileSpeed:
            case StatType.DamageMultiplier:
            case StatType.CriticalMultiplier:
            case StatType.WeakPointDamageMultiplier:
            case StatType.WeakPointReveal:
            case StatType.Attack:
            case StatType.ShieldStackCount:
            case StatType.StaminaRecoveryRate:
            case StatType.DodgeDistance:
            case StatType.DodgeStaminaCost:
            case StatType.DodgeInvulnerabilityDuration:
            case StatType.ParryWindow:
            case StatType.ParryStaminaCost:
                return value < 0f ? 0f : value;
            case StatType.FireInterval:
                // 지나치게 짧은 간격으로 발생할 수 있는 발사 폭주를 막는다.
                return value < 0.05f ? 0.05f : value;
            case StatType.CriticalChance:
                // 확률로 해석할 수 있도록 0과 1 사이로 제한한다.
                if (value < 0f) return 0f;
                return value > 1f ? 1f : value;
            case StatType.Range:
            case StatType.PierceCount:
                // 소비 시스템의 거부 정책은 유지하고 음수 결과만 차단한다.
                return value < 0f ? 0f : value;
            case StatType.FiringMoveSpeedMultiplier:
            case StatType.KnockbackResistance:
                // 배율의 의미가 뒤집히거나 증폭되지 않도록 0과 1 사이로 제한한다.
                if (value < 0f) return 0f;
                return value > 1f ? 1f : value;
            default:
                throw new ArgumentOutOfRangeException(nameof(statType), statType, $"{nameof(StatTypeRules)}: 정의되지 않은 StatType입니다.");
        }
    }

    /// <summary>
    /// 관통 수의 실수 계산 결과를 음수가 아닌 최종 정수로 변환한다.
    /// 모든 소비자가 같은 반올림 정책을 사용하도록 정수 변환을 이 메서드에 모은다.
    /// </summary>
    public static int ResolvePierceCount(float finalValue)
    {
        float clamped = ClampFinalValue(StatType.PierceCount, finalValue);
        int rounded = (int)Math.Floor(clamped + 0.5f);
        return rounded < 0 ? 0 : rounded;
    }
}
