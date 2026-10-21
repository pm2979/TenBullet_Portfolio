using System;
using UnityEngine;

/// <summary>Sensor와 Attack 상태로 순찰·추적·후퇴·공격·수색·복귀 의도를 결정</summary>
// 실행은 Movement와 Attack Controller에 위임하고 대여별 감지·목적지·타이머만 소유
[RequireComponent(typeof(EnemyTargetSensor))]
[RequireComponent(typeof(EnemyMovementController))]
[DisallowMultipleComponent]
public sealed class EnemyDecisionController : MonoBehaviour
{
    private enum NavigationMode
    {
        None,
        Patrol,
        Pursue,
        Reposition,
        Investigate,
        Search,
        ReturnToSpawn,
        ReturnFailed
    }

    private EnemyTargetSensor _targetSensor;
    private EnemyMovementController _movementController;
    private EnemyAttackController _attackController;

    // Navigation 도착 보고가 없는 구성의 Patrol·후퇴 거리 판정 보조값
    private const float NavigationArrivalDistance = 0.2f;
    private const float StuckDetectionDuration = 1f;
    private const float StuckMinimumProgress = 0.05f;
    private const float SearchDuration = 2.5f;

    private float _moveSpeed;
    private EnemyRuntimeConfig _runtimeConfig;
    private float _decisionTimer;
    private float _elapsedSinceLastTick;
    private bool _wasKnockedBack;

    private Vector2 _spawnPosition;
    private float _idleTimer;

    private float _repositionRuntimeTime;
    private float _repositionCooldownUntil;

    private NavigationMode _navigationMode;
    private Vector2 _navigationTarget;
    private float _navigationModeElapsed;
    private Vector2 _stuckReferencePosition;
    private float _stuckElapsed;

    private bool _hasLastConfirmedTargetPosition;
    private Vector2 _lastConfirmedTargetPosition;

    public EnemyBehaviorState CurrentState { get; private set; } = EnemyBehaviorState.Idle;
    public bool IsActive { get; private set; }
    private float CurrentInterval => HasDetectedPlayer
        ? _runtimeConfig.CombatDecisionInterval
        : _runtimeConfig.DetectionInterval;

    /// <summary>이번 대여에서 최초 감지 조건을 통과했는지 여부</summary>
    public bool HasDetectedPlayer { get; private set; }

    /// <summary>현재 Patrol 목적지가 있으면 위치와 함께 반환</summary>
    public bool TryGetPatrolTarget(out Vector2 patrolTarget)
    {
        patrolTarget = _navigationTarget;
        return _navigationMode == NavigationMode.Patrol;
    }

    public event Action<EnemyBehaviorState> StateChanged;

    private void Awake()
    {
        _targetSensor = GetComponent<EnemyTargetSensor>();
        _movementController = GetComponent<EnemyMovementController>();
        _attackController = GetComponent<EnemyAttackController>();
    }

    private void Update()
    {
        if (!IsActive)
        {
            return;
        }

        if (_movementController.IsKnockedBack)
        {
            _wasKnockedBack = true;
            return;
        }

        if (_wasKnockedBack)
        {
            _wasKnockedBack = false;
            _decisionTimer = 0f;
            _elapsedSinceLastTick = 0f;
            ResetStuckMeasurement();
        }

        float deltaTime = Time.deltaTime;
        _decisionTimer -= deltaTime;
        _elapsedSinceLastTick += deltaTime;

        if (_decisionTimer > 0f)
        {
            return;
        }

        Tick(_elapsedSinceLastTick);
        _elapsedSinceLastTick = 0f;
        _decisionTimer += ResolveNextDecisionDelay();
    }

    // Patrol 대기 종료가 기본 판단 주기보다 빠르면 다음 Tick 예약을 앞당김
    private float ResolveNextDecisionDelay()
    {
        if (!HasDetectedPlayer && _runtimeConfig.UsePatrol &&
            _navigationMode != NavigationMode.Patrol && _idleTimer > 0f)
        {
            return Mathf.Min(CurrentInterval, _idleTimer);
        }

        return CurrentInterval;
    }

