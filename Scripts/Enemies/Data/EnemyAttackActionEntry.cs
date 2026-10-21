using UnityEngine;

/// <summary>공격 출력 데이터와 실행 Trigger를 연결하는 직렬화 항목</summary>
[System.Serializable]
public sealed class EnemyAttackActionEntry
{
    [Tooltip("이 출력을 실행하는 시점")]
    [SerializeField] private EnemyAttackActionTrigger _trigger = EnemyAttackActionTrigger.OnActiveStart;
    [Tooltip("Trigger가 OnActiveStart가 아닐 때만 사용하는 Animation Action Marker ID")]
    [SerializeField] private int _markerId;
    [Tooltip("실행할 출력 설정")]
    [SerializeField] private EnemyAttackActionSO _action;

    public EnemyAttackActionTrigger Trigger => _trigger;
    public int MarkerId => _markerId;
    public EnemyAttackActionSO Action => _action;
}
