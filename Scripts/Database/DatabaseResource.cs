using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 게임 전체 수명 동안 루트 데이터 핸들 하나만 소유하는 명시적 초기화 서비스
/// </summary>
    public sealed class DatabaseResource : MonoBehaviour
    {
        public const string DatabaseAddress = "game_database";

        private static DatabaseResource _instance;

        private AsyncOperationHandle<GameDatabaseSO> _databaseHandle;
        private CancellationTokenSource _initializationCancellation;
        private UniTask _initializationTask;
        private GameDatabaseSO _database;
        private bool _isInitializing;

        public static DatabaseResource Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                _instance = FindFirstObjectByType<DatabaseResource>();
                if (_instance != null)
                {
                    return _instance;
                }

                GameObject serviceObject = new(nameof(DatabaseResource));
                return serviceObject.AddComponent<DatabaseResource>();
            }
        }

        public bool IsInitialized => _database != null && _databaseHandle.IsValid();
        public GameDatabaseSO Database => GetDatabase();

        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized)
            {
                return UniTask.CompletedTask;
            }

            if (_isInitializing)
            {
                // 공유 로드는 유지하되 후속 호출자는 자신의 수명에 맞춰 대기만 취소 가능
                return _initializationTask.AttachExternalCancellation(cancellationToken);
            }

            _isInitializing = true;
            _initializationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                destroyCancellationToken);
            _initializationTask = InitializeCoreAsync(_initializationCancellation.Token).Preserve();
            return _initializationTask;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // 씬 진입점과 같은 GameObject에 붙을 수 있으므로 중복 서비스 컴포넌트만 정리
                Destroy(this);
                return;
            }

            if (Application.isPlaying && GetComponent<GameDatabaseBootstrap>() != null)
            {
                // DontDestroyOnLoad가 Bootstrap까지 영속화하지 않도록 서비스 전용 루트로 분리
                GameObject serviceObject = new(nameof(DatabaseResource));
                serviceObject.AddComponent<DatabaseResource>();
                Destroy(this);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private async UniTask InitializeCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _databaseHandle = Addressables.LoadAssetAsync<GameDatabaseSO>(DatabaseAddress);
                GameDatabaseSO loadedDatabase = await _databaseHandle.ToUniTask(
                    cancellationToken: cancellationToken,
                    cancelImmediately: true,
                    autoReleaseWhenCanceled: true);

                if (loadedDatabase == null)
                {
                    throw new InvalidOperationException("로드된 게임 데이터베이스가 비어 있습니다.");
                }

                _database = loadedDatabase;
            }
            catch (OperationCanceledException)
            {
                ReleaseDatabaseHandle();
                throw;
            }
            catch (Exception exception)
            {
                ReleaseDatabaseHandle();
                throw new InvalidOperationException(
                    $"게임 데이터베이스 '{DatabaseAddress}' 로드에 실패했습니다.",
                    exception);
            }
            finally
            {
                _initializationCancellation?.Dispose();
                _initializationCancellation = null;
                _initializationTask = default;
                _isInitializing = false;
            }
        }

        private GameDatabaseSO GetDatabase()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "DatabaseResource.InitializeAsync가 완료되기 전에 게임 데이터에 접근했습니다.");
            }

            return _database;
        }

        private void OnDestroy()
        {
            _initializationCancellation?.Cancel();
            ReleaseDatabaseHandle();
            _initializationCancellation?.Dispose();
            _initializationCancellation = null;
            _initializationTask = default;
            _isInitializing = false;

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void ReleaseDatabaseHandle()
        {
            if (_databaseHandle.IsValid())
            {
                Addressables.Release(_databaseHandle);
            }

            _databaseHandle = default;
            _database = null;
        }
    }