    /// <summary>최종 MoveSpeed와 행동 설정 및 Spawn 기준 위치로 판단 루프 활성화</summary>
    public void Activate(float moveSpeed, EnemyRuntimeConfig runtimeConfig, Vector2 spawnPosition)
    {
        _moveSpeed = moveSpeed;
        _runtimeConfig = runtimeConfig;
        _decisionTimer = UnityEngine.Random.Range(0f, runtimeConfig.DetectionInterval);
        _elapsedSinceLastTick = 0f;
        _wasKnockedBack = _movementController.IsKnockedBack;
        _spawnPosition = spawnPosition;
        IsActive = true;

        if (_attackController != null)
        {
            _attackController.PhaseChanged += HandleAttackPhaseChanged;
        }

        ResetPatrolRuntimeState();
        ResetRepositionRuntimeState();
        ResetNavigationRuntimeState();
        ResetTrackingRuntimeState();

        if (_runtimeConfig.UsePatrol)
        {
            BeginIdleWait();
        }

        SetState(EnemyBehaviorState.Idle);
    }

    /// <summary>판단을 중지하고 공격 구독·감지·이동 세션을 Idle 초기 상태로 정리</summary>
    public void Deactivate()
    {
        if (_attackController != null)
        {
            _attackController.PhaseChanged -= HandleAttackPhaseChanged;
        }

        IsActive = false;
        _wasKnockedBack = false;
        SetNavigationMode(NavigationMode.None);
        _movementController.Stop(Vector2.zero);

        ResetPatrolRuntimeState();
        ResetRepositionRuntimeState();
        ResetNavigationRuntimeState();
        ResetTrackingRuntimeState();
        _runtimeConfig = null;
        _spawnPosition = Vector2.zero;

        SetState(EnemyBehaviorState.Idle);
    }

    private void HandleAttackPhaseChanged(EnemyAttackPhase phase)
    {
        if (phase == EnemyAttackPhase.Ready)
        {
            _decisionTimer = 0f;
            return;
        }

        if (_navigationMode == NavigationMode.Reposition)
        {
            FinishReposition(Vector2.zero);
        }
        else
        {
            SetNavigationMode(NavigationMode.None);
            _movementController.Stop(Vector2.zero);
        }

        // 공격 시작은 NavigationMode가 없으므로 행동 상태를 즉시 Attack으로 확정
        SetState(EnemyBehaviorState.Attack);
    }

    // Patrol 목적지·대기와 최초 감지 상태 초기화
    private void ResetPatrolRuntimeState()
    {
        _idleTimer = 0f;
        HasDetectedPlayer = false;
    }

    // 후퇴 목적지·진행 시간·쿨다운 초기화
    private void ResetRepositionRuntimeState()
    {
        _repositionRuntimeTime = 0f;
        _repositionCooldownUntil = 0f;
    }

    // Navigation 목적·시간·진척 측정 초기화
    private void ResetNavigationRuntimeState()
    {
        _navigationMode = NavigationMode.None;
        _navigationTarget = Vector2.zero;
        _navigationModeElapsed = 0f;
        ResetStuckMeasurement();
    }

    // 마지막 확인 위치와 수색 시간 초기화
    private void ResetTrackingRuntimeState()
    {
        _hasLastConfirmedTargetPosition = false;
        _lastConfirmedTargetPosition = Vector2.zero;
    }

    /// <summary>공칭 판단 주기를 경과 시간으로 사용해 한 번의 판단 실행</summary>
    public void Tick()
    {
        Tick(CurrentInterval);
    }

