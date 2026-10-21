using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>해금 공격 선택과 Ready·Windup·Active·Recovery 진행 및 종료를 관리</summary>
// 공통 쿨다운·Aim Lock·이동 잠금만 소유하고 공격별 출력 상태는 Action에 위임
[RequireComponent(typeof(EnemyTargetSensor))]
[RequireComponent(typeof(EnemyMovementController))]
[DisallowMultipleComponent]
public sealed class EnemyAttackController : MonoBehaviour
{
    [Tooltip("이 Enemy가 실행 가능한 공격 Binding 목록. Definition은 Archetype의 attackCatalog와 일치해야 한다")]
    [SerializeField] private EnemyAttackBinding[] _bindings;

    private readonly Dictionary<EnemyAttackDefinitionSO, IEnemyAttackAction> _resolvedActions = new Dictionary<EnemyAttackDefinitionSO, IEnemyAttackAction>();
    private readonly Dictionary<EnemyAttackDefinitionSO, float> _cooldownReadyAtTime = new Dictionary<EnemyAttackDefinitionSO, float>();
    private readonly List<EnemyAttackDefinitionSO> _unlockedPatterns = new List<EnemyAttackDefinitionSO>();

    private EnemyTargetSensor _targetSensor;
    private EnemyMovementController _movementController;

    private Transform _caster;
    private Faction _sourceFaction;
    private Component _attacker;

    private EnemyAttackDefinitionSO _currentDefinition;
    private IEnemyAttackAction _currentAction;
    private EnemyAttackContext _currentContext;
    private TargetSnapshot _lastConfirmedTargetSnapshot;
    private float _phaseTimer;

    private EnemyAttackDefinitionSO _lastSucceededPattern;
    private EnemyAttackDefinitionSO _reservedEntryPattern;

    public EnemyAttackPhase CurrentPhase { get; private set; } = EnemyAttackPhase.Ready;
    public bool CanUpdateWeaponAim =>
        CurrentPhase == EnemyAttackPhase.Windup || CurrentPhase == EnemyAttackPhase.Recovery;

    /// <summary>Aim Lock 이후 Recovery 종료까지 확정된 방향 유지 여부</summary>
    public bool IsTargetTransformLocked => HasLockedAim;
    public bool HasLockedAim { get; private set; }
    public AimSnapshot LockedAimSnapshot { get; private set; }
    public EnemyAttackDefinitionSO LastStartedPattern { get; private set; }
    public EnemyAttackDefinitionSO LastSucceededPattern => _lastSucceededPattern;
    public EnemyAttackEndResult LastEndResult { get; private set; } = EnemyAttackEndResult.Completed;

    public event Action<EnemyAttackPhase> PhaseChanged;
    public event Action<Vector2> WeaponAimChanged;

    private void Awake()
    {
        _targetSensor = GetComponent<EnemyTargetSensor>();
        _movementController = GetComponent<EnemyMovementController>();
    }

    private void Update()
    {
        if (CurrentPhase == EnemyAttackPhase.Ready)
        {
            return;
        }

        Tick(Time.deltaTime, _targetSensor.Current);
    }

    /// <summary>Binding 조회 구조와 이번 대여의 최초 해금·쿨다운 상태 초기화</summary>
    public void Initialize(
        EnemyArchetypeSO archetype, Transform caster, Faction sourceFaction, Component attacker)
    {
        ResetRuntimeState();

        _caster = caster;
        _sourceFaction = sourceFaction;
        _attacker = attacker;

        ResolveBindings(archetype);

        IReadOnlyList<EnemyAttackDefinitionSO> initialPatterns = archetype.InitiallyUnlockedPatterns;

        if (initialPatterns == null)
        {
            return;
        }

        for (int i = 0; i < initialPatterns.Count; i++)
        {
            Unlock(initialPatterns[i]);
        }
    }

