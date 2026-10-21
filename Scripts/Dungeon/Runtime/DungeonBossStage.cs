using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Boss Dungeon 전용 Arena Stage. Arena Instance·Boss Deployment와 전환에 다시 필요한 입력을 소유하고,
/// Portal 상호작용 한 번에 대한 단방향 전환을 수행한다. Run 상태 전이와 물리 Stage 정리 순서는 소유자인
/// DungeonRuntime이 계속 소유하며, 이 클래스는 그 조정 메서드를 호출한다(dungeon_system-design.md 4.2a).
/// </summary>
internal sealed class DungeonBossStage : IDisposable
{
    private const string BossArenaRoomId = "boss_arena";

    private readonly DungeonRuntime _runtime;

    // 전환 시점에 Arena Navigation·Boss Spawn을 다시 준비하기 위해 준비 단계의 입력을 보관한다.
    private DungeonBuildPlan _buildPlan;
    private PoolManager _poolManager;
    private AstarPath _astarPath;
    private Transform _enemyTarget;
    private PlayerCameraRig _playerCameraRig;
    private Func<EnemyActor, IEnemyNavigation> _navigationFactory;
    private string _playerSpawnPointId;

    private GameObject _arenaInstance;
    private RoomRuntime _arenaRoom;
    private DungeonEnemyDeployment _bossDeployment;
    private DungeonEnemySpawnEntry _pendingBossEntry;
    private bool _transitionInFlight;

    /// <summary>준비에 성공한 Boss Arena RoomRuntime. 준비 전이거나 실패했으면 null이다.</summary>
    public RoomRuntime ArenaRoom => _arenaRoom;