    /// <summary>실제 경과 시간을 Patrol·수색·고착 판단에 반영해 한 번의 판단 실행</summary>
    public void Tick(float elapsedSeconds)
    {
        float safeElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
        _repositionRuntimeTime += safeElapsedSeconds;

        if (_navigationMode == NavigationMode.Reposition)
        {
            _navigationModeElapsed += safeElapsedSeconds;
        }

        TargetSnapshot snapshot = _targetSensor.Refresh();

        // Spawn 복귀 중에는 최초 감지 조건으로만 전투 재진입 허용
        if (_navigationMode == NavigationMode.ReturnToSpawn ||
            _navigationMode == NavigationMode.ReturnFailed)
        {
            if (snapshot.IsAlive && IsInitialDetection(snapshot))
            {
                ResumeDetectedTarget(snapshot);
            }
            else
            {
                ContinueReturnToSpawn(safeElapsedSeconds);
                return;
            }
        }

        // Target 생존 계약 상실 시 공격을 취소하고 Spawn 복귀 시작
        if (!snapshot.IsAlive)
        {
            if (HasDetectedPlayer || _navigationMode == NavigationMode.Search)
            {
                BeginReturnToSpawn(cancelAttack: true);
                ContinueReturnToSpawn(0f);
                return;
            }

            TickWithoutTarget(safeElapsedSeconds);
            return;
        }

        // 전투 진입은 최초 탐지 거리와 시야를 함께 요구
        if (!HasDetectedPlayer)
        {
            if (!IsInitialDetection(snapshot))
            {
                TickWithoutTarget(elapsedSeconds);
                return;
            }

            HasDetectedPlayer = true;
            ConfirmTargetPosition(snapshot.Position);
        }

        // 최초 감지 시 Patrol 경로를 취소하고 Target 추적 세션으로 전환
        if (_navigationMode == NavigationMode.Patrol)
        {
            SetNavigationMode(NavigationMode.None);
        }

        if (snapshot.HasTrackingLineOfSight)
        {
            ConfirmTargetPosition(snapshot.Position);

            if (_navigationMode == NavigationMode.Search)
            {
                EndSearchForRediscovery();
            }
        }

        if (_attackController != null && _attackController.CurrentPhase != EnemyAttackPhase.Ready)
        {
            // 단순 추적 시야 상실은 현재 Action 종료 계약을 침범하지 않음
            SetState(EnemyBehaviorState.Attack);
            return;
        }

        if (_navigationMode == NavigationMode.Search)
        {
            ContinueSearch(safeElapsedSeconds);
            return;
        }

        // 후보별 시야 정책을 Attack Controller에 맡기고 시작 실패 시에만 조사로 전환
        if (_attackController != null &&
            _attackController.TryStartAttack(snapshot) == EnemyAttackStartResult.Started)
        {
            SetState(EnemyBehaviorState.Attack);
            return;
        }

        if (!snapshot.HasTrackingLineOfSight)
        {
            ContinueInvestigation(safeElapsedSeconds);
            return;
        }

        if (_navigationMode == NavigationMode.Pursue && HasNavigationStalled(safeElapsedSeconds))
        {
            RecoverFromNavigationStall(snapshot.Direction);
            return;
        }

        if (_navigationMode == NavigationMode.Reposition && ContinueReposition(snapshot, safeElapsedSeconds))
        {
            return;
        }

        if (_attackController != null && _attackController.TryGetNearestEngagementRange(snapshot.Distance, out float minRange, out float maxRange))
        {
            if (snapshot.Distance > maxRange)
            {
                EnterPursue(snapshot.Position);
                return;
            }

            if (snapshot.Distance < minRange)
            {
                if (CanStartReposition())
                {
                    TryStartReposition(snapshot, minRange);
                    return;
                }

                EnterIdleStop(snapshot.Direction);
                return;
            }

            // 일시적 시작 불가는 사거리를 유지한 Idle 대기로 처리
            EnterIdleStop(snapshot.Direction);
            return;
        }

        // 해금 공격이 없으면 Target 추적 이동만 유지
        EnterPursue(snapshot.Position);
    }

