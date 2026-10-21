using System;
using System.Collections.Generic;

/// <summary>
/// 능력치 정의의 기본값과 인스턴스별 보정치를 결합해 최종값을 계산한다.
/// 원본 에셋을 변경하지 않으며 플레이어와 적이 같은 계산 규칙을 공유하게 한다.
/// </summary>
public sealed class RuntimeStats
{
    // 조회 비용을 일정하게 유지하도록 능력치별 기본값, 보정 합계, 최종값을 함께 보관한다.
    private sealed class StatEntry
    {
        public float Base;
        public float FlatSum;
        public float PercentSum;
        public float Final;
        // 정의 에셋이 기본값을 소유한 능력치만 GetFinalValue로 직접 조회할 수 있다.
        public bool IsStatsOwned;
    }

    private readonly Dictionary<StatType, StatEntry> _entries = new Dictionary<StatType, StatEntry>();
    private readonly Dictionary<StatModifierHandle, StatModifier> _activeModifiers = new Dictionary<StatModifierHandle, StatModifier>();
    // 기본 구조체 값과 실제 발급값을 구분하기 위해 식별자는 1부터 시작한다.
    private int _nextHandleId = 1;

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 보호막의 최종 시작 중첩 수다.
    /// 정수 능력치의 공통 정책에 따라 고정 보정만 허용하고 가장 가까운 정수로 반올림한다.
    /// </summary>
    public int ShieldStackCount => (int)Math.Floor(GetFinalValue(StatType.ShieldStackCount) + 0.5f);

    /// <summary>
    /// 정의 에셋의 기본값을 복사해 독립적인 런타임 상태를 시작한다.
    /// 같은 인스턴스의 중복 초기화는 기존 보정 상태 손실을 막기 위해 거부한다.
    /// </summary>
    public void Initialize(StatsSO statsSO)
    {
        if (IsInitialized)
        {
            throw new InvalidOperationException($"{nameof(RuntimeStats)}: 이미 초기화된 인스턴스를 다시 초기화할 수 없습니다.");
        }

        if (statsSO == null)
        {
            throw new ArgumentNullException(nameof(statsSO));
        }

        AddEntry(StatType.MaxHealth, statsSO.MaxHealth);
        AddEntry(StatType.MoveSpeed, statsSO.MoveSpeed);
        AddEntry(StatType.ShieldStackCount, statsSO.ShieldStackCount);
        AddEntry(StatType.KnockbackResistance, statsSO.KnockbackResistance);

        IsInitialized = true;
    }

    /// <summary>
    /// 정의 에셋이 기본값을 소유한 능력치의 최종값을 반환한다.
    /// 콘텐츠별 기본값을 사용하는 능력치는 ApplyModifiers로 계산해야 한다.
    /// </summary>
    public float GetFinalValue(StatType statType)
    {
        EnsureInitialized();

        if (!_entries.TryGetValue(statType, out StatEntry entry) || !entry.IsStatsOwned)
        {
            throw new InvalidOperationException($"{nameof(RuntimeStats)}: {statType}은 StatsSO가 base를 소유하지 않습니다. ApplyModifiers를 사용하세요.");
        }

        return entry.Final;
    }

    /// <summary>
    /// 무기나 행동 등 외부 콘텐츠가 소유한 기본값에 현재 보정치를 적용한다.
    /// 보정치가 없어도 능력치별 유효 범위는 동일하게 적용한다.
    /// </summary>
    public float ApplyModifiers(StatType statType, float externalBase)
    {
        EnsureInitialized();
        StatTypeRules.ValidateBaseValue(statType, externalBase);

        if (!_entries.TryGetValue(statType, out StatEntry entry))
        {
            return StatTypeRules.ClampFinalValue(statType, externalBase);
        }

        float raw = (externalBase + entry.FlatSum) * (1f + entry.PercentSum);
        return StatTypeRules.ClampFinalValue(statType, raw);
    }

    /// <summary>
    /// 외부 기본값과 나중에 결합할 수 있도록 고정 보정과 비율 보정의 합계를 분리해 반환한다.
    /// 등록된 보정이 없으면 외부 기본값을 보존하는 0의 합계를 반환한다.
    /// </summary>
    public (float flatSum, float percentSum) GetModifierSums(StatType statType)
    {
        EnsureInitialized();

        if (!_entries.TryGetValue(statType, out StatEntry entry))
        {
            return (0f, 0f);
        }

        return (entry.FlatSum, entry.PercentSum);
    }

