// Dungeon 생성·런타임·실패 분류가 공유하는 공개 상태값을 한곳에서 관리
// 기존 Asset 호환을 위해 이름과 정수값을 유지하고 새 값은 마지막에만 추가
// 여러 시스템이 공유하는 DungeonDifficulty는 DungeonDifficulty.cs에 따로 둔다

/// <summary>
/// 일반 생성 경로와 보스 전용 생성 경로를 구분하는 던전 유형.
/// </summary>
public enum DungeonType
{
    Normal = 0,
    Boss = 1,
}

/// <summary>
/// 회전하지 않는 2D 정수 셀 Room의 출입구 방향.
/// </summary>
public enum RoomConnectionDirection
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3,
}

/// <summary>
/// 생성과 런타임이 공유하는 Room 역할 분류.
/// </summary>
public enum RoomRole
{
    /// <summary>입구 Room.</summary>
    Entrance = 0,

    /// <summary>전투 Room.</summary>
    Combat = 1,

    /// <summary>보물 Room.</summary>
    Treasure = 2,

    /// <summary>상점 Room.</summary>
    Shop = 3,

    /// <summary>비밀 Room.</summary>
    Secret = 4,

    /// <summary>저주 Room.</summary>
    Cursed = 5,

    /// <summary>휴식 Room.</summary>
    Rest = 6,

    /// <summary>보스 Room.</summary>
    Boss = 7,

    /// <summary>통로 Room.</summary>
    Corridor = 8,
}

/// <summary>
/// Room Spawn Point 하나가 수용할 수 있는 배치 대상 분류.
/// </summary>
public enum DungeonSpawnPointKind
{
    /// <summary>Player 시작 위치.</summary>
    Player = 0,

    /// <summary>일반 Enemy 배치 위치.</summary>
    RegularEnemy = 1,

    /// <summary>Target 목표 Enemy 전용 위치.</summary>
    Target = 2,

    /// <summary>Boss 전용 위치.</summary>
    Boss = 3,
}

/// <summary>
/// Dungeon Run 완료 조건을 결정하는 목표 종류.
/// </summary>
public enum DungeonObjectiveType
{
    /// <summary>배치된 모든 일반 Enemy 처치.</summary>
    AllEnemies = 0,

    /// <summary>지정된 Target Enemy 처치.</summary>
    Target = 1,

    /// <summary>Boss Enemy 처치.</summary>
    Boss = 2,
}

/// <summary>
/// DungeonRuntime이 소유하는 Dungeon Run 전체 진행의 단일 권위.
/// </summary>
public enum DungeonRunState
{
    Idle = 0,
    Preparing = 1,
    Ready = 2,
    Cleared = 3,
    Failed = 4,
    Cancelled = 5,
    Disposed = 6
}

/// <summary>
/// DungeonSession이 한 번만 확정하는 도전 종료 결과.
/// </summary>
public enum DungeonRunResult
{
    Cleared = 0,
    Failed = 1,
}

/// <summary>
/// Edgar 기반 공간 생성·변환 실패 원인. Edgar 타입을 참조하지 않는다.
/// </summary>
public enum EdgarGenerationFailure
{
    None = 0,
    InvalidDefinition = 1,
    InvalidTopology = 2,
    TemplateCandidateUnavailable = 3,
    RoomTemplateMappingAmbiguous = 4,
    GenerationFailed = 5,
    RoomMappingFailed = 6,
    ConnectionMappingFailed = 7,
}

/// <summary>
/// Dungeon Room 조립 실패 원인.
/// </summary>
public enum DungeonAssemblyFailure
{
    None = 0,
    InvalidPlan = 1,
    MissingRoomPrefab = 2,
    MissingRoomView = 3,
    InvalidRoomView = 4,
    InstantiationFailed = 5,
    MissingRoomInstance = 6,
    ConnectionDoorMismatch = 7,

    // 이미 준비되거나 공개된 Dungeon이 있어 다시 조립할 수 없음.
    AlreadyBuilt = 8,
}

/// <summary>
/// DungeonEnemyPlanner 생성 실패 원인.
/// </summary>
public enum DungeonEnemyPlanFailure
{
    None = 0,
    InvalidPoster = 1,
    InvalidSpawnPointSnapshot = 2,
    NoRegularEnemyPoint = 3,
    NoEligibleCandidate = 4,
    TargetPointUnavailable = 5,
    BossPointInvalid = 6,
}

/// <summary>
/// DungeonEnemyDeployer.Deploy 실패 원인.
/// </summary>
public enum DungeonEnemyDeploymentFailure
{
    None = 0,
    InvalidConfiguration = 1,
    SpawnPointMissing = 2,
    EnemyRentFailed = 3,
}

/// <summary>
/// Dungeon Bootstrap 준비가 중단된 단계. 같은 단계 안의 세부 원인은 값을 나누지 않고
/// DungeonBootstrapResult.Message로 전달한다. 직렬화되지 않으므로 준비 순서대로 번호를 매긴다.
/// </summary>
public enum DungeonBootstrapFailure
{
    None = 0,

    // 호출 전제: 이미 준비 중이거나 정리가 끝나지 않았거나 필수 참조가 비어 있음.
    AlreadyInitialized = 1,
    DisposalPending = 2,
    InvalidConfiguration = 3,

    // 데이터 단계: Database 초기화·콘텐츠 조회·Pool 등록·정적 데이터 검증.
    DatabaseInitializationFailed = 4,
    ContentUnavailable = 5,
    PoolUnavailable = 6,
    StaticValidationFailed = 7,

    // 계획 단계: Topology 수립과 Edgar 공간 생성·역매핑·계획 검증.
    PlanGenerationFailed = 8,

    // 조립 단계: 생성 결과를 RoomRuntime으로 Binding.
    AssemblyFailed = 9,

    // Scene 준비 단계: Scene 검증·Player 배치·Navigation Scan.
    SceneSetupFailed = 10,

    // 배치 단계: Enemy 계획 수립과 원자적 Spawn.
    EnemyDeploymentFailed = 11,

    // Boss Arena 단계: Arena Root·Prefab·RoomView 구성이 유효하지 않거나,
    // Portal 대상 Combat Room에 DungeonBossPortal이 연결되지 않음.
    BossArenaUnavailable = 12,

    // 공개 단계와 그 밖의 중단 사유.
    SessionPublishFailed = 13,
    Cancelled = 14,
    UnexpectedFailure = 15,
}
