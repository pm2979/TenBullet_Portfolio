using UnityEngine;

/// <summary>
/// Artifact 하나가 장착 시 Player MoveSpeed에 적용할 Modifier 정의(stats-implementation-plan.md §2 PM 결정).
/// 획득·인벤토리·저장은 이 시스템의 범위가 아니며, 장착·해제 시점의 Modifier 등록·제거만 다룬다
/// </summary>
[CreateAssetMenu(fileName = "ArtifactDefinition_", menuName = "Scriptable Objects/Artifacts/ArtifactDefinition")]
public sealed class ArtifactDefinitionSO : ScriptableObject
{
    [Tooltip("장착 시 Player MoveSpeed에 적용할 Modifier 종류")]
    [SerializeField] private StatModifierType _modifierType;
    [Tooltip("장착 시 Player MoveSpeed에 적용할 Modifier 값")]
    [SerializeField] private float _value;

    public StatModifierType ModifierType => _modifierType;
    public float Value => _value;
}
