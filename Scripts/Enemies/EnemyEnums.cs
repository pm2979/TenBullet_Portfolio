// Enemy 런타임과 직렬화 데이터가 공유하는 공개 상태값을 한곳에서 관리
// 기존 Asset 호환을 위해 이름과 정수값을 유지하고 새 값은 마지막에만 추가

/// <summary>Pool 대여부터 반환까지 EnemyActor의 수명주기 상태</summary>
public enum EnemyLifecycleState
{
    Inactive = 0,
    Spawning = 1,
    Active = 2,
    Dying = 3
}

/// <summary>처치·Encounter·Scene·초기화 실패를 구분하는 Pool 반환 이유</summary>
public enum EnemyReturnReason
{
    Killed = 0,
    EncounterCancelled = 1,
    SceneTransition = 2,
    InitializationFailed = 3
}

/// <summary>EnemyInstanceProvider의 대여 실패 분류</summary>
public enum EnemyRentFailureReason
{
    None = 0,
    InvalidArchetype = 1,
    PoolManagerMissing = 2,
    PoolRentFailed = 3,
    TypeMismatch = 4,
    InitializationFailed = 5
}

/// <summary>EnemyDecisionController가 선택한 현재 행동 의도</summary>
public enum EnemyBehaviorState
{
    Idle = 0,
    Pursue = 1,
    Reposition = 2,
    Attack = 3,

    /// <summary>최초 감지 전 SpawnPosition 반경을 이동하는 순찰 상태</summary>
    Patrol = 4
}

/// <summary>Navigation 조향 요청의 성공·대기·도착·실패 상태</summary>
public enum EnemyNavigationStatus
{
    Success = 0,
    PathPending = 1,
    DestinationReached = 2,
    NoPath = 3,
    InvalidTarget = 4,
    NotReady = 5
}

/// <summary>EnemyAttackController가 소유하는 공통 공격 단계</summary>
public enum EnemyAttackPhase
{
    Ready = 0,
    Windup = 1,
    Active = 2,
    Recovery = 3
}

/// <summary>Windup과 Active가 Timer·Marker·Action 완료 신호로 전이하는 방식</summary>
// 신호 기반 방식은 누락 시 Failed로 끝내기 위한 SafeTimeout 필요
public enum EnemyAttackAdvanceMode
{
    /// <summary>설정한 단계 지속 시간 경과로 전이</summary>
    Timer = 0,

    /// <summary>Animation Marker 도달 신호로 전이</summary>
    Marker = 1,

    /// <summary>현재 Action의 모든 출력 완료 통지로 Active 종료</summary>
    ActionCompleted = 2
}

/// <summary>공격 후보 선택과 시작 시도의 결과</summary>
public enum EnemyAttackStartResult
{
    Started = 0,
    Unavailable = 1,
    InvalidTarget = 2,
    Blocked = 3,
    MissingDependency = 4
}

/// <summary>시작된 공격의 정상 완료·취소·실패 결과</summary>
public enum EnemyAttackEndResult
{
    Completed = 0,
    Cancelled = 1,
    Failed = 2
}

/// <summary>진행 중인 공격을 중단하는 수명주기·Target·설정 이유</summary>
public enum EnemyAttackCancelReason
{
    Death = 0,
    TargetLost = 1,
    EncounterCancelled = 2,
    Deactivated = 3,
    PoolReturned = 4,
    ConfigurationError = 5
}

/// <summary>Target Transform 읽기를 중단하고 AimSnapshot을 고정하는 시점</summary>
public enum EnemyAimLockTiming
{
    WindupStart = 0,
    WindupEnd = 1
}

/// <summary>EnemyAttackActionEntry의 출력을 실행하거나 판정 구간을 전환하는 시점</summary>
public enum EnemyAttackActionTrigger
{
    /// <summary>Active 진입 시 Marker 없이 한 번 실행</summary>
    OnActiveStart = 0,
    /// <summary>지정한 Marker 도달 시 한 번 실행</summary>
    OnActionMarker = 1,
    /// <summary>지정한 Marker 도달 시 지속 판정 구간 시작</summary>
    OnMarkerWindowEnable = 2,
    /// <summary>지정한 Marker 도달 시 지속 판정 구간 종료</summary>
    OnMarkerWindowDisable = 3
}

/// <summary>기본 원거리 공격의 직선·연사·분산·원형 발사 각도 패턴</summary>
public enum EnemyRangedFirePattern
{
    Single = 0,
    Burst = 1,
    Spread = 2,
    Circle = 3
}

/// <summary>데이터·Spawn·Inspector 표시와 검증에 사용하는 Enemy 콘텐츠 등급</summary>
// 공격 실행 분기와 무관하며 직렬화 호환을 위해 값과 순서 유지
public enum MonsterGrade
{
    Normal = 0,
    Elite = 1,
    Boss = 2
}
