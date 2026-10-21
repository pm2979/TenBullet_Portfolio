using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Dungeon Run Coordinator. Database 확인 이후 Generation·Room Binding·Encounter 조립·Player 배치·
/// Navigation 준비·Session 공개까지 하위 시스템 실행 순서를 직접 조정하고, 7.1 상태표
/// (Idle/Preparing/Ready/Running/Cleared/Failed/Cancelled/Disposed)의 전이를 소유한다.
/// </summary>
public sealed class DungeonRuntime : IDisposable
{
    private readonly DungeonTopologyPlanner _topologyPlanner = new DungeonTopologyPlanner();
    private readonly EdgarGenerationAdapter _edgarAdapter = new EdgarGenerationAdapter();
    private readonly DungeonPlanValidator _validator = new DungeonPlanValidator();
    private DungeonAssembly _assembly;
    private DungeonSession _pendingSession;
    private DungeonNavigationController _navigationController;
    private DungeonObjectiveTracker _objectiveTracker;
    private DungeonBossStage _bossStage;

    public DungeonRunState State { get; private set; } = DungeonRunState.Idle;
    public DungeonSession CurrentSession { get; private set; }
    public DungeonNavigationContext NavigationContext => _navigationController?.Context;
    public bool IsReady => CurrentSession != null && NavigationContext != null && NavigationContext.IsReady;
    public IReadOnlyList<RoomRuntime> PreparedRooms =>
        _assembly != null ? _assembly.Rooms : Array.Empty<RoomRuntime>();

    // 제품 진입점: Database 확인부터 Edgar 생성·Room Binding·Encounter 조립·Player 배치·Navigation
    // 준비·Session 공개까지 6.1 순서 그대로 조정한다. 실패 시 Cancelled로 전이하며 자원 정리는 호출자가
    // Dispose()로 수행한다.
    public async UniTask<DungeonBootstrapResult> StartAsync(
        WantedBoardSlot selectedSlot,
        PoolManager poolManager,
        AstarPath astarPath,
        Transform roomParent,
        Transform enemyTarget,
        PlayerCameraRig playerCameraRig,
        Func<EnemyActor, IEnemyNavigation> navigationFactory,
        string playerSpawnPointId,
        Transform bossArenaRoot = null,
        CancellationToken cancellationToken = default)
    {
        if (State != DungeonRunState.Idle)
        {
            return DungeonBootstrapResult.Failed(
                DungeonBootstrapFailure.AlreadyInitialized,
                "이미 준비 중이거나 공개된 Dungeon Runtime입니다.");
        }

        if (poolManager == null || astarPath == null || roomParent == null || enemyTarget == null)
        {
            return Fail(
                DungeonBootstrapFailure.InvalidConfiguration,
                "PoolManager, AstarPath, Room Parent, Enemy Target 참조가 필요합니다.");
        }

        SetState(DungeonRunState.Preparing);
        bool databaseInitialized = false;

        try
        {
            DatabaseResource databaseResource = DatabaseResource.Instance;
            await databaseResource.InitializeAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            databaseInitialized = true;

            if (!poolManager.IsInitialized)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastEarlyUpdate, cancellationToken);
            }

            GameDatabaseSO database = databaseResource.Database;
            WantedPosterSO poster = selectedSlot.Poster;
            DungeonDefinitionSO definition = poster != null ? poster.DungeonDefinition : null;

            if (!ContainsReference(database.WantedPosters, poster) ||
                !ContainsReference(database.DungeonDefinitions, definition))
            {
                return Fail(
                    DungeonBootstrapFailure.ContentUnavailable,
                    "선택한 수배서 또는 Dungeon 정의가 GameDatabaseSO에 등록되지 않았습니다.");
            }

            if (!poolManager.IsInitialized)
            {
                return Fail(DungeonBootstrapFailure.PoolUnavailable, "PoolManager가 초기화되지 않았습니다.");
            }

            DungeonDataValidationResult staticGateResult = DungeonStaticContentGate.Validate(
                database, poolManager, poster);

            if (staticGateResult.HasErrors)
            {
                return Fail(DungeonBootstrapFailure.StaticValidationFailed, BuildJoinedMessage(staticGateResult.Errors));
            }