    private void ResolveBindings(EnemyArchetypeSO archetype)
    {
        if (_bindings == null)
        {
            return;
        }

        HashSet<EnemyAttackDefinitionSO> catalogSet = BuildCatalogSet(archetype);

        for (int i = 0; i < _bindings.Length; i++)
        {
            EnemyAttackBinding binding = _bindings[i];

            if (binding == null || binding.Definition == null || binding.ActionComponent == null)
            {
                Debug.LogError($"EnemyAttackController: '{name}'의 Binding[{i}]에 null Definition 또는 Action이 있습니다.", this);
                continue;
            }

            // 다른 GameObject의 Action 연결로 단계 소유 Controller가 분리되는 구성 거부
            if (binding.ActionComponent.gameObject != gameObject)
            {
                Debug.LogError($"EnemyAttackController: '{name}'의 Binding[{i}] Action이 같은 GameObject의 Component가 아닙니다(자식·다른 Enemy는 허용하지 않습니다).", this);
                continue;
            }

            if (!(binding.ActionComponent is IEnemyAttackAction action))
            {
                Debug.LogError($"EnemyAttackController: '{name}'의 Binding[{i}] Action이 IEnemyAttackAction을 구현하지 않습니다.", this);
                continue;
            }

            if (_resolvedActions.ContainsKey(binding.Definition))
            {
                Debug.LogError($"EnemyAttackController: '{name}'에 같은 Definition '{binding.Definition.name}'이(가) 중복 Binding되어 있습니다.", this);
                continue;
            }

            if (!catalogSet.Contains(binding.Definition))
            {
                Debug.LogError($"EnemyAttackController: '{name}'의 Binding[{i}] Definition '{binding.Definition.name}'이(가) archetype의 attackCatalog 밖입니다.", this);
                continue;
            }

            _resolvedActions.Add(binding.Definition, action);
        }
    }

    private static HashSet<EnemyAttackDefinitionSO> BuildCatalogSet(EnemyArchetypeSO archetype)
    {
        HashSet<EnemyAttackDefinitionSO> catalogSet = new HashSet<EnemyAttackDefinitionSO>();
        IReadOnlyList<EnemyAttackDefinitionSO> catalog = archetype.AttackCatalog;

        if (catalog == null)
        {
            return catalogSet;
        }

        for (int i = 0; i < catalog.Count; i++)
        {
            if (catalog[i] != null)
            {
                catalogSet.Add(catalog[i]);
            }
        }

        return catalogSet;
    }

    /// <summary>유효한 Binding이 있는 공격 후보를 이번 대여에 해금</summary>
    public void Unlock(EnemyAttackDefinitionSO definition)
    {
        if (definition == null || !_resolvedActions.ContainsKey(definition))
        {
            return;
        }

        if (_unlockedPatterns.Contains(definition))
        {
            return;
        }

        _unlockedPatterns.Add(definition);
    }

    /// <summary>다음 선택에서 일반 후보보다 우선 시도할 일회성 Entry 패턴 예약</summary>
    public void ReserveEntryPattern(EnemyAttackDefinitionSO definition)
    {
        if (definition == null || !_resolvedActions.ContainsKey(definition))
        {
            return;
        }

        _reservedEntryPattern = definition;
    }

    /// <summary>상태 변경 없이 현재 Snapshot에서 시작 가능한 공격 존재 여부 확인</summary>
    public bool HasReadyAttack(TargetSnapshot targetSnapshot)
    {
        return FindReadyCandidate(targetSnapshot) != null;
    }

    /// <summary>현재 거리와 가장 가까운 단일 해금 공격 범위를 결정적으로 반환</summary>
    // 분리된 공격 범위 사이의 빈 구간을 합쳐 유효 범위로 오인하지 않도록 개별 범위 비교
    public bool TryGetNearestEngagementRange(float distance, out float minRange, out float maxRange)
    {
        minRange = 0f;
        maxRange = 0f;
        bool found = false;
        float bestGap = float.PositiveInfinity;

        for (int i = 0; i < _unlockedPatterns.Count; i++)
        {
            EnemyAttackDefinitionSO definition = _unlockedPatterns[i];
            float gap = DistanceGapToRange(distance, definition.MinRange, definition.MaxRange);

            if (found && gap >= bestGap)
            {
                continue;
            }

            found = true;
            bestGap = gap;
            minRange = definition.MinRange;
            maxRange = definition.MaxRange;
        }

        return found;
    }

    // 현재 거리에서 개별 공격 범위 경계까지의 최소 간격 계산
    private static float DistanceGapToRange(float distance, float minRange, float maxRange)
    {
        if (distance < minRange)
        {
            return minRange - distance;
        }

        if (distance > maxRange)
        {
            return distance - maxRange;
        }

        return 0f;
    }

