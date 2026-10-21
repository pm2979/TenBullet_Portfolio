using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 개발용 Poster·Seed로 Dungeon을 시작하고, Player 초기화·Camera/UI 연결·Session 이벤트 구독·
/// 목표 완료 표시·ExitDungeon Hold 입력 연결만 담당한다. Room 생성·Encounter 시작·문 Lock/Unlock은
/// 직접 호출하지 않으며, Exit Room 진입 여부는 더 이상 판정하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonSceneController : MonoBehaviour
{
    [Space(5)]
    [Header("개발용 입력")]
    [SerializeField] private WantedPosterSO _developmentPoster;
    [SerializeField] private bool _useRandomSeed;
    [SerializeField] private int _developmentSeed;

    [Space(10)]
    [Header("핵심 참조")]
    [SerializeField] private DungeonBootstrap _bootstrap;
    [SerializeField] private Player _player;
    [SerializeField] private PlayerDataSO _playerData;
    [SerializeField] private DungeonExitInputReader _exitInputReader;

    [Space(10)]
    [Header("Camera / UI 초기화 대상")]
    [SerializeField] private PlayerCameraRig _cameraTargetController;
    [SerializeField] private PlayerHealthUI _gameplayUI;

    [Space(10)]
    [Header("Scene UI Panel")]
    [SerializeField] private GameObject _loadingPanel;
    [SerializeField] private GameObject _escapeAvailablePanel;
    [SerializeField] private GameObject _abandonConfirmPanel;
    [SerializeField] private GameObject _resultPanel;

    private DungeonSession _session;
    private bool _hasStartedInitialization;

    /// <summary>
    /// 결과 UI가 Session 종료 결과를 소비할 수 있도록 노출하는 최소 이벤트.
    /// </summary>
    public event Action<DungeonRunResult> DungeonEnded;

    private void Awake()
    {
        SetPanelActive(_loadingPanel, false);
        SetPanelActive(_escapeAvailablePanel, false);
        SetPanelActive(_abandonConfirmPanel, false);
        SetPanelActive(_resultPanel, false);

        SetPlayerInputEnabled(false);
    }

    private void Start()
    {
        int seed = _useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : _developmentSeed;

        if (_useRandomSeed)
        {
            Logger.Log($"{nameof(DungeonSceneController)}: 랜덤 Seed({seed})로 Dungeon을 시작합니다.", this);
        }

        StartDungeonAsync(_developmentPoster, seed).Forget();
    }

    private void OnDestroy()
    {
        UnsubscribeSession();
        UnsubscribePlayer();
    }

    // Player 초기화, Camera/UI 연결, Bootstrap 준비, Session·Player 이벤트 구독을 문서화된 순서로 수행한다.
    private async UniTask StartDungeonAsync(WantedPosterSO poster, int seed)
    {
        // Start가 예기치 않게 다시 호출되어도 Session 이벤트가 중복 구독되지 않도록 최초 1회만 진행한다.
        if (_hasStartedInitialization)
        {
            return;
        }

        _hasStartedInitialization = true;

        if (poster == null || _bootstrap == null || _player == null || _playerData == null)
        {
            Logger.LogError($"{nameof(DungeonSceneController)}: 필수 참조가 비어 있어 Dungeon을 준비할 수 없습니다.", this);
            return;
        }

        SetPanelActive(_loadingPanel, true);

        _player.Initialize(_playerData);

        if (_cameraTargetController != null)
        {
            _cameraTargetController.Initialize(_player);
        }

        WantedBoardSlot slot = new WantedBoardSlot(poster.SlotType, poster, seed);
        DungeonBootstrapResult result = await _bootstrap.InitializeAsync(slot);

        SetPanelActive(_loadingPanel, false);

        if (!result.Succeeded)
        {
            Logger.LogError(
                $"{nameof(DungeonSceneController)}: Dungeon 준비 실패({result.Failure}) - {result.Message}",
                this);
            return;
        }

        // GameDatabaseSO 조회가 필요하므로(PlayerHealthUI.Initialize → DatabaseResource.Instance.Database),
        // InitializeAsync 내부에서 Database 초기화가 끝난 뒤인 이 시점에 연결한다.
        if (_gameplayUI != null)
        {
            _gameplayUI.Initialize(_player);
        }

        _session = _bootstrap.CurrentSession;

        if (_session == null)
        {
            Logger.LogError($"{nameof(DungeonSceneController)}: Bootstrap 성공 후에도 CurrentSession이 비어 있습니다.", this);
            return;
        }

        _session.ObjectiveCompleted += HandleObjectiveCompleted;
        _session.Ended += HandleSessionEnded;
        _player.Died += HandlePlayerDied;

        if (_exitInputReader != null)
        {
            _exitInputReader.ExitRequested += HandleExitRequested;
        }

        SetPlayerInputEnabled(true);
    }

    // 목표 완료 사실을 알리는 표시만 켠다. 탈출 자체는 ExitDungeon Hold 입력(HandleExitRequested)으로만 일어난다.
    private void HandleObjectiveCompleted()
    {
        SetPanelActive(_escapeAvailablePanel, true);
    }

    // ExitDungeon Hold가 완료된 순간 한 번 호출된다. TryEscape 자체가 목표 완료 전/Session 종료 후 호출을 거부한다.
    private void HandleExitRequested()
    {
        _session?.TryEscape();
    }

    // DungeonRunResultConnector가 이미 Gameplay 차단과 Dispose를 수행했으므로 CurrentSession을 다시 조회하지 않고
    // 이벤트 인자로 전달된 결과만 소비한다.
    private void HandleSessionEnded(DungeonRunResult result)
    {
        SetPlayerInputEnabled(false);
        SetPanelActive(_escapeAvailablePanel, false);
        SetPanelActive(_abandonConfirmPanel, false);
        SetPanelActive(_resultPanel, true);

        UnsubscribeSession();
        UnsubscribePlayer();

        DungeonEnded?.Invoke(result);
    }

    // Player 사망을 Dungeon 실패로 확정한다. Session.TryEnd가 중복 호출을 거부하므로 추가 Guard가 필요 없다.
    private void HandlePlayerDied()
    {
        _session?.TryEnd(DungeonRunResult.Failed);
    }

    /// <summary>중도 포기 안내 Panel을 연다.</summary>
    public void RequestAbandon()
    {
        SetPanelActive(_abandonConfirmPanel, true);
    }

    /// <summary>중도 포기 확인 Button이 호출한다.</summary>
    public void ConfirmAbandon()
    {
        SetPanelActive(_abandonConfirmPanel, false);
        _session?.TryEnd(DungeonRunResult.Failed);
    }

    /// <summary>중도 포기 안내를 닫고 Session을 유지한다.</summary>
    public void CancelAbandon()
    {
        SetPanelActive(_abandonConfirmPanel, false);
    }

    // Bootstrap 성공 전과 Session 종료 뒤에는 PlayerInputAdapter·PlayerController만 비활성화한다.
    // Player의 상위 GameObject는 건드리지 않아 Awake 가능한 활성 경계를 유지한다.
    private void SetPlayerInputEnabled(bool isEnabled)
    {
        if (_player == null)
        {
            return;
        }

        if (_player.playerController != null)
        {
            _player.playerController.enabled = isEnabled;
        }

        PlayerInputAdapter inputAdapter = _player.GetComponent<PlayerInputAdapter>();

        if (inputAdapter != null)
        {
            inputAdapter.enabled = isEnabled;
        }
    }

    private void UnsubscribeSession()
    {
        if (_exitInputReader != null)
        {
            _exitInputReader.ExitRequested -= HandleExitRequested;
        }

        if (_session == null)
        {
            return;
        }

        _session.ObjectiveCompleted -= HandleObjectiveCompleted;
        _session.Ended -= HandleSessionEnded;
        _session = null;
    }

    private void UnsubscribePlayer()
    {
        if (_player != null)
        {
            _player.Died -= HandlePlayerDied;
        }
    }

    private static void SetPanelActive(GameObject panel, bool isActive)
    {
        if (panel != null)
        {
            panel.SetActive(isActive);
        }
    }
}