            if (!DungeonPlanRules.HasValidDefinition(definition))
            {
                return Fail(
                    DungeonBootstrapFailure.PlanGenerationFailed,
                    "Dungeon 정의가 계획 생성에 필요한 최소 계약을 만족하지 않습니다.");
            }

            DungeonTopologyPlan topology = _topologyPlanner.Create(definition, selectedSlot.DungeonSeed);
            EdgarGenerationResult edgarResult = _edgarAdapter.Generate(
                definition, topology, selectedSlot.DungeonSeed, definition.GenerationVersion);

            if (!edgarResult.Succeeded)
            {
                return Fail(
                    DungeonBootstrapFailure.PlanGenerationFailed,
                    $"Dungeon 공간 생성에 실패했습니다: {edgarResult.Failure}");
            }

            DungeonPlanValidationResult validation = _validator.Validate(edgarResult.Plan);

            if (!validation.IsValid)
            {
                DestroyEdgarGeneration(edgarResult);
                return Fail(
                    DungeonBootstrapFailure.PlanGenerationFailed,
                    $"Dungeon 계획 검증에 실패했습니다:\n{string.Join("\n", validation.Errors)}");
            }

            DungeonAssemblyResult assemblyResult = BuildFromEdgarGeneration(edgarResult, roomParent);

            if (!assemblyResult.Succeeded)
            {
                return Fail(DungeonBootstrapFailure.AssemblyFailed, assemblyResult.Message);
            }

