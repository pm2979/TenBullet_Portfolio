using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 전투 오브젝트보다 먼저 데이터베이스 초기화를 완료하고, 맵 생성 위치·카메라 경계를 연결한 뒤 게임 플레이를 시작하는 씬 진입점
/// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameDatabaseBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("데이터베이스 초기화가 완료된 뒤 활성화할 게임 플레이 루트")]
        private GameObject[] _gameplayRoots = Array.Empty<GameObject>();

        private bool _hasStartedInitialization;

        private void Awake()
        {
            SetGameplayRootsActive(false);
        }

        private void Start()
        {
            InitializeAsync(destroyCancellationToken).Forget();
        }

        private async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            // Start가 예기치 않게 다시 호출되어도 생성 위치·이벤트 상태가 중복 적용되지 않도록 최초 1회만 진행
            if (_hasStartedInitialization)
            {
                return;
            }

            _hasStartedInitialization = true;

            try
            {
                DatabaseResource databaseResource = DatabaseResource.Instance;
                await databaseResource.InitializeAsync(cancellationToken);

                SetGameplayRootsActive(true);
                await UniTask.NextFrame(PlayerLoopTiming.LastEarlyUpdate, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 씬 종료나 오브젝트 파괴에 따른 취소는 정상 수명 종료로 처리
            }
            catch (Exception exception)
            {
                // 활성화 이후 실패해도 Gameplay Root가 켜진 채로 남지 않도록 되돌림 (활성화 전 실패 시에는 무해한 반복 호출)
                SetGameplayRootsActive(false);
                Debug.LogError(
                    $"게임 데이터베이스 초기화에 실패하여 게임 플레이를 시작하지 않습니다.\n{exception}",
                    this);
            }
        }

        // 최초 진입과 향후 전투 재시작 모두에서 재사용할 수 있도록 생성 위치 적용만 분리한 작은 메서드
        private void ApplySpawnPositions()
        {
            // Auto Sync Transforms가 꺼져 있으므로 이후 Physics2D 질의(콜라이더 등)가 새 위치를 즉시 반영하도록 명시적 동기화
            Physics2D.SyncTransforms();
        }

        private static void MoveToSpawnPoint(Rigidbody2D body, Transform spawnPoint)
        {
            if (body == null || spawnPoint == null)
            {
                return;
            }

            // Auto Sync Transforms가 꺼져 있는 프로젝트 설정에서 Rigidbody2D.position 대입은 다음 물리 스텝 전까지
            // Transform에 반영되지 않아(같은 프레임 내 즉시 이동 실패) transform.position을 직접 대입한다
            body.transform.position = spawnPoint.position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private void SetGameplayRootsActive(bool isActive)
        {
            foreach (GameObject gameplayRoot in _gameplayRoots)
            {
                if (gameplayRoot != null && gameplayRoot != gameObject)
                {
                    gameplayRoot.SetActive(isActive);
                }
            }
        }
    }