    private static bool IsInitialDetection(TargetSnapshot snapshot)
    {
        return snapshot.IsValid && snapshot.HasLineOfSight;
    }

    private void ConfirmTargetPosition(Vector2 targetPosition)
    {
        _hasLastConfirmedTargetPosition = true;
        _lastConfirmedTargetPosition = targetPosition;
    }

    private void ResumeDetectedTarget(TargetSnapshot snapshot)
    {
        HasDetectedPlayer = true;
        ConfirmTargetPosition(snapshot.Position);
        SetNavigationMode(NavigationMode.None);
    }

    // 추적 시야 상실 뒤 숨겨진 실시간 좌표 대신 마지막 확인 위치만 조사
    private void ContinueInvestigation(float elapsedSeconds)
    {
        if (_navigationMode == NavigationMode.Reposition)
        {
            FinishReposition(Vector2.zero);
        }

        if (!_hasLastConfirmedTargetPosition)
        {
            BeginSearch();
            return;
        }

        if (_navigationMode == NavigationMode.Investigate &&
            (HasInvestigationPathEnded() || HasReachedPosition(_lastConfirmedTargetPosition) ||
             HasNavigationStalled(elapsedSeconds)))
        {
            BeginSearch();
            return;
        }

        SetNavigationMode(NavigationMode.Investigate, _lastConfirmedTargetPosition);
        _movementController.MoveTowards(_navigationTarget, _moveSpeed);
    }

    private bool HasInvestigationPathEnded()
    {
        EnemyNavigationStatus status = _movementController.LastNavigationStatus;
        return status == EnemyNavigationStatus.DestinationReached ||
               status == EnemyNavigationStatus.NoPath ||
               status == EnemyNavigationStatus.InvalidTarget;
    }

    private void BeginSearch()
    {
        SetNavigationMode(NavigationMode.Search);
        _movementController.Stop(Vector2.zero);
    }

    private void ContinueSearch(float elapsedSeconds)
    {
        _navigationModeElapsed += elapsedSeconds;

        if (_navigationModeElapsed < SearchDuration)
        {
            _movementController.Stop(Vector2.zero);
            return;
        }

        BeginReturnToSpawn(cancelAttack: false);
        ContinueReturnToSpawn(0f);
    }

    private void EndSearchForRediscovery()
    {
        SetNavigationMode(NavigationMode.None);
    }

    private void BeginReturnToSpawn(bool cancelAttack)
    {
        if (cancelAttack && _attackController != null &&
            _attackController.CurrentPhase != EnemyAttackPhase.Ready)
        {
            _attackController.Cancel(EnemyAttackCancelReason.TargetLost);
        }

        if (_navigationMode == NavigationMode.Reposition)
        {
            FinishReposition(Vector2.zero);
        }

        HasDetectedPlayer = false;
        SetNavigationMode(NavigationMode.ReturnToSpawn, _spawnPosition);
    }

    // Spawn 복귀 경로 실패는 반복 요청 없이 현 위치 정지와 최초 감지만 유지
    private void ContinueReturnToSpawn(float elapsedSeconds)
    {
        if (_navigationMode == NavigationMode.ReturnFailed)
        {
            _movementController.Stop(Vector2.zero);
            return;
        }

        if (HasReachedPosition(_spawnPosition) ||
            _movementController.LastNavigationStatus == EnemyNavigationStatus.DestinationReached)
        {
            FinishReturnToSpawn();
            return;
        }

        EnemyNavigationStatus status = _movementController.LastNavigationStatus;

        if (_navigationMode == NavigationMode.ReturnToSpawn &&
            (status == EnemyNavigationStatus.NoPath || status == EnemyNavigationStatus.InvalidTarget ||
             HasNavigationStalled(elapsedSeconds)))
        {
            SetNavigationMode(NavigationMode.ReturnFailed);
            _movementController.Stop(Vector2.zero);
            return;
        }

        SetNavigationMode(NavigationMode.ReturnToSpawn, _spawnPosition);
        _movementController.MoveTowards(_navigationTarget, _moveSpeed);
    }

