using UnityEngine;

/// <summary>공격 Definition과 같은 Enemy GameObject의 실행 Action을 연결하는 직렬화 데이터</summary>
// 다른 객체의 Action 연결로 Controller 상태가 분리되는 구성을 검증 단계에서 거부
[System.Serializable]
public sealed class EnemyAttackBinding
{
    [Tooltip("실행할 공격의 공유 정적 설정")]
    [SerializeField] private EnemyAttackDefinitionSO _definition;
    [Tooltip("IEnemyAttackAction을 구현한, 이 Binding을 소유한 EnemyAttackController와 같은 GameObject의 Component. " +
             "자식이나 다른 GameObject의 Component는 허용하지 않는다")]
    [SerializeField] private Component _actionComponent;

    public EnemyAttackDefinitionSO Definition => _definition;
    public Component ActionComponent => _actionComponent;
}
