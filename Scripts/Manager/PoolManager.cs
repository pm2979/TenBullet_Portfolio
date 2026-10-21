using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

/// <summary>
/// 빈번한 게임 오브젝트 생성 및 파괴로 인한 메모리 파편화 및 가비지 컬렉션(GC) 부하 방지 목적.
/// 재사용 가능한 자원을 미리 확보하여 런타임 성능 안정성을 확보하기 위한 중앙 관리 시스템.
/// 제약 조건: 프리팹의 인스턴스 ID를 키로 사용하므로 동일 프리팹에 대한 중복 설정 주의 필요.
/// </summary>
[DisallowMultipleComponent]
public class PoolManager : MonoSingleton<PoolManager>
{
    [Tooltip("풀에 추가할 Component와, 각각 생성할 게임오브젝트 개수를 이 배열에 채워 넣는다.")]
    [SerializeField] private PoolDefinition[] _poolDefinitions = null;

    private Transform _poolRoot;
    private Transform _inactiveCreationRoot;
    // 빠른 조회를 위해 프리팹 ID와 런타임 풀을 매핑한 캐시 저장소.
    private readonly Dictionary<int, RuntimePool> _runtimePools = new Dictionary<int, RuntimePool>();
    // 대여/반환된 Component가 어느 풀 소속인지 판별하기 위한 역참조 매핑.
    private readonly Dictionary<Component, RuntimePool> _componentOwners = new Dictionary<Component, RuntimePool>();

    public bool IsInitialized { get; private set; }

    // 풀링 대상 Component와 초기 할당 수량을 정의하는 구성 데이터.
    [System.Serializable]
    private sealed class PoolDefinition
    {
        [Tooltip("풀링할 Prefab의 Component. 이 Component가 붙어 있는 GameObject가 그대로 Instantiate된다.")]
        [SerializeField] private Component _prefabComponent;

        [Tooltip("사전 생성 및 반환 상태로 유지할 최대 수량(동시 대여 상한 아님).")]
        [FormerlySerializedAs("poolSize")]
        [SerializeField] private int _initialSize;

        public Component PrefabComponent => _prefabComponent;
        public int InitialSize => _initialSize;
    }

    // 프리팹 하나에 대응하는 ObjectPool<Component>와 현재 대여 중인 Component 집합을 함께 관리하는 내부 단위.
    private sealed class RuntimePool
    {
        public ObjectPool<Component> Pool;
        public readonly HashSet<Component> RentedComponents = new HashSet<Component>();
    }

    private void Start()
    {
        if (IsInitialized)
        {
            return;
        }

        _poolRoot = transform;
        _inactiveCreationRoot = CreateInactiveCreationRoot();

        InitializePools();
        IsInitialized = true;
    }

    // Dungeon Bootstrap이 Gameplay 공개 전에 필요한 Prefab 등록 여부를 확인한다.
    public bool IsPrefabRegistered(GameObject prefab)
    {
        return IsInitialized && prefab != null && _runtimePools.ContainsKey(prefab.GetInstanceID());
    }

    // 게임 시작 시 필요한 모든 자원을 메모리에 사전 할당하여 플레이 중 지연 현상 방지.
    private void InitializePools()
    {
        for (int i = 0; i < _poolDefinitions.Length; i++)
        {
            CreatePool(_poolDefinitions[i]);
        }
    }

    // 프리팹별 독립적인 관리 공간(Anchor)과 ObjectPool<Component>를 생성하고 initialSize만큼 사전 생성한다.
    // 설계 결정: 하이러키 창의 가독성을 위해 각 풀마다 전용 부모 오브젝트 구성.
    private void CreatePool(PoolDefinition poolDefinition)
    {
        Component prefabComponent = poolDefinition.PrefabComponent;

        if (prefabComponent == null)
        {
            Debug.LogError("PoolManager: _poolDefinitions에 null Prefab 항목이 있습니다.", this);
            return;
        }

        GameObject prefab = prefabComponent.gameObject;
        int poolKey = prefab.GetInstanceID();

        // 중복 Prefab은 Anchor를 만들기 전에 거부한다.
        if (_runtimePools.ContainsKey(poolKey))
        {
            Debug.LogError($"PoolManager: Prefab '{prefab.name}'이(가) _poolDefinitions에 중복 등록되어 있습니다.", this);
            return;
        }

        int initialSize = poolDefinition.InitialSize;

        if (initialSize <= 0)
        {
            Debug.LogError($"PoolManager: Prefab '{prefab.name}'의 _initialSize가 0 이하입니다.", this);
            return;
        }

        Transform containerTransform = CreatePoolContainer(prefab.name);

        RuntimePool runtimePool = new RuntimePool();

        runtimePool.Pool = new ObjectPool<Component>(
            createFunc: () => CreatePooledComponent(prefabComponent, containerTransform, runtimePool),
            actionOnGet: null,
            actionOnRelease: component => component.gameObject.SetActive(false),
            actionOnDestroy: component =>
            {
                _componentOwners.Remove(component);

                if (component != null)
                {
                    Destroy(component.gameObject);
                }
            },
            collectionCheck: true,
            defaultCapacity: initialSize,
            maxSize: initialSize);

        _runtimePools.Add(poolKey, runtimePool);

        PrewarmPool(runtimePool, initialSize);
    }