    private void FinishReturnToSpawn()
    {
        _hasLastConfirmedTargetPosition = false;
        _lastConfirmedTargetPosition = Vector2.zero;
        SetNavigationMode(NavigationMode.None);

        if (_runtimeConfig.UsePatrol)
        {
            BeginIdleWait();
        }

        EnterIdleStop();
    }

    private bool HasReachedPosition(Vector2 position)
    {
        Vector2 selfPosition = transform.position;
        return (position - selfPosition).sqrMagnitude <= NavigationArrivalDistance * NavigationArrivalDistance;
    }

    private bool CanStartReposition()
    {
        return _runtimeConfig.AllowReposition &&
               !_movementController.IsLocked &&
               _repositionRuntimeTime >= _repositionCooldownUntil;
    }

    // Target 반대편 후퇴 목적지를 한 번 보정하고 실패 시 직선 대체 없이 쿨다운 적용
    private void TryStartReposition(TargetSnapshot snapshot, float minRange)
    {
        if (snapshot.Direction.sqrMagnitude <= 0f)
        {
            FailRepositionStart(snapshot.Direction);
            return;
        }

        Vector2 desiredPosition = snapshot.Position -
                                  snapshot.Direction * (minRange + _runtimeConfig.RepositionDistanceMargin);

        if (!_movementController.TryGetNearestWalkablePosition(desiredPosition, out Vector2 retreatPoint) ||
            !IsFartherFromTarget(retreatPoint, snapshot.Position, transform.position))
        {
            FailRepositionStart(snapshot.Direction);
            return;
        }

        SetNavigationMode(NavigationMode.Reposition, retreatPoint);
        _movementController.MoveTowards(_navigationTarget, ResolveRepositionMoveSpeed());
    }

    // 저장한 후퇴 목적지를 유지하며 시간·도착·경로·고착 종료 조건 확인
    private bool ContinueReposition(TargetSnapshot snapshot, float elapsedSeconds)
    {
        if (_movementController.IsLocked ||
            _navigationModeElapsed >= _runtimeConfig.RepositionMaxDuration ||
            HasRepositionPathEnded() ||
            HasReachedRepositionTarget() ||
            HasNavigationStalled(elapsedSeconds) ||
            !IsFartherFromTarget(_navigationTarget, snapshot.Position, transform.position))
        {
            FinishReposition(snapshot.Direction);
            return false;
        }

        _movementController.MoveTowards(_navigationTarget, ResolveRepositionMoveSpeed());
        return true;
    }

    private bool HasRepositionPathEnded()
    {
        EnemyNavigationStatus status = _movementController.LastNavigationStatus;
        return status == EnemyNavigationStatus.DestinationReached ||
               status == EnemyNavigationStatus.NoPath ||
               status == EnemyNavigationStatus.InvalidTarget;
    }

    private bool HasReachedRepositionTarget()
    {
        Vector2 selfPosition = transform.position;
        return (_navigationTarget - selfPosition).sqrMagnitude <=
               NavigationArrivalDistance * NavigationArrivalDistance;
    }

    private static bool IsFartherFromTarget(Vector2 retreatPoint, Vector2 targetPosition, Vector2 selfPosition)
    {
        return (retreatPoint - targetPosition).sqrMagnitude > (selfPosition - targetPosition).sqrMagnitude;
    }

    private void FailRepositionStart(Vector2 facingDirection)
    {
        _repositionCooldownUntil = _repositionRuntimeTime + _runtimeConfig.RepositionCooldown;
        EnterIdleStop(facingDirection);
    }

    // 성공·실패·잠금·공격 선점 모두 후퇴 종료 시점부터 동일 쿨다운 적용
    private void FinishReposition(Vector2 facingDirection)
    {
        _repositionCooldownUntil = _repositionRuntimeTime + _runtimeConfig.RepositionCooldown;
        SetNavigationMode(NavigationMode.None);
        _movementController.Stop(facingDirection);
    }