    private EnemyAttackDefinitionSO FindReadyCandidate(TargetSnapshot targetSnapshot)
    {
        if (CurrentPhase != EnemyAttackPhase.Ready || !targetSnapshot.IsAlive)
        {
            return null;
        }

        if (_reservedEntryPattern != null && IsCandidateReady(_reservedEntryPattern, targetSnapshot))
        {
            return _reservedEntryPattern;
        }

        int candidateCount = _unlockedPatterns.Count;

        if (candidateCount == 0)
        {
            return null;
        }

        // 직전 성공 다음 후보부터 한 바퀴 순회해 결정적 선택 순환 유지
        int startIndex = _lastSucceededPattern != null
            ? _unlockedPatterns.IndexOf(_lastSucceededPattern) + 1
            : 0;

        for (int offset = 0; offset < candidateCount; offset++)
        {
            EnemyAttackDefinitionSO definition = _unlockedPatterns[(startIndex + offset) % candidateCount];

            if (IsCandidateReady(definition, targetSnapshot))
            {
                return definition;
            }
        }

        return null;
    }

    private bool IsCandidateReady(EnemyAttackDefinitionSO definition, TargetSnapshot targetSnapshot)
    {
        if (_cooldownReadyAtTime.TryGetValue(definition, out float readyAt) && Time.time < readyAt)
        {
            return false;
        }

        if (targetSnapshot.Distance < definition.MinRange || targetSnapshot.Distance > definition.MaxRange)
        {
            return false;
        }

        if (definition.RequiresLineOfSight && !targetSnapshot.HasTrackingLineOfSight)
        {
            return false;
        }

        return _resolvedActions[definition].CanStart(BuildContext(definition));
    }

    // 후보 검사와 실제 시작이 동일한 시전자 Context를 공유하는 생성 경계
    private EnemyAttackContext BuildContext(EnemyAttackDefinitionSO definition)
    {
        return new EnemyAttackContext(definition, _caster, _sourceFaction, _attacker);
    }

    /// <summary>Pool 반환을 위해 진행 Action과 모든 대여별 공격 상태를 멱등 초기화</summary>
    public void ResetRuntimeState()
    {
        if (CurrentPhase != EnemyAttackPhase.Ready)
        {
            EndCurrentAttack(EnemyAttackEndResult.Cancelled, EnemyAttackCancelReason.PoolReturned);
        }

        _resolvedActions.Clear();
        _cooldownReadyAtTime.Clear();
        _unlockedPatterns.Clear();

        _caster = null;
        _attacker = null;
        _sourceFaction = Faction.Neutral;
        LastStartedPattern = null;
        _lastSucceededPattern = null;
        _reservedEntryPattern = null;
        LastEndResult = EnemyAttackEndResult.Completed;
    }

    /// <summary>현재 Snapshot에서 후보를 선택하고 Windup 진입 시도</summary>
    public EnemyAttackStartResult TryStartAttack(TargetSnapshot targetSnapshot)
    {
        if (CurrentPhase != EnemyAttackPhase.Ready)
        {
            return EnemyAttackStartResult.Blocked;
        }

        if (!targetSnapshot.IsAlive)
        {
            return EnemyAttackStartResult.InvalidTarget;
        }

        // 시야 요구 여부를 후보별로 적용해 비시야 공격까지 공통 차단하는 오류 방지
        EnemyAttackDefinitionSO candidate = FindReadyCandidate(targetSnapshot);

        if (candidate == null)
        {
            return EnemyAttackStartResult.Unavailable;
        }

        Begin(candidate, targetSnapshot);

        return EnemyAttackStartResult.Started;
    }

    private void Begin(EnemyAttackDefinitionSO definition, TargetSnapshot targetSnapshot)
    {
        HasLockedAim = false;
        LockedAimSnapshot = default;
        _currentDefinition = definition;
        _currentAction = _resolvedActions[definition];
        _lastConfirmedTargetSnapshot = targetSnapshot;
        _currentContext = BuildContext(definition);
        _phaseTimer = 0f;
        LastStartedPattern = definition;

        if (definition == _reservedEntryPattern)
        {
            _reservedEntryPattern = null;
        }

        // Windup 전환을 먼저 공개해 Presentation의 유휴 Aim 경로와 중복 처리 방지
        SetPhase(EnemyAttackPhase.Windup);

        // 공격 시작 시 Target을 향하고 Lock 이후에는 Active·Recovery 동안 방향 고정
        _movementController.SetFacingDirection(targetSnapshot.Direction);
        PublishWeaponAim(targetSnapshot.Direction);

        _currentAction.OnWindupStart(_currentContext, targetSnapshot);

        // Windup 준비 뒤 Aim을 잠가 생성 전 Telegraph가 최신 Target을 계속 추적하는 오류 방지
        if (definition.AimLockTiming == EnemyAimLockTiming.WindupStart)
        {
            LockAim(targetSnapshot);
        }
    }

