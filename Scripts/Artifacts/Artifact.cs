using UnityEngine;

/// <summary>
/// Artifact 공급자 하나의 장착·해제 수명주기만 담당하는 Component(stats-implementation-plan.md 3장 단계 A).
/// 자신이 등록한 StatModifierHandle 하나만 보관하며, 대상 Player·Enemy 내부 필드를 직접 변경하지 않는다.
/// 영구 장착 정책(PM 결정): 명시적으로 Unequip을 호출하기 전까지 Modifier를 유지한다.
/// 획득·인벤토리·UI·저장은 이 범위가 아니다
/// </summary>
public sealed class Artifact : MonoBehaviour
{
    private RuntimeStats _targetRuntimeStats;
    private StatModifierHandle? _handle;

    public bool IsEquipped => _handle.HasValue;

    /// <summary>
    /// 대상 Stats에 definition의 Modifier를 등록한다. 이미 장착 중이면 먼저 Unequip한 뒤 새로 등록한다.
    /// 대상 Stats가 없거나 RuntimeStats가 아직 초기화되지 않았으면 아무 효과 없이 반환한다(기존 Player 연결 패턴과 동일)
    /// </summary>
    public void Equip(Stats targetStats, ArtifactDefinitionSO definition)
    {
        if (definition == null)
        {
            throw new System.ArgumentNullException(nameof(definition));
        }

        if (IsEquipped)
        {
            Unequip();
        }

        if (targetStats == null || targetStats.RuntimeStats == null)
        {
            return;
        }

        _targetRuntimeStats = targetStats.RuntimeStats;
        _handle = _targetRuntimeStats.AddModifier(new StatModifier(StatType.MoveSpeed, definition.ModifierType, definition.Value));
    }

    /// <summary>
    /// 등록해 둔 Modifier를 제거한다. 장착 중이 아니면 예외 없이 무시한다(RemoveModifier와 동일한 멱등 계약)
    /// </summary>
    public void Unequip()
    {
        if (!_handle.HasValue)
        {
            return;
        }

        _targetRuntimeStats.RemoveModifier(_handle.Value);
        _handle = null;
        _targetRuntimeStats = null;
    }
}
