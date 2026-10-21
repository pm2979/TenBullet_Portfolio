using UnityEngine;

[CreateAssetMenu(fileName = "MovementData_", menuName = "Scriptable Objects/Movement/MovementData")]
public class MovementDataSO : ScriptableObject
{
    [Header("이동 정보")]
    [Tooltip("기본 이동 속도")]
    [SerializeField] private float _moveSpeed = 8f;
    [Tooltip("구르기(Dodge) 이동이 있다면 그때의 구르기 거리")]
    [SerializeField] private float _dodgeDistance;
    [Tooltip("구르기 이동에 사용하는 고정 시간(초). 거리에 따라 변하지 않는다")]
    [SerializeField] private float _dodgeDuration;
    [Tooltip("구르기 1회 시작에 필요한 Stamina 비용")]
    [SerializeField] private float _dodgeStaminaCost;
    [Tooltip("구르기 시작 후 무적 상태를 유지하는 시간(초)")]
    [SerializeField] private float _dodgeInvulnerabilityDuration;

    public float MoveSpeed => _moveSpeed;
    public float DodgeDistance => _dodgeDistance;
    public float DodgeDuration => _dodgeDuration;
    public float DodgeStaminaCost => _dodgeStaminaCost;
    public float DodgeInvulnerabilityDuration => _dodgeInvulnerabilityDuration;
}