    internal DungeonBossStage(DungeonRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    // Boss Arena Prefab을 Boss Arena Root 아래 비활성 Instance로 준비하고, Portal 대상 Combat Room의
    // DungeonBossPortal을 활성화해 상호작용을 켠다. Edgar 물리 Graph·Door/Connection 집계와 독립적인
    // 별도 Stage이므로 DungeonRuntimeBinder를 거치지 않고 직접 Instantiate·TryInitialize한다
    // (dungeon_system-design.md 4.2a). 실제 단방향 전환은 Portal 상호작용 시점에 수행한다.
    internal DungeonBootstrapResult Prepare(DungeonBuildPlan plan, Transform bossArenaRoot)
    {
        RoomArchetypeSO bossArenaArchetype = plan.Definition.BossArenaRoom;

        if (bossArenaRoot == null || bossArenaArchetype == null || bossArenaArchetype.RoomPrefab == null)
        {
            return _runtime.Fail(
                DungeonBootstrapFailure.BossArenaUnavailable,
                "Boss Arena Root 또는 DungeonDefinitionSO.BossArenaRoom(Prefab) 참조가 비어 있습니다.");
        }

        if (string.IsNullOrEmpty(plan.PortalTargetRoomId))
        {
            return _runtime.Fail(
                DungeonBootstrapFailure.BossArenaUnavailable,
                "Dungeon 계획에 Boss Portal 대상 Combat Room ID가 없습니다.");
        }

        RoomRuntime portalRoom = FindRoomById(_runtime.PreparedRooms, plan.PortalTargetRoomId);

        if (portalRoom == null || portalRoom.View.BossPortal == null)
        {
            return _runtime.Fail(
                DungeonBootstrapFailure.BossArenaUnavailable,
                $"Boss Portal 대상 Room '{plan.PortalTargetRoomId}'에 DungeonBossPortal이 연결되지 않았습니다.");
        }

        GameObject instance = UnityEngine.Object.Instantiate(bossArenaArchetype.RoomPrefab, bossArenaRoot);
        instance.SetActive(false);
        RoomView view = instance.GetComponent<RoomView>();

        if (view == null)
        {
            DungeonAssembly.DestroyInstance(instance);
            return _runtime.Fail(
                DungeonBootstrapFailure.BossArenaUnavailable, "Boss Arena 구성이 유효하지 않습니다 - RoomView가 없습니다.");
        }

        if (!view.TryInitialize(BossArenaRoomId, RoomRole.Boss, out string failureReason))
        {
            DungeonAssembly.DestroyInstance(instance);
            return _runtime.Fail(
                DungeonBootstrapFailure.BossArenaUnavailable, $"Boss Arena 구성이 유효하지 않습니다 - {failureReason}");
        }

        _arenaInstance = instance;
        _arenaRoom = new RoomRuntime(new DungeonRoomPlan(BossArenaRoomId, RoomRole.Boss, bossArenaArchetype), view);
        portalRoom.View.BossPortal.Activate(HandlePortalActivationRequested);
        return DungeonBootstrapResult.Success();
    }

    // Session 공개 직전에 전환에 필요한 입력과 보관해 둔 Boss Entry를 받는다.
    internal void Arm(
        DungeonBuildPlan plan,
        PoolManager poolManager,
        AstarPath astarPath,
        Transform enemyTarget,
        PlayerCameraRig playerCameraRig,
        Func<EnemyActor, IEnemyNavigation> navigationFactory,
        string playerSpawnPointId,
        DungeonEnemySpawnEntry bossEntry)
    {
        _buildPlan = plan;
        _poolManager = poolManager;
        _astarPath = astarPath;
        _enemyTarget = enemyTarget;
        _playerCameraRig = playerCameraRig;
        _navigationFactory = navigationFactory;
        _playerSpawnPointId = playerSpawnPointId;
        _pendingBossEntry = bossEntry;
    }

    // Enemy Plan의 Boss Kind Entry(최대 1개)를 물리 Stage 원자적 Spawn 대상에서 분리한다.
    internal static void SplitPhysicalAndBossEntries(
        IReadOnlyList<DungeonEnemySpawnEntry> entries,
        out List<DungeonEnemySpawnEntry> physicalEntries,
        out DungeonEnemySpawnEntry bossEntry)
    {
        physicalEntries = new List<DungeonEnemySpawnEntry>(entries.Count);
        bossEntry = null;

        for (int i = 0; i < entries.Count; i++)
        {
            DungeonEnemySpawnEntry entry = entries[i];

            if (entry.Kind == DungeonSpawnPointKind.Boss)
            {
                bossEntry = entry;
            }
            else
            {
                physicalEntries.Add(entry);
            }
        }
    }

    // Arena Enemy와 Arena Instance를 정리한다. 호출은 소유자인 DungeonRuntime.Dispose()가 한다.
    public void Dispose()
    {
        _bossDeployment?.Dispose();
        _bossDeployment = null;
        _arenaRoom = null;
        DungeonAssembly.DestroyInstance(_arenaInstance);
        _arenaInstance = null;
    }

    // Boss Portal 상호작용을 받아 단방향 Arena 전환을 시작한다.
    private void HandlePortalActivationRequested()
    {
        TransitionAsync().Forget();
    }

    // Portal 상호작용 성공 뒤 물리 Deployment 반환 → 물리 Navigation 정리·Root 비활성 → Arena 활성·Player
    // 배치 → Arena Navigation Scan → Boss 단일 Spawn → Session Arena 등록 순서로 단방향 전환한다
    // (dungeon_system-design.md 4.2a). 전환은 한 번만 수행하며, 중간에 실패하면 Run을 Failed로 한 번만
    // 확정한다 - 물리 Dungeon으로 되돌아가지 않는다.
    private async UniTaskVoid TransitionAsync()
    {
        if (_transitionInFlight || _runtime.CurrentSession == null || _arenaRoom == null ||
            _arenaInstance == null || _pendingBossEntry == null)
        {
            return;
        }

        _transitionInFlight = true;

        try
        {
            _runtime.TearDownPhysicalStage();

            _arenaInstance.SetActive(true);

            if (!DungeonRuntime.TryPlacePlayerAtRoom(
                _arenaRoom.View, _enemyTarget, _playerCameraRig, _playerSpawnPointId))
            {
                FailTransition($"Boss Arena에서 \"{_playerSpawnPointId}\" Spawn Point를 찾지 못했습니다.");
                return;
            }

            await UniTask.NextFrame(PlayerLoopTiming.LastEarlyUpdate);

            // Await 재개 시점에 Player 사망·Scene 종료 등으로 Run이 이미 Failed/Disposed로 끝났을 수 있다.
            // 이 경우 Session·Runtime 필드가 이미 정리됐으므로 더 진행하지 않고 조용히 중단한다(FR-14, NFR-02).
            if (_runtime.CurrentSession == null || _runtime.CurrentSession.HasEnded)
            {
                return;
            }

            DungeonNavigationController arenaNavigationController = new DungeonNavigationController(_astarPath, _buildPlan);

            if (!arenaNavigationController.TryInitialize(
                new List<RoomRuntime> { _arenaRoom }, out string arenaNavigationFailureReason))
            {
                arenaNavigationController.Dispose();
                FailTransition($"Boss Arena Navigation 준비에 실패했습니다: {arenaNavigationFailureReason}");
                return;
            }

            _runtime.AdoptNavigationController(arenaNavigationController);

            DungeonEnemyPlan bossPlan = new DungeonEnemyPlan(new List<DungeonEnemySpawnEntry> { _pendingBossEntry });
            DungeonEnemyDeploymentResult bossDeploymentResult = new DungeonEnemyDeployer().Deploy(
                bossPlan,
                new List<RoomRuntime> { _arenaRoom },
                new EnemyInstanceProvider(_poolManager),
                _enemyTarget,
                _navigationFactory);

            if (!bossDeploymentResult.Succeeded)
            {
                FailTransition($"Boss Spawn에 실패했습니다: {bossDeploymentResult.Failure}");
                return;
            }

            _bossDeployment = bossDeploymentResult.Deployment;
            _pendingBossEntry = null;

            EnemyActor bossActor = _bossDeployment.Entries.Count > 0 ? _bossDeployment.Entries[0].Actor : null;
            _runtime.RegisterBossActor(bossActor);

            if (!_runtime.CurrentSession.EnterArenaRoom(_arenaRoom))
            {
                FailTransition("Session이 Boss Arena로 전환되지 않았습니다.");
            }
        }
        catch (Exception exception)
        {
            FailTransition($"Boss Arena 전환 중 예외가 발생했습니다: {exception.Message}");
        }
    }

    // 전환 실패를 Run 종료(Failed)로 한 번만 확정한다. Session.TryEnd가 중복 호출을 거부하므로 추가 Guard가
    // 필요 없고, 뒤이은 Dispose()가 부분 생성된 Arena Enemy/Root/Navigation을 모두 정리한다.
    private void FailTransition(string message)
    {
        Logger.LogError($"[Dungeon] Boss Arena 전환 실패: {message}");
        _runtime.CurrentSession?.TryEnd(DungeonRunResult.Failed);
    }

    private static RoomRuntime FindRoomById(IReadOnlyList<RoomRuntime> rooms, string roomId)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i] != null && string.Equals(rooms[i].RoomId, roomId, StringComparison.Ordinal))
            {
                return rooms[i];
            }
        }

        return null;
    }
}
