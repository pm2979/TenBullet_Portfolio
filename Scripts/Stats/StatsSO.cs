using UnityEngine;

/// <summary>
/// 플레이어와 적의 캐릭터별 기본 능력치를 보관하는 정의 에셋이다.
/// 런타임 인스턴스는 값을 복사해 사용하며 이 에셋 자체는 실행 중 변경하지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "StatsDefinition_", menuName = "Scriptable Objects/Stats/StatsDefinition")]
public sealed class StatsSO : ScriptableObject
{
    [Space(5)]
    [Header("기본 능력치")]
    [Tooltip("캐릭터의 최대 체력 기본값. 0보다 커야 한다")]
    [SerializeField] private int _maxHealth = 100;
    [Tooltip("캐릭터의 이동 속도 기본값. 0 이상이어야 한다")]
    [SerializeField] private float _moveSpeed = 8f;
    [Tooltip("전투 시작 시 적용할 보호막 중첩 수. 0이면 보호막 없이 시작한다")]
    [SerializeField] private int _shieldStackCount = 0;
    [Tooltip("넉백 속도에 곱하는 저항 배율. 0이면 완전 저항, 1이면 저항 없음")]
    [SerializeField, Range(0f, 1f)] private float _knockbackResistance = 1f;

    public int MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public int ShieldStackCount => _shieldStackCount;
    public float KnockbackResistance => _knockbackResistance;
}
