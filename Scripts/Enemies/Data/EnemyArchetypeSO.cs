using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Enemy 한 종류의 식별·행동·순찰·후퇴·공격·Boss Phase 설정을 정의</summary>
// 공유 정적 자산이므로 체력·Target·쿨다운 같은 인스턴스 런타임 상태 저장 금지
[CreateAssetMenu(fileName = "EnemyArchetype_", menuName = "Scriptable Objects/Enemies/EnemyArchetype")]
public sealed class EnemyArchetypeSO : ScriptableObject
{
    [Space(5)]
    [Header("콘텐츠 식별")]
    [Tooltip("세이브·참조에 사용할 안정적인 콘텐츠 ID. Address와 별개로 관리한다")]
    [SerializeField] private string _contentId;
    [Tooltip("Pool에 등록해 사용할 Enemy Prefab")]
    [SerializeField] private GameObject _enemyPrefab;
    [Tooltip("데이터·Spawn·Inspector 분류용 등급. 공격 실행 분기 키로 사용하지 않는다")]
    [SerializeField] private MonsterGrade _monsterGrade;
    [Space(5)]
    [Header("행동 설정")]
    [Tooltip("피격 후 무적 시간 활성화 여부")]
    [SerializeField] private bool _isHitInvulnerabilityEnabled;
    [Tooltip("피격 후 무적 지속 시간(초)")]
    [SerializeField] private float _hitInvulnerabilityDuration;
    [Tooltip("타겟을 탐지하는 거리")]
    [SerializeField] private float _detectionRange;
    [Tooltip("감지한 타겟의 시야를 계속 확인할 최대 거리. 0이면 기존 Asset 호환을 위해 탐지 거리로 폴백한다")]
    [SerializeField] private float _trackingSightRange;
    [Tooltip("최초 감지를 확인할 주기(초). 0 이하는 데이터 오류다")]
    [SerializeField] private float _detectionInterval;
    [Tooltip("감지 후 전투 판단을 반복할 주기(초). 0 이하는 데이터 오류다")]
    [FormerlySerializedAs("_decisionInterval")]
    [SerializeField] private float _combatDecisionInterval;

    [Space(5)]
    [Header("Patrol")]
    [Tooltip("감지 전 Idle만 유지할지(false), Idle과 Patrol을 오갈지(true). 아래 값들은 true일 때만 사용한다")]
    [SerializeField] private bool _usePatrol;
    [Tooltip("Spawn 위치를 중심으로 Patrol 목적지를 고를 반경. UsePatrol이면 0보다 커야 한다")]
    [SerializeField] private float _patrolRadius = 3f;
    [Tooltip("Patrol 목적지 도착 후 다음 목적지를 고르기까지 기다릴 최소 시간(초)")]
    [SerializeField] private float _patrolIdleDurationMin = 1f;
    [Tooltip("같은 대기의 최대 시간(초). 최소 시간보다 작을 수 없다")]
    [SerializeField] private float _patrolIdleDurationMax = 3f;
    [Tooltip("Patrol 이동에 적용할 최종 MoveSpeed 배율. UsePatrol이면 0보다 커야 한다")]
    [SerializeField] private float _patrolMoveSpeedMultiplier = 0.5f;
    [Tooltip("한 번의 목적지 선택에서 허용할 최대 후보 시도 횟수. 모두 실패하면 이동하지 않고 다음 기회에 재시도한다")]
    [Min(1)]
    [SerializeField] private int _patrolTargetMaxAttempts = 4;

    [Space(5)]
    [Header("Reposition")]
    [Tooltip("공격 최소 사거리 안에서 제한된 후퇴를 허용할지 여부. 기존 Asset은 기본적으로 허용하지 않는다")]
    [SerializeField] private bool _allowReposition;
    [Tooltip("한 번의 후퇴가 지속될 수 있는 최대 시간(초)")]
    [SerializeField] private float _repositionMaxDuration = 0.4f;
    [Tooltip("후퇴 종료 후 다시 후퇴할 수 있을 때까지의 대기 시간(초)")]
    [SerializeField] private float _repositionCooldown = 2f;
    [Tooltip("후퇴 이동에 적용할 최종 MoveSpeed 배율")]
    [SerializeField] private float _repositionMoveSpeedMultiplier = 0.8f;
    [Tooltip("공격 최소 사거리 바깥에 후퇴 목적지를 둘 추가 거리")]
    [SerializeField] private float _repositionDistanceMargin = 0.5f;

    [Space(5)]
    [Header("Encounter")]
    [Tooltip("Encounter Budget 소비 시 사용할 Encounter Cost. 0보다 커야 한다")]
    [Min(1)]
    [SerializeField] private int _encounterCost = 1;

    [Space(5)]
    [Header("공격 카탈로그")]
    [Tooltip("이 Enemy가 보유한 전체 공격 후보")]
    [SerializeField] private EnemyAttackDefinitionSO[] _attackCatalog;
    [Tooltip("Spawn 초기화 시점에 곧바로 선택 가능한 공격 목록. attackCatalog의 부분집합이어야 한다")]
    [SerializeField] private EnemyAttackDefinitionSO[] _initiallyUnlockedPatterns;

    [Space(10)]
    [Header("Boss 전용")]
    [Tooltip("Boss가 아니면 비워 둔다. HP 임계값에 따라 순서대로 적용되는 Phase 목록")]
    [SerializeField] private BossPhaseDefinition[] _bossPhases;

    public string ContentId => _contentId;
    public GameObject EnemyPrefab => _enemyPrefab;
    public MonsterGrade MonsterGrade => _monsterGrade;
    public bool IsHitInvulnerabilityEnabled => _isHitInvulnerabilityEnabled;
    public float HitInvulnerabilityDuration => _hitInvulnerabilityDuration;
    public float DetectionRange => _detectionRange;
    public float TrackingSightRange => _trackingSightRange;
    public float DetectionInterval => _detectionInterval;
    public float CombatDecisionInterval => _combatDecisionInterval;
    public bool UsePatrol => _usePatrol;
    public float PatrolRadius => _patrolRadius;
    public float PatrolIdleDurationMin => _patrolIdleDurationMin;
    public float PatrolIdleDurationMax => _patrolIdleDurationMax;
    public float PatrolMoveSpeedMultiplier => _patrolMoveSpeedMultiplier;
    public int PatrolTargetMaxAttempts => _patrolTargetMaxAttempts;
    public bool AllowReposition => _allowReposition;
    public float RepositionMaxDuration => _repositionMaxDuration;
    public float RepositionCooldown => _repositionCooldown;
    public float RepositionMoveSpeedMultiplier => _repositionMoveSpeedMultiplier;
    public float RepositionDistanceMargin => _repositionDistanceMargin;
    public int EncounterCost => _encounterCost;
    public IReadOnlyList<EnemyAttackDefinitionSO> AttackCatalog => _attackCatalog;
    public IReadOnlyList<EnemyAttackDefinitionSO> InitiallyUnlockedPatterns => _initiallyUnlockedPatterns;
    public IReadOnlyList<BossPhaseDefinition> BossPhases => _bossPhases;
}