    /// <summary>진행 중 공격의 Aim 추적·단계 시간·SafeTimeout 갱신</summary>
    public void Tick(float deltaTime, TargetSnapshot latestTargetSnapshot)
    {
        if (CurrentPhase == EnemyAttackPhase.Ready)
        {
            return;
        }

        _phaseTimer += deltaTime;

        // Lock 전 Windup에서만 Weapon Aim을 최신 Target 방향으로 갱신
        bool isTrackingWindup = CurrentPhase == EnemyAttackPhase.Windup && !HasLockedAim;

        bool canConfirmTarget = latestTargetSnapshot.IsAlive && latestTargetSnapshot.HasTrackingLineOfSight;

        if (isTrackingWindup && canConfirmTarget)
        {
            _lastConfirmedTargetSnapshot = latestTargetSnapshot;
            PublishWeaponAim(latestTargetSnapshot.Direction);

            // 정지한 Lock 전 Windup에서만 Target 방향이 몸체 바라보기를 소유
            if (_movementController.IsLocked)
            {
                _movementController.SetFacingDirection(latestTargetSnapshot.Direction);
            }
        }

        switch (CurrentPhase)
        {
            case EnemyAttackPhase.Windup:
                AdvancePhase(_currentDefinition.WindupAdvanceMode, _currentDefinition.WindupDuration, LockAimAndEnterActive);
                break;

            case EnemyAttackPhase.Active:
                AdvancePhase(_currentDefinition.ActiveAdvanceMode, _currentDefinition.ActiveDuration, EnterRecovery);
                break;

            case EnemyAttackPhase.Recovery:
                if (_phaseTimer >= _currentDefinition.RecoveryDuration)
                {
                    Complete();
                }
                break;
        }
    }

    // 선택된 진행 신호만 수락하고 신호 누락은 SafeTimeout 뒤 실패 종료
    private void AdvancePhase(EnemyAttackAdvanceMode advanceMode, float configuredDuration, Action onAdvance)
    {
        if (advanceMode == EnemyAttackAdvanceMode.Timer)
        {
            if (_phaseTimer >= configuredDuration)
            {
                onAdvance();
            }

            return;
        }

        if (_phaseTimer >= _currentDefinition.SafeTimeout)
        {
            Fail();
        }
    }

    /// <summary>현재 단계가 Marker 방식일 때 Animation Marker를 단계 전이로 처리</summary>
    public void NotifyAnimationMarker()
    {
        switch (CurrentPhase)
        {
            case EnemyAttackPhase.Windup:
                if (_currentDefinition.WindupAdvanceMode == EnemyAttackAdvanceMode.Marker)
                {
                    LockAimAndEnterActive();
                }
                break;

            case EnemyAttackPhase.Active:
                if (_currentDefinition.ActiveAdvanceMode == EnemyAttackAdvanceMode.Marker)
                {
                    EnterRecovery();
                }
                break;
        }
    }

    /// <summary>Active 단계의 실제 출력 Marker를 현재 Action에만 전달</summary>
    public void NotifyAttackActionMarker(int markerId)
    {
        if (CurrentPhase != EnemyAttackPhase.Active)
        {
            return;
        }

        _currentAction.OnActionMarker(_currentContext, markerId);
    }

    /// <summary>ActionCompleted 방식의 Active에서 출력 완료를 Recovery 전이로 처리</summary>
    public void NotifyActionCompleted()
    {
        if (CurrentPhase != EnemyAttackPhase.Active ||
            _currentDefinition.ActiveAdvanceMode != EnemyAttackAdvanceMode.ActionCompleted)
        {
            return;
        }

        EnterRecovery();
    }

    /// <summary>현재 Active Action의 출력 실패를 공통 Failed 종료로 처리</summary>
    public void NotifyActionFailed()
    {
        if (CurrentPhase != EnemyAttackPhase.Active)
        {
            return;
        }

        Fail();
    }

    private void LockAimAndEnterActive()
    {
        if (_currentDefinition.AimLockTiming == EnemyAimLockTiming.WindupEnd)
        {
            LockAim(_lastConfirmedTargetSnapshot);
        }

        _phaseTimer = 0f;

        SetPhase(EnemyAttackPhase.Active);

        _currentAction.OnActiveStart(_currentContext, LockedAimSnapshot);
    }

    // 설정된 시점에 AimSnapshot을 한 번 생성해 Action과 Presentation의 단일 원본으로 고정
    private void LockAim(TargetSnapshot snapshot)
    {
        LockedAimSnapshot = CaptureAimSnapshot(snapshot);
        HasLockedAim = true;
        _movementController.SetFacingDirection(LockedAimSnapshot.Direction);
        PublishWeaponAim(LockedAimSnapshot.Direction);
        _currentAction.OnAimLocked(_currentContext, LockedAimSnapshot);
    }