    private float ResolveRepositionMoveSpeed()
    {
        return _moveSpeed * _runtimeConfig.RepositionMoveSpeedMultiplier;
    }

    // Target 미감지 상태에서 설정에 따라 Idle 유지 또는 Patrol 세션 진행
    private void TickWithoutTarget(float elapsedSeconds)
    {
        if (!_runtimeConfig.UsePatrol)
        {
            EnterIdleStop();
            return;
        }

        if (_navigationMode == NavigationMode.Patrol)
        {
            if (HasNavigationStalled(elapsedSeconds))
            {
                AbandonPatrolTarget();
                return;
            }

            if (HasPatrolPathFailed())
            {
                // 경로 생성 실패 목적지를 버리고 다음 대기 뒤 새 후보 재시도
                AbandonPatrolTarget();
                return;
            }

            if (!HasReachedPatrolTarget())
            {
                _movementController.MoveTowards(_navigationTarget, ResolvePatrolMoveSpeed());
                return;
            }

            // Patrol 도착 뒤 다음 목적지 선택 전 Idle 대기
            AbandonPatrolTarget();
            return;
        }

        _idleTimer -= elapsedSeconds;

        if (_idleTimer > 0f)
        {
            EnterIdleStop();
            return;
        }

        if (!TrySelectPatrolTarget(out Vector2 patrolTarget))
        {
            // 모든 후보 보정 실패 시 정지 상태로 다음 선택까지 대기
            BeginIdleWait();
            EnterIdleStop();
            return;
        }

        SetNavigationMode(NavigationMode.Patrol, patrolTarget);
        _movementController.MoveTowards(_navigationTarget, ResolvePatrolMoveSpeed());
    }

    private void EnterIdleStop(Vector2 facingDirection = default)
    {
        SetNavigationMode(NavigationMode.None);
        _movementController.Stop(facingDirection);
    }

    private void EnterPursue(Vector2 targetPosition)
    {
        SetNavigationMode(NavigationMode.Pursue, targetPosition);
        _movementController.MoveTowards(_navigationTarget, _moveSpeed);
    }

    private void BeginIdleWait()
    {
        _idleTimer = UnityEngine.Random.Range(_runtimeConfig.PatrolIdleDurationMin, _runtimeConfig.PatrolIdleDurationMax);
    }

    // Actor가 공급한 최종 MoveSpeed에 Patrol 배율만 적용
    private float ResolvePatrolMoveSpeed()
    {
        return _moveSpeed * _runtimeConfig.PatrolMoveSpeedMultiplier;
    }

    // Spawn 반경 후보를 이동 가능 지점으로 보정하고 보정 후에도 반경 안인지 재검증
    private bool TrySelectPatrolTarget(out Vector2 patrolTarget)
    {
        for (int attempt = 0; attempt < _runtimeConfig.PatrolTargetMaxAttempts; attempt++)
        {
            Vector2 candidate = _spawnPosition + UnityEngine.Random.insideUnitCircle * _runtimeConfig.PatrolRadius;

            if (!_movementController.TryGetNearestWalkablePosition(candidate, out Vector2 walkableCandidate))
            {
                continue;
            }

            // 보정 결과가 Spawn 반경 밖이면 Patrol 후보에서 제외
            if (!IsPatrolCandidateInRadius(walkableCandidate))
            {
                continue;
            }

            patrolTarget = walkableCandidate;
            return true;
        }

        patrolTarget = Vector2.zero;
        return false;
    }

    // 현재 Patrol 목적지의 Navigation 실패 여부 확인
    private bool HasPatrolPathFailed()
    {
        EnemyNavigationStatus status = _movementController.LastNavigationStatus;

        return status == EnemyNavigationStatus.NoPath || status == EnemyNavigationStatus.InvalidTarget;
    }