            return FinishPreparation(
                edgarResult.Plan,
                poolManager,
                astarPath,
                enemyTarget,
                playerCameraRig,
                playerSpawnPointId,
                poster,
                selectedSlot.DungeonSeed,
                navigationFactory,
                bossArenaRoot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Fail(DungeonBootstrapFailure.Cancelled, "Dungeon 준비가 취소되었습니다.");
        }
        catch (Exception exception)
        {
            return Fail(
                databaseInitialized
                    ? DungeonBootstrapFailure.UnexpectedFailure
                    : DungeonBootstrapFailure.DatabaseInitializationFailed,
                databaseInitialized
                    ? $"Dungeon 준비 중 예외가 발생했습니다: {exception.Message}"
                    : $"게임 데이터베이스 초기화에 실패했습니다: {exception.Message}");
        }
    }

    // Edgar가 이미 Instantiate한 Room Instance를 Binding해 준비하고 외부 공개는 보류한다.
    public DungeonAssemblyResult BuildFromEdgarGeneration(EdgarGenerationResult edgarResult, Transform roomParent)
    {
        if (_assembly != null || _pendingSession != null || CurrentSession != null)
        {
            return DungeonAssemblyResult.Failed(
                DungeonAssemblyFailure.AlreadyBuilt,
                "이미 준비되거나 공개된 Dungeon이 있습니다.");
        }

        DungeonAssemblyResult assemblyResult = new DungeonRuntimeBinder().Bind(
            edgarResult.Plan,
            edgarResult.RoomInstancesByRoomId,
            edgarResult.GeneratedRoot,
            roomParent);

        if (!assemblyResult.Succeeded)
        {
            return assemblyResult;
        }

        _assembly = assemblyResult.Assembly;
        _pendingSession = new DungeonSession(_assembly.Rooms);
        SetState(DungeonRunState.Preparing);
        return assemblyResult;
    }

    // Room 조립 완료 이후 Scene 검증·Player 배치·Navigation 준비·Enemy 전체 원자적 Spawn·Session 공개를
    // 이 순서 그대로 수행한다. Room 진입은 더 이상 Spawn을 트리거하지 않는다.
    internal DungeonBootstrapResult FinishPreparation(
        DungeonBuildPlan plan,
        PoolManager poolManager,
        AstarPath astarPath,
        Transform enemyTarget,
        PlayerCameraRig playerCameraRig,
        string playerSpawnPointId,
        WantedPosterSO poster,
        int seed,
        Func<EnemyActor, IEnemyNavigation> navigationFactory,
        Transform bossArenaRoot = null)
    {
        DungeonDataValidationResult sceneGateResult = DungeonSceneValidationGate.Validate(
            PreparedRooms, astarPath, enemyTarget.gameObject.layer);

        if (sceneGateResult.HasErrors)
        {
            return Fail(DungeonBootstrapFailure.SceneSetupFailed, BuildJoinedMessage(sceneGateResult.Errors));
        }

        if (!TryPlacePlayerAtEntrance(PreparedRooms, enemyTarget, playerCameraRig, playerSpawnPointId))
        {
            return Fail(
                DungeonBootstrapFailure.SceneSetupFailed,
                $"Entrance Room에서 \"{playerSpawnPointId}\" Spawn Point를 찾지 못했습니다.");
        }

        _navigationController = new DungeonNavigationController(astarPath, plan);
        if (!_navigationController.TryInitialize(PreparedRooms, out string navigationFailureReason))
        {
            return Fail(DungeonBootstrapFailure.SceneSetupFailed, navigationFailureReason);
        }

        if (plan.Definition.DungeonType == DungeonType.Boss)
        {
            _bossStage = new DungeonBossStage(this);
            DungeonBootstrapResult bossArenaResult = _bossStage.Prepare(plan, bossArenaRoot);

            if (!bossArenaResult.Succeeded)
            {
                return bossArenaResult;
            }
        }

        List<DungeonSpawnPointSnapshot> spawnPoints = BuildSpawnPointSnapshots(PreparedRooms);

        if (_bossStage?.ArenaRoom != null)
        {
            spawnPoints.AddRange(BuildSpawnPointSnapshots(new[] { _bossStage.ArenaRoom }));
        }

        DungeonEnemyPlanResult planResult = new DungeonEnemyPlanner().Create(poster, spawnPoints, seed);

        if (!planResult.Succeeded)
        {
            return Fail(
                DungeonBootstrapFailure.EnemyDeploymentFailed,
                $"Enemy Spawn 계획 생성에 실패했습니다: {planResult.Failure} {planResult.Message}");
        }

        // Boss는 Arena Navigation 준비 성공 후 한 번만 Spawn한다(dungeon_system-design.md 2.2) - 물리
        // Stage의 원자적 Spawn에서는 Boss Entry를 제외하고 Portal 전환 때까지 보관한다.
        DungeonBossStage.SplitPhysicalAndBossEntries(
            planResult.Plan.Entries, out List<DungeonEnemySpawnEntry> physicalEntries, out DungeonEnemySpawnEntry bossEntry);

        if (_bossStage?.ArenaRoom != null && bossEntry == null)
        {
            return Fail(
                DungeonBootstrapFailure.EnemyDeploymentFailed, "Boss Dungeon 계획에 Boss Spawn Entry가 없습니다.");
        }

        DungeonEnemyDeploymentResult deploymentResult = new DungeonEnemyDeployer().Deploy(
            new DungeonEnemyPlan(physicalEntries),
            PreparedRooms,
            new EnemyInstanceProvider(poolManager),
            enemyTarget,
            navigationFactory);

        if (!deploymentResult.Succeeded)
        {
            return Fail(
                DungeonBootstrapFailure.EnemyDeploymentFailed,
                $"Enemy 전체 Spawn에 실패했습니다: {deploymentResult.Failure}");
        }

        _assembly.AttachEnemyDeployment(deploymentResult.Deployment);

        DungeonObjectiveTracker objectiveTracker = new DungeonObjectiveTracker(
            poster.ObjectiveType, deploymentResult.Deployment.Entries);
        _pendingSession.AttachObjectiveTracker(objectiveTracker);
        _objectiveTracker = objectiveTracker;

        _bossStage?.Arm(
            plan,
            poolManager,
            astarPath,
            enemyTarget,
            playerCameraRig,
            navigationFactory,
            playerSpawnPointId,
            bossEntry);

        if (!PublishSession())
        {
            return Fail(DungeonBootstrapFailure.SessionPublishFailed, "준비된 DungeonSession을 공개하지 못했습니다.");
        }

        return DungeonBootstrapResult.Success();
    }

    // 조립된 Room의 Spawn Point를 DungeonEnemyPlanner 입력용 snapshot으로 옮긴다. Id가 비어 있는
    // Point는 계획이 참조할 수 없으므로 제외한다.
    private static List<DungeonSpawnPointSnapshot> BuildSpawnPointSnapshots(IReadOnlyList<RoomRuntime> rooms)
    {
        List<DungeonSpawnPointSnapshot> snapshots = new List<DungeonSpawnPointSnapshot>();

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomRuntime room = rooms[i];
            IReadOnlyList<RoomSpawnPointView> spawnPoints = room?.View?.SpawnPoints;

            if (spawnPoints == null)
            {
                continue;
            }

            for (int j = 0; j < spawnPoints.Count; j++)
            {
                RoomSpawnPointView spawnPoint = spawnPoints[j];

                if (spawnPoint != null && !string.IsNullOrWhiteSpace(spawnPoint.SpawnPointId))
                {
                    snapshots.Add(new DungeonSpawnPointSnapshot(room.RoomId, spawnPoint.SpawnPointId, spawnPoint.Kind));
                }
            }
        }

        return snapshots;
    }

    // Navigation 등 후속 준비가 끝난 뒤 대기 중 Session을 현재 Session으로 공개하고 Ready로 전이한다.
    public bool PublishSession()
    {
        if (_pendingSession == null || CurrentSession != null)
        {
            return false;
        }

        CurrentSession = _pendingSession;
        _pendingSession = null;
        CurrentSession.Ended += HandleSessionEnded;
        SetState(DungeonRunState.Ready);
        Logger.Log($"[Dungeon] 던전 진입 완료: Room={PreparedRooms.Count}개");
        return true;
    }

    // Encounter가 Enemy별 A* Navigation을 현재 Dungeon 수명에 연결하도록 위임한다.
    public AstarEnemyNavigation CreateEnemyNavigation(
        Seeker seeker,
        float targetMoveThreshold,
        float waypointDistance,
        float destinationDistance)
    {
        // IsReady(CurrentSession 공개 여부)가 아니라 Navigation 준비 여부만 확인한다.
        // Enemy 전체 Spawn은 Session 공개(PublishSession) 이전, FinishPreparation 단계에서 일어난다.
        if (NavigationContext == null || !NavigationContext.IsReady)
        {
            return null;
        }

        return _navigationController.CreateEnemyNavigation(
            seeker,
            targetMoveThreshold,
            waypointDistance,
            destinationDistance);
    }

    // Session 구독을 먼저 해제하고 Navigation과 이번 Build의 Room을 역순으로 제거한다. 명시적 결과
    // (Cleared/Failed) 없이 호출되면 Cancelled로 확정한 뒤 Disposed로 전이한다.
    public void Dispose()
    {
        if (State == DungeonRunState.Disposed)
        {
            return;
        }

        if (State != DungeonRunState.Cleared && State != DungeonRunState.Failed &&
            State != DungeonRunState.Cancelled)
        {
            SetState(DungeonRunState.Cancelled);
        }

        _navigationController?.Dispose();
        _navigationController = null;

        DungeonSession session = CurrentSession ?? _pendingSession;

        if (CurrentSession != null)
        {
            CurrentSession.Ended -= HandleSessionEnded;
        }

        CurrentSession = null;
        _pendingSession = null;
        session?.Dispose();
        _objectiveTracker = null;
        _assembly?.Dispose();
        _assembly = null;

        _bossStage?.Dispose();
        _bossStage = null;

        SetState(DungeonRunState.Disposed);
    }

    // Session이 확정한 Run 결과를 Cleared/Failed로 그대로 반영한다.
    private void HandleSessionEnded(DungeonRunResult result)
    {
        SetState(result == DungeonRunResult.Cleared ? DungeonRunState.Cleared : DungeonRunState.Failed);
    }

    private void SetState(DungeonRunState state)
    {
        State = state;
    }

    // Boss Portal 전환의 첫 단계: 물리 Stage의 Enemy 반환 → Navigation 정리 → 생성 Root 비활성을
    // 이 순서 그대로 수행한다. 단방향 전환이므로 되돌리지 않는다(dungeon_system-design.md 4.2a).
    internal void TearDownPhysicalStage()
    {
        _assembly?.ReleasePhysicalEnemies();
        _navigationController?.Dispose();
        _navigationController = null;
        _assembly?.SetRootActive(false);
    }

    // Arena Scan에 성공한 Navigation Controller를 현재 Dungeon Navigation으로 받는다.
    internal void AdoptNavigationController(DungeonNavigationController controller)
    {
        _navigationController = controller;
    }

    // Boss Spawn 성공 직후에만 호출해 Boss 목표 완료 판정을 연다.
    internal void RegisterBossActor(EnemyActor bossActor)
    {
        _objectiveTracker?.RegisterBossActor(bossActor);
    }

    // 준비 중 실패를 Cancelled로 확정하고 실패 결과를 반환한다. 자원 정리는 호출자의 Dispose()가 수행한다.
    internal DungeonBootstrapResult Fail(DungeonBootstrapFailure failure, string message)
    {
        SetState(DungeonRunState.Cancelled);
        return DungeonBootstrapResult.Failed(failure, message);
    }

    // Entrance RoomRuntime의 player_spawn 위치로 Player Transform과 물리 상태를 맞춘다. Navigation Scan 전에
    // 호출해 초기 물리·카메라·Enemy Target 위치가 생성된 Entrance를 기준으로 하게 한다.
    private static bool TryPlacePlayerAtEntrance(
        IReadOnlyList<RoomRuntime> rooms,
        Transform enemyTarget,
        PlayerCameraRig playerCameraRig,
        string playerSpawnPointId)
    {
        RoomRuntime entranceRoom = null;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Role == RoomRole.Entrance)
            {
                entranceRoom = rooms[i];
                break;
            }
        }

        return entranceRoom != null &&
            TryPlacePlayerAtRoom(entranceRoom.View, enemyTarget, playerCameraRig, playerSpawnPointId);
    }

    // 지정 Room View의 Spawn Point로 Player Transform과 물리 상태를 맞춘다. Entrance 최초 배치와 Boss Arena
    // Portal 전환 배치가 이 로직을 공유한다. Camera Rig는 Bootstrap이 Inspector로 넘긴 참조를 쓰며,
    // 연결되지 않았으면 위치만 맞추고 Camera는 다음 추적 Update에서 따라온다.
    internal static bool TryPlacePlayerAtRoom(
        RoomView roomView,
        Transform enemyTarget,
        PlayerCameraRig playerCameraRig,
        string playerSpawnPointId)
    {
        if (roomView == null || !roomView.TryGetSpawnPoint(playerSpawnPointId, out Transform spawnTransform))
        {
            return false;
        }

        enemyTarget.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);

        Rigidbody2D playerBody = enemyTarget.GetComponent<Rigidbody2D>();

        if (playerBody != null)
        {
            playerBody.position = spawnTransform.position;
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        Physics2D.SyncTransforms();
        playerCameraRig?.SnapToPlayer();
        return true;
    }

    // Validation Gate가 모은 오류 목록을 원인과 콘텐츠 참조를 포함한 한 메시지로 합친다.
    private static string BuildJoinedMessage(IReadOnlyList<DungeonDataValidationIssue> errors)
    {
        string[] lines = new string[errors.Count];

        for (int i = 0; i < errors.Count; i++)
        {
            lines[i] = "- " + errors[i].Message;
        }

        return string.Join("\n", lines);
    }

    // ScriptableObject 목록에서 선택한 인스턴스가 Database 그래프에 속하는지 확인한다.
    private static bool ContainsReference<T>(IReadOnlyList<T> values, T target)
        where T : UnityEngine.Object
    {
        if (values == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    // Binder에 소유권이 넘어가기 전(게임 규칙 검증 실패 등)에 중단할 때 Edgar 생성 Root를 직접 정리한다.
    private static void DestroyEdgarGeneration(EdgarGenerationResult edgarResult)
    {
        if (edgarResult?.GeneratedRoot != null)
        {
            DungeonAssembly.DestroyInstance(edgarResult.GeneratedRoot);
        }
    }
}
