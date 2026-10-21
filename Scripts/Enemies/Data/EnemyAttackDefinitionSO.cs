using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy 공격 하나의 선택 조건·단계 진행·쿨다운·출력 구성을 정의</summary>
// 여러 Enemy가 공유하는 정적 자산이므로 실행 단계와 쿨다운 상태 저장 금지
[CreateAssetMenu(fileName = "EnemyAttackDefinition_", menuName = "Scriptable Objects/Enemies/EnemyAttackDefinition")]
public sealed class EnemyAttackDefinitionSO : ScriptableObject
{
    [Space(5)]
    [Header("거리·시야")]
    [Tooltip("공격을 시작할 수 있는 최소 거리")]
    [SerializeField] private float _minRange;
    [Tooltip("공격을 시작할 수 있는 최대 거리")]
    [SerializeField] private float _maxRange;
    [Tooltip("공격 시작에 타겟과의 시야(장애물 미차단)가 필요한지 여부")]
    [SerializeField] private bool _requiresLineOfSight;

    [Space(5)]
    [Header("단계 시간")]
    [Tooltip("Windup 단계 지속 시간(초)")]
    [SerializeField] private float _windupDuration;
    [Tooltip("Active 단계 지속 시간(초)")]
    [SerializeField] private float _activeDuration;
    [Tooltip("Recovery 단계 지속 시간(초)")]
    [SerializeField] private float _recoveryDuration;
    [Tooltip("Marker·ActionCompleted 진행 방식에서 신호가 오지 않아 단계가 끝나지 않을 때 Failed로 강제 종료하는 안전 타임아웃(초). 해당 방식을 쓰면 0보다 커야 한다")]
    [SerializeField] private float _safeTimeout;

    [Space(5)]
    [Header("쿨다운")]
    [Tooltip("이 공격을 다시 선택할 수 있기까지의 쿨다운(초)")]
    [SerializeField] private float _cooldown;

    [Space(5)]
    [Header("진행 방식")]
    [Tooltip("AimSnapshot을 고정하는 시점")]
    [SerializeField] private EnemyAimLockTiming _aimLockTiming = EnemyAimLockTiming.WindupEnd;
    [Tooltip("Windup에서 Active로 넘어가는 방식. Timer 또는 Marker만 사용하며 Marker는 SafeTimeout과 함께 설정한다")]
    [SerializeField] private EnemyAttackAdvanceMode _windupAdvanceMode = EnemyAttackAdvanceMode.Timer;
    [Tooltip("Active에서 Recovery로 넘어가는 방식. 연사처럼 여러 발을 프레임에 걸쳐 내보내는 출력은 마지막 발사 뒤 통지하는 ActionCompleted를 사용한다. Marker·ActionCompleted는 SafeTimeout과 함께 설정한다")]
    [SerializeField] private EnemyAttackAdvanceMode _activeAdvanceMode = EnemyAttackAdvanceMode.Timer;

    [Space(5)]
    [Header("단계별 이동 허용")]
    [Tooltip("Windup 중 이동을 허용할지 여부")]
    [SerializeField] private bool _allowMovementDuringWindup;
    [Tooltip("Active 중 이동을 허용할지 여부")]
    [SerializeField] private bool _allowMovementDuringActive;
    [Tooltip("Recovery 중 이동을 허용할지 여부")]
    [SerializeField] private bool _allowMovementDuringRecovery;

    [Space(5)]
    [Header("출력 구성")]
    [Tooltip("Trigger·Action 조합으로 구성하는 출력 목록")]
    [SerializeField] private EnemyAttackActionEntry[] _actions;

    public float MinRange => _minRange;
    public float MaxRange => _maxRange;
    public bool RequiresLineOfSight => _requiresLineOfSight;
    public float WindupDuration => _windupDuration;
    public float ActiveDuration => _activeDuration;
    public float RecoveryDuration => _recoveryDuration;
    public float SafeTimeout => _safeTimeout;
    public float Cooldown => _cooldown;
    public EnemyAimLockTiming AimLockTiming => _aimLockTiming;
    public EnemyAttackAdvanceMode WindupAdvanceMode => _windupAdvanceMode;
    public EnemyAttackAdvanceMode ActiveAdvanceMode => _activeAdvanceMode;
    public bool AllowMovementDuringWindup => _allowMovementDuringWindup;
    public bool AllowMovementDuringActive => _allowMovementDuringActive;
    public bool AllowMovementDuringRecovery => _allowMovementDuringRecovery;
    public IReadOnlyList<EnemyAttackActionEntry> Actions => _actions;
}