    // Navigation 도착 상태를 우선하고 직선 구현을 위해 거리 판정 병행
    private bool HasReachedPatrolTarget()
    {
        if (_movementController.LastNavigationStatus == EnemyNavigationStatus.DestinationReached)
        {
            return true;
        }

        Vector2 selfPosition = transform.position;

        return (_navigationTarget - selfPosition).sqrMagnitude <= NavigationArrivalDistance * NavigationArrivalDistance;
    }

    // Patrol 목적지와 현재 경로를 버리고 다음 선택까지 Idle 대기
    private void AbandonPatrolTarget()
    {
        SetNavigationMode(NavigationMode.None);
        BeginIdleWait();
        EnterIdleStop();
    }

    // Navigation 목적이 바뀔 때만 이전 경로를 취소하고 세션 측정값 초기화
    private void SetNavigationMode(NavigationMode nextMode, Vector2 target = default)
    {
        if (_navigationMode == nextMode)
        {
            _navigationTarget = target;
            SetState(ResolveBehaviorState(nextMode));
            return;
        }

        if (_navigationMode != NavigationMode.None)
        {
            _movementController.CancelNavigationPath();
        }

        _navigationMode = nextMode;
        _navigationTarget = target;
        _navigationModeElapsed = 0f;
        ResetStuckMeasurement();
        SetState(ResolveBehaviorState(nextMode));
    }

    private static EnemyBehaviorState ResolveBehaviorState(NavigationMode mode)
    {
        switch (mode)
        {
            case NavigationMode.Patrol:
            case NavigationMode.ReturnToSpawn:
                return EnemyBehaviorState.Patrol;
            case NavigationMode.Pursue:
            case NavigationMode.Investigate:
                return EnemyBehaviorState.Pursue;
            case NavigationMode.Reposition:
                return EnemyBehaviorState.Reposition;
            default:
                return EnemyBehaviorState.Idle;
        }
    }

    // 실제 조향 중에만 진척을 측정해 대기·잠금·넉백을 고착으로 오인하지 않도록 처리
    private bool HasNavigationStalled(float elapsedSeconds)
    {
        Vector2 currentPosition = transform.position;

        if (_navigationMode == NavigationMode.None ||
            !_movementController.HasNavigationIntent ||
            _movementController.LastNavigationStatus != EnemyNavigationStatus.Success ||
            _movementController.IsLocked ||
            _movementController.IsKnockedBack ||
            (_attackController != null && _attackController.CurrentPhase != EnemyAttackPhase.Ready))
        {
            _stuckReferencePosition = currentPosition;
            _stuckElapsed = 0f;
            return false;
        }

        if ((currentPosition - _stuckReferencePosition).sqrMagnitude >=
            StuckMinimumProgress * StuckMinimumProgress)
        {
            _stuckReferencePosition = currentPosition;
            _stuckElapsed = 0f;
            return false;
        }

        _stuckElapsed += elapsedSeconds;

        if (_stuckElapsed < StuckDetectionDuration)
        {
            return false;
        }

        ResetStuckMeasurement();
        return true;
    }

    private void RecoverFromNavigationStall(Vector2 facingDirection)
    {
        SetNavigationMode(NavigationMode.None);
        _movementController.Stop(facingDirection);
    }

    private void ResetStuckMeasurement()
    {
        _stuckReferencePosition = transform.position;
        _stuckElapsed = 0f;
    }

    // 보정 이후에도 최종 목적지를 SpawnPosition 반경 안으로 제한
    private bool IsPatrolCandidateInRadius(Vector2 candidate)
    {
        float radius = _runtimeConfig.PatrolRadius;
        return (candidate - _spawnPosition).sqrMagnitude <= radius * radius;
    }

    private void SetState(EnemyBehaviorState next)
    {
        if (CurrentState == next)
        {
            return;
        }

        CurrentState = next;
        StateChanged?.Invoke(next);
    }
}