    /// <summary>
    /// 보정치를 검증한 뒤 적용하고 해당 보정만 제거할 수 있는 핸들을 반환한다.
    /// 유효하지 않은 입력은 상태를 변경하기 전에 거부한다.
    /// </summary>
    public StatModifierHandle AddModifier(StatModifier modifier)
    {
        EnsureInitialized();
        ValidateModifier(modifier);

        switch (modifier.ModifierType)
        {
            case StatModifierType.Flat:
                ApplyFlatDelta(modifier.StatType, modifier.Value);
                break;
            case StatModifierType.Percent:
                ApplyPercentDelta(modifier.StatType, modifier.Value);
                break;
        }

        StatModifierHandle handle = new StatModifierHandle(_nextHandleId++);
        _activeModifiers[handle] = modifier;

        return handle;
    }

    /// <summary>
    /// 핸들에 대응하는 보정치 하나를 제거하고 영향을 받은 최종값을 갱신한다.
    /// 이미 제거했거나 이 인스턴스가 발급하지 않은 핸들은 상태를 바꾸지 않고 false를 반환한다.
    /// </summary>
    public bool RemoveModifier(StatModifierHandle handle)
    {
        EnsureInitialized();

        if (!_activeModifiers.TryGetValue(handle, out StatModifier modifier))
        {
            return false;
        }

        _activeModifiers.Remove(handle);

        StatEntry entry = GetEntry(modifier.StatType);
        switch (modifier.ModifierType)
        {
            case StatModifierType.Flat:
                entry.FlatSum -= modifier.Value;
                break;
            case StatModifierType.Percent:
                entry.PercentSum -= modifier.Value;
                break;
        }

        Recalculate(modifier.StatType, entry);

        return true;
    }

    // 기본값은 유지하고 모든 보정과 발급 핸들을 제거해 초기 상태로 되돌린다.
    public void Reset()
    {
        EnsureInitialized();

        foreach (KeyValuePair<StatType, StatEntry> pair in _entries)
        {
            pair.Value.FlatSum = 0f;
            pair.Value.PercentSum = 0f;
            Recalculate(pair.Key, pair.Value);
        }

        _activeModifiers.Clear();
        _nextHandleId = 1;
    }

    // 부분 적용을 막기 위해 보정 대상, 방식, 값을 상태 변경 전에 모두 검증한다.
    private static void ValidateModifier(StatModifier modifier)
    {
        if (!float.IsFinite(modifier.Value))
        {
            throw new ArgumentException($"{nameof(StatModifier)}.{nameof(StatModifier.Value)}({modifier.Value})가 유효하지 않습니다.", nameof(modifier));
        }

        // 알 수 없는 능력치가 집계에 들어가는 것을 차단한다.
        StatTypeRules.GetValueKind(modifier.StatType);

        switch (modifier.ModifierType)
        {
            case StatModifierType.Flat:
                break;
            case StatModifierType.Percent:
                if (!StatTypeRules.SupportsPercentModifier(modifier.StatType))
                {
                    throw new NotSupportedException($"{modifier.StatType}은 정수 Percent 반올림 정책이 확정되지 않아 Percent Modifier를 허용하지 않습니다.");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(modifier), modifier.ModifierType, $"{nameof(StatModifierType)}: 정의되지 않은 값입니다.");
        }
    }

    // 잘못된 원본 데이터를 숨기지 않도록 기본값은 보정하지 않고 초기화 단계에서 거부한다.
    private void AddEntry(StatType statType, float baseValue)
    {
        StatTypeRules.ValidateBaseValue(statType, baseValue);

        StatEntry entry = new StatEntry { Base = baseValue, IsStatsOwned = true };
        Recalculate(statType, entry);
        _entries[statType] = entry;
    }

    // 외부 기본값 능력치는 0 기준의 보정 집계 항목을 필요한 시점에 만든다.
    private StatEntry GetEntry(StatType statType)
    {
        EnsureInitialized();

        if (_entries.TryGetValue(statType, out StatEntry entry))
        {
            return entry;
        }

        entry = new StatEntry { Base = 0f };
        _entries[statType] = entry;

        return entry;
    }

    // 고정 보정 합계를 변경하고 해당 능력치의 캐시만 갱신한다.
    private void ApplyFlatDelta(StatType statType, float delta)
    {
        StatEntry entry = GetEntry(statType);
        entry.FlatSum += delta;
        Recalculate(statType, entry);
    }

    // 비율 보정 합계를 변경하고 해당 능력치의 캐시만 갱신한다.
    private void ApplyPercentDelta(StatType statType, float delta)
    {
        StatEntry entry = GetEntry(statType);
        entry.PercentSum += delta;
        Recalculate(statType, entry);
    }

    // 고정 보정 후 비율 보정을 적용하고 능력치별 허용 범위로 최종값을 제한한다.
    private void Recalculate(StatType statType, StatEntry entry)
    {
        float raw = (entry.Base + entry.FlatSum) * (1f + entry.PercentSum);
        entry.Final = StatTypeRules.ClampFinalValue(statType, raw);
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException($"{nameof(RuntimeStats)}: 초기화되기 전에 접근했습니다.");
        }
    }
}
