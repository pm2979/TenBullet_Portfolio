using UnityEngine;

/// <summary>
/// MonoBehaviour 기반 싱글톤 추상 클래스
/// 프로젝트 전반에서 전역 접근이 필요한 매니저 클래스의 기반 구조 제공
/// </summary>
public class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _instance;
    private static bool _applicationIsQuitting = false;

    /// <summary>
    /// 인스턴스 존재 여부 확인용 프로퍼티
    /// </summary>
    public static bool HasInstance => _instance != null && !_applicationIsQuitting;

    /// <summary>
    /// 생성 시 게임 오브젝트 이름 변경 여부 제어
    /// </summary>
    protected virtual bool ShouldRename => false;

    /// <summary>
    /// 씬 전환 시 파괴 방지 여부 제어
    /// </summary>
    protected virtual bool IsDontDestroyOnLoad => true;

    /// <summary>
    /// 싱글톤 인스턴스 접근자
    /// 지연 로딩(Lazy Loading) 방식의 인스턴스 생성 및 반환
    /// </summary>
    public static T Instance
    {
        get
        {
            // 앱 종료 중 인스턴스 재생성 방지 목적
            if (_applicationIsQuitting)
            {
                return null;
            }

            if (_instance == null)
            {
                _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);

                if (_instance == null)
                {
                    GameObject go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 중복 인스턴스 방지 및 초기 설정 수행
    /// </summary>
    protected virtual void Awake()
    {
        // Domain Reload를 사용하지 않아도 이전 Play Mode의 종료 상태가 남지 않도록 초기화한다.
        _applicationIsQuitting = false;

        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = (T)this;

        if (ShouldRename)
        {
            name = typeof(T).Name;
        }

        if (IsDontDestroyOnLoad)
        {
            // 자식 객체일 경우 DontDestroyOnLoad 호출 시 발생하는 유니티 내부 에러 방지
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Logger.LogWarning($"[MonoSingleton] {name}이(가) 자식 객체로 존재. DontDestroyOnLoad 적용 실패.");
            }
        }
    }

    /// <summary>
    /// 씬 전환 시 인스턴스 유지 여부에 따른 처리
    /// </summary>
    protected void DontDestroyOnLoadIfNeeded()
    {
        if (IsDontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// 인스턴스 해제 및 참조 정리
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 종료 상태 플래그 설정
    /// </summary>
    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

}
