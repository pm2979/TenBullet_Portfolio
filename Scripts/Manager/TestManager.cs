using System.Collections;
using UnityEngine;

/// <summary>
/// GameManager 없이 테스트 씬에서 Player 등 이미 만들어진 기능과 앞으로 추가될 기능을 생성·초기화해 검증하기 위한 테스트 전용 진입점.
/// Player, PlayerHealthUI 등 대상 기능 스크립트는 이 클래스를 참조하지 않으며,
/// TestManager만 각 기능이 공개한 초기화 API를 호출해 단방향으로 의존한다.
/// </summary>
[DisallowMultipleComponent]
public class TestManager : MonoBehaviour
{
    [Header("플레이어")]
    [Tooltip("생성할 플레이어의 PlayerDataSO")]
    [SerializeField] private PlayerDataSO _playerDetails;

    [Space(10)]
    [Header("Enemy")]
    [Tooltip("생성할 Enemy의 EnemyArchetypeSO")]
    [SerializeField] private EnemyArchetypeSO _enemyArchetype;

    [Tooltip("TestManager 위치를 기준으로 Enemy를 생성할 상대 좌표")]
    [SerializeField] private Vector2 _enemySpawnOffset = new Vector2(5f, 0f);

    [Space(10)]
    [Header("UI")]
    [Tooltip("생성한 플레이어와 연결할 체력 UI")]
    [SerializeField] private PlayerHealthUI _playerHealthUI;

    private Player _player;

    private void Awake()
    {
        _player = SpawnPlayer();
    }

    private IEnumerator Start()
    {
        // PoolManager.Start의 풀 사전 생성을 기다린 뒤 Enemy와 투사체를 대여한다
        yield return null;

        SpawnEnemy(_player);
    }

    // 플레이어 프리팹을 현재 위치에 생성·초기화하고 체력 UI와 연결한다
    private Player SpawnPlayer()
    {
        GameObject playerGameObject = Instantiate(_playerDetails.playerPrefab, transform.position, Quaternion.identity);

        Player player = playerGameObject.GetComponent<Player>();
        player.Initialize(_playerDetails);

        _playerHealthUI.Initialize(player);

        return player;
    }

    // 기존 Enemy 대여·초기화 경계에 Player 타겟과 테스트용 직선 Navigation을 주입한다
    private void SpawnEnemy(Player player)
    {
        EnemyRuntimeConfig runtimeConfig = new EnemyRuntimeConfig(_enemyArchetype);
        EnemySpawnContext spawnContext = new EnemySpawnContext(
            player.transform,
            runtimeConfig,
            navigation: new DirectEnemyNavigation());

        EnemyInstanceProvider instanceProvider = new EnemyInstanceProvider(PoolManager.Instance);
        Vector2 spawnPosition = (Vector2)transform.position + _enemySpawnOffset;
        EnemyRentResult result = instanceProvider.Rent(
            _enemyArchetype,
            spawnContext,
            spawnPosition,
            Quaternion.identity);

        if (!result.IsSuccess)
        {
            Debug.LogError($"TestManager: Enemy 생성에 실패했습니다. ({result.FailureReason})", this);
        }
    }
}
