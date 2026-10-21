using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Pathfinding;
using UnityEngine;

/// <summary>
/// Dungeon Scene Composition Root. Scene 직렬화 참조를 보관하고 DungeonRuntime의 생성·시작·Dispose
/// 연결만 담당한다. Generation·Room·Encounter·Spawn·Navigation 세부 순서는 DungeonRuntime이 조정한다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class DungeonBootstrap : MonoBehaviour
{
    [Header("런타임 참조")]
    [SerializeField] private PoolManager _poolManager;
    [SerializeField] private AstarPath _astarPath;
    [SerializeField] private Transform _roomParent;
    [Tooltip("Encounter Enemy가 추적할 대상(Player).")]
    [SerializeField] private Transform _enemyTarget;
    [Tooltip("Entrance 최초 배치와 Boss Arena 전환 배치 직후 Camera를 Player 위치로 끊어 맞출 Rig. " +
        "비워 두면 위치만 맞추고 Camera는 다음 추적 Update에서 따라온다.")]
    [SerializeField] private PlayerCameraRig _playerCameraRig;
    [Tooltip("Boss Dungeon에서 BossArenaRoom Prefab을 비활성으로 준비해 둘 별도 Root. Normal Dungeon에서는 사용하지 않는다.")]
    [SerializeField] private Transform _bossArenaRoot;

    [Space(10)]
    [Header("Encounter Navigation")]
    // TODO(dungeon-system-update-plan 2단계 TBD): 실제 콘텐츠 튜닝 전까지 쓰는 임시 보수적 기본값.
    [SerializeField] private float _navigationTargetMoveThreshold = 0.5f;
    [SerializeField] private float _navigationWaypointDistance = 0.1f;
    [SerializeField] private float _navigationDestinationDistance = 0.1f;

    [Space(10)]
    [Header("공개 제어")]
    [Tooltip("Session과 Navigation 준비가 끝난 뒤에만 활성화합니다.")]
    [SerializeField] private GameObject[] _gameplayRoots = Array.Empty<GameObject>();

    private const string PlayerSpawnPointId = "player_spawn";

    private DungeonRuntime _runtime;
    private DungeonRunResultConnector _resultConnector;
    private CancellationTokenSource _initializationCancellation;
    private bool _isInitializing;
    private int _disposedAtFrame = -1;

    public DungeonSession CurrentSession => _runtime?.CurrentSession;
    public DungeonNavigationContext NavigationContext => _runtime?.NavigationContext;
    public bool IsReady => _runtime != null && _runtime.IsReady;

    private void Awake()
    {
        SetGameplayRootsActive(false);
    }

    private void OnDestroy()
    {
        DisposeDungeon();
    }

    // 선택한 수배서로 DungeonRuntime을 생성하고 시작을 위임한 뒤, 성공 시 Dispose 연결만 수행한다.
    public async UniTask<DungeonBootstrapResult> InitializeAsync(
        WantedBoardSlot selectedSlot,
        DungeonRunResultHandler resultHandler = null,
        int boardRefreshSeed = 0,
        CancellationToken cancellationToken = default)
    {
        if (_isInitializing || _runtime != null)
        {
            return DungeonBootstrapResult.Failed(
                DungeonBootstrapFailure.AlreadyInitialized,
                "이미 Dungeon을 준비 중이거나 공개한 상태입니다.");
        }

        if (_disposedAtFrame == Time.frameCount)
        {
            return DungeonBootstrapResult.Failed(
                DungeonBootstrapFailure.DisposalPending,
                "직전 Dispose가 같은 프레임에 끝나지 않아 다음 프레임부터 재생성할 수 있습니다.");
        }

        if (!HasValidSceneConfiguration())
        {
            return DungeonBootstrapResult.Failed(
                DungeonBootstrapFailure.InvalidConfiguration,
                "PoolManager, AstarPath, Room Parent, Enemy Target 참조가 필요합니다.");
        }

        _isInitializing = true;
        SetGameplayRootsActive(false);
        CancellationTokenSource initializationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            destroyCancellationToken);
        _initializationCancellation = initializationCancellation;
        _runtime = new DungeonRuntime();

        try
        {
            DungeonBootstrapResult result = await _runtime.StartAsync(
                selectedSlot,
                _poolManager,
                _astarPath,
                _roomParent,
                _enemyTarget,
                _playerCameraRig,
                BuildEnemyNavigation,
                PlayerSpawnPointId,
                _bossArenaRoot,
                initializationCancellation.Token);

            if (!result.Succeeded)
            {
                return FailAndCleanup(result.Failure, result.Message);
            }

            _resultConnector = new DungeonRunResultConnector(
                _runtime.CurrentSession,
                resultHandler,
                selectedSlot.Poster,
                boardRefreshSeed,
                () => SetGameplayRootsActive(false),
                DisposeDungeon);

            SetGameplayRootsActive(true);
            return result;
        }
        finally
        {
            if (_initializationCancellation == initializationCancellation)
            {
                _initializationCancellation = null;
            }

            initializationCancellation.Dispose();
            _isInitializing = false;
        }
    }

    // Encounter가 Enemy별 A* Navigation을 현재 Dungeon 수명에 연결하도록 위임한다.
    public AstarEnemyNavigation CreateEnemyNavigation(
        Seeker seeker,
        float targetMoveThreshold,
        float waypointDistance,
        float destinationDistance)
    {
        // Session 공개(IsReady) 이전, FinishPreparation의 Enemy 전체 Spawn 단계에서도 호출되므로
        // 여기서는 Runtime 존재 여부만 확인하고 실제 Navigation 준비 판단은 Runtime에 위임한다.
        if (_runtime == null)
        {
            return null;
        }

        return _runtime.CreateEnemyNavigation(
            seeker,
            targetMoveThreshold,
            waypointDistance,
            destinationDistance);
    }

    // Gameplay를 먼저 숨기고 Dispose 연결을 역순으로 정리한다. Room GameObject의 실제 파괴는 이번 프레임
    // 끝까지 미뤄지므로, 같은 프레임 재생성은 거부하고 다음 프레임부터 허용한다.
    public void DisposeDungeon()
    {
        _initializationCancellation?.Cancel();
        SetGameplayRootsActive(false);
        _resultConnector?.Dispose();
        _resultConnector = null;
        _runtime?.Dispose();
        _runtime = null;
        _disposedAtFrame = Time.frameCount;
    }

    // Scene 참조가 Bootstrap 시작 조건을 만족하는지 확인한다.
    private bool HasValidSceneConfiguration()
    {
        return _poolManager != null && _astarPath != null && _roomParent != null && _enemyTarget != null;
    }

    // Enemy의 Seeker를 찾아 준비 완료된 Navigation Factory를 지연 호출한다. Enemy 실제 대여 시점에만 호출되므로
    // 아직 Navigation이 준비되지 않은 조립 시점에 미리 만들어 넘겨도 안전하다.
    private IEnemyNavigation BuildEnemyNavigation(EnemyActor actor)
    {
        Seeker seeker = actor.GetComponent<Seeker>();

        if (seeker == null)
        {
            return null;
        }

        return CreateEnemyNavigation(
            seeker,
            _navigationTargetMoveThreshold,
            _navigationWaypointDistance,
            _navigationDestinationDistance);
    }

    // 실패한 준비에서 공개 상태와 부분 생성 객체를 모두 되돌린다.
    private DungeonBootstrapResult FailAndCleanup(DungeonBootstrapFailure failure, string message)
    {
        DisposeDungeon();
        return DungeonBootstrapResult.Failed(failure, message);
    }

    // 준비 완료 전에는 Bootstrap 자신을 제외한 Gameplay Root를 노출하지 않는다.
    private void SetGameplayRootsActive(bool isActive)
    {
        if (_gameplayRoots == null)
        {
            return;
        }

        for (int i = 0; i < _gameplayRoots.Length; i++)
        {
            GameObject gameplayRoot = _gameplayRoots[i];

            if (gameplayRoot != null && gameplayRoot != gameObject)
            {
                gameplayRoot.SetActive(isActive);
            }
        }
    }
}