    private void PublishWeaponAim(Vector2 direction)
    {
        if (!CanUpdateWeaponAim || direction.sqrMagnitude <= 0f)
        {
            return;
        }

        WeaponAimChanged?.Invoke(direction);
    }

    private AimSnapshot CaptureAimSnapshot(TargetSnapshot snapshot)
    {
        Vector2 selfPosition = _caster != null ? (Vector2)_caster.position : Vector2.zero;
        Vector2 offset = snapshot.Position - selfPosition;
        Vector2 direction = offset.sqrMagnitude > 0f ? offset.normalized : snapshot.Direction;

        return new AimSnapshot(direction, snapshot.Position, snapshot.Velocity, Time.time);
    }

    private void EnterRecovery()
    {
        _phaseTimer = 0f;

        SetPhase(EnemyAttackPhase.Recovery);

        _currentAction.OnRecoveryStart(_currentContext);
    }

    // Recovery 종료를 정상 완료 결과로 공통 종료 경계에 전달
    private void Complete()
    {
        EndCurrentAttack(EnemyAttackEndResult.Completed, cancelReason: default);
    }

    /// <summary>진행 중 공격을 이유와 함께 취소하고 Action 정리와 이동 잠금 해제를 한 번 수행</summary>
    public void Cancel(EnemyAttackCancelReason reason)
    {
        if (CurrentPhase == EnemyAttackPhase.Ready)
        {
            return;
        }

        EndCurrentAttack(EnemyAttackEndResult.Cancelled, reason);
    }

    // 신호 누락과 출력 실패를 쿨다운 없는 Failed 결과로 공통 종료
    private void Fail()
    {
        EndCurrentAttack(EnemyAttackEndResult.Failed, EnemyAttackCancelReason.ConfigurationError);
    }

    /// <summary>성공·취소·실패·Pool 반환이 공유하는 단일 공격 종료 경계</summary>
    // 실행 상태 제거 후 Action을 정리하고 마지막에 Ready를 알려 재진입 시 이전 Action의 간섭 방지
    private void EndCurrentAttack(EnemyAttackEndResult endResult, EnemyAttackCancelReason cancelReason)
    {
        IEnemyAttackAction action = _currentAction;
        EnemyAttackContext context = _currentContext;
        bool succeeded = endResult == EnemyAttackEndResult.Completed;

        if (succeeded)
        {
            EnemyAttackDefinitionSO definition = _currentDefinition;

            _cooldownReadyAtTime[definition] = Time.time + Mathf.Max(definition.Cooldown, 0f);
            _lastSucceededPattern = definition;
        }

        LastEndResult = endResult;
        _currentDefinition = null;
        _currentAction = null;
        _currentContext = default;
        _lastConfirmedTargetSnapshot = default;
        _phaseTimer = 0f;
        HasLockedAim = false;
        LockedAimSnapshot = default;

        // Action 정리 완료 전 Ready 구독자의 새 공격 시작 방지
        CurrentPhase = EnemyAttackPhase.Ready;
        ApplyMovementLockForPhase(EnemyAttackPhase.Ready);

        if (succeeded)
        {
            action.OnCompleted(context);
        }
        else
        {
            action.OnCancelled(context, cancelReason);
        }

        PhaseChanged?.Invoke(EnemyAttackPhase.Ready);
    }

    // 진행 단계 진입 전용이며 Ready 복귀는 종료 순서를 소유한 EndCurrentAttack에서 처리
    private void SetPhase(EnemyAttackPhase phase)
    {
        CurrentPhase = phase;
        ApplyMovementLockForPhase(phase);
        PhaseChanged?.Invoke(phase);
    }

    // 단계별 이동 허용 설정을 Movement 잠금으로 반영하고 Ready에서는 항상 해제
    private void ApplyMovementLockForPhase(EnemyAttackPhase phase)
    {
        bool allowMovement = true;

        switch (phase)
        {
            case EnemyAttackPhase.Windup:
                allowMovement = _currentDefinition.AllowMovementDuringWindup;
                break;

            case EnemyAttackPhase.Active:
                allowMovement = _currentDefinition.AllowMovementDuringActive;
                break;

            case EnemyAttackPhase.Recovery:
                allowMovement = _currentDefinition.AllowMovementDuringRecovery;
                break;
        }

        _movementController.SetMovementLocked(!allowMovement, LockedAimSnapshot.Direction);
    }
}