    private Transform CreatePoolContainer(string prefabName)
    {
        GameObject containerGameObject = new GameObject(prefabName + "Anchor");

        containerGameObject.transform.SetParent(_poolRoot);

        return containerGameObject.transform;
    }

    // 활성 Prefab을 Instantiate할 때 초기화 전 OnEnable이 실행되지 않도록 생성 중에만 사용할 비활성 부모다.
    private Transform CreateInactiveCreationRoot()
    {
        GameObject creationRootGameObject = new GameObject("InactiveCreationRoot");
        creationRootGameObject.transform.SetParent(_poolRoot);
        creationRootGameObject.SetActive(false);

        return creationRootGameObject.transform;
    }

    // initialSize만큼 실제로 Instantiate가 일어나도록 먼저 모두 대여한 뒤 반환하여 비활성 상태로 사전 확보한다.
    private void PrewarmPool(RuntimePool runtimePool, int initialSize)
    {
        Component[] preWarmedComponents = new Component[initialSize];

        for (int i = 0; i < initialSize; i++)
        {
            preWarmedComponents[i] = RentFromPool(runtimePool);
        }

        for (int i = 0; i < initialSize; i++)
        {
            ReturnToPool(runtimePool, preWarmedComponents[i]);
        }
    }

    // ObjectPool의 createFunc. Inspector에서 지정한 Component를 그대로 Instantiate해 타입 조회 없이 확보한다.
    private Component CreatePooledComponent(Component prefabComponent, Transform containerTransform, RuntimePool runtimePool)
    {
        Component component = Instantiate(prefabComponent, _inactiveCreationRoot);
        MonoBehaviour[] behaviours = component.GetComponentsInChildren<MonoBehaviour>(true);
        bool[] enabledStates = new bool[behaviours.Length];

        for (int i = 0; i < behaviours.Length; i++)
        {
            enabledStates[i] = behaviours[i].enabled;
            behaviours[i].enabled = false;
        }

        component.transform.SetParent(containerTransform, false);

        // 비활성 Prefab도 Awake를 마친 상태로 Pool에 보관한다. MonoBehaviour가 비활성이라 OnEnable은 실행되지 않는다.
        component.gameObject.SetActive(true);
        component.gameObject.SetActive(false);

        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = enabledStates[i];
        }

        _componentOwners[component] = runtimePool;

        return component;
    }

    // Instantiate를 대체하여 기존 오브젝트를 재활용하기 위한 핵심 인터페이스.
    // 결과의 의도: 요청된 위치와 회전값으로 초기화된 즉시 사용 가능한 컴포넌트 반환.
    public Component RentComponent(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.Log("No object pool for null prefab");
            return null;
        }

        int poolKey = prefab.GetInstanceID();

        if (_runtimePools.TryGetValue(poolKey, out RuntimePool runtimePool))
        {
            Component componentToRent = RentFromPool(runtimePool);

            ApplySpawnTransform(position, rotation, componentToRent, prefab);

            return componentToRent;
        }

        Debug.Log("No object pool for " + prefab);
        return null;
    }

    /// <summary>
    /// 대여한 Component를 소유 풀로 반환한다. PoolManager가 대여해 준 적 없거나 이미 반환된 Component는
    /// false를 반환하고 반환 처리를 하지 않는다.
    /// </summary>
    public bool ReturnComponent(Component component)
    {
        if (component == null)
        {
            return false;
        }

        if (!_componentOwners.TryGetValue(component, out RuntimePool runtimePool))
        {
            Debug.LogWarning($"PoolManager: 소유하지 않은 Component '{component}'를 반환하려 했습니다.", component);
            return false;
        }

        if (!runtimePool.RentedComponents.Contains(component))
        {
            Debug.LogWarning($"PoolManager: 이미 반환되었거나 대여되지 않은 Component '{component}'를 반환하려 했습니다.", component);
            return false;
        }

        ReturnToPool(runtimePool, component);

        return true;
    }

    // 풀에서 Component를 대여하고 대여 중 목록에 추가한다. 풀이 비어 있으면 ObjectPool이 createFunc로 임시 생성한다.
    private Component RentFromPool(RuntimePool runtimePool)
    {
        Component component = runtimePool.Pool.Get();

        runtimePool.RentedComponents.Add(component);

        return component;
    }

    // 대여 중 목록에서 제거하고 풀에 반환한다. maxSize를 초과한 반환분은 ObjectPool이 actionOnDestroy로 제거한다.
    private void ReturnToPool(RuntimePool runtimePool, Component component)
    {
        runtimePool.RentedComponents.Remove(component);

        runtimePool.Pool.Release(component);
    }

    // 이전 사용 상태가 현재 배치에 영향을 주지 않도록 트랜스폼 값 초기화.
    private void ApplySpawnTransform(Vector3 position, Quaternion rotation, Component componentToRent, GameObject prefab)
    {
        componentToRent.transform.position = position;
        componentToRent.transform.rotation = rotation;
        componentToRent.gameObject.transform.localScale = prefab.transform.localScale;
    }

    protected override void OnDestroy()
    {
        IsInitialized = false;
        base.OnDestroy();

        foreach (RuntimePool runtimePool in _runtimePools.Values)
        {
            runtimePool.Pool.Dispose();
        }

        _runtimePools.Clear();
        _componentOwners.Clear();
    }
}
