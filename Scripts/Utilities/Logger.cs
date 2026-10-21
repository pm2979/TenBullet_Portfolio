using System;
using System.Diagnostics;
using Object = UnityEngine.Object;

/// <summary>
/// 프로젝트 전역에서 사용되는 중앙 집중식 로그 관리 클래스
/// 다양한 로그 레벨(Warning, Exception, Assertion) 지원
/// Context 참조를 통한 디버깅 효율성 증대
/// </summary>
public static class Logger
{
    private const string ColorOrange = "#ffa500";
    private const string ColorRed = "#ff0000";
    private const string ColorGreen = "#00ff00";

    /// <summary>
    /// 일반 정보 로그
    /// 에디터 및 개발 빌드 전용
    /// </summary>
    /// <param name="message">출력 메시지</param>
    /// <param name="context">클릭 시 하이라이트 될 컨텍스트 오브젝트 (선택)</param>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message, Object context = null)
    {
        UnityEngine.Debug.Log(message, context);
    }

    /// <summary>
    /// 경고 로그
    /// 예상치 못한 동작이나, 게임 진행에 영향이 없는 경우 사용
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object message, Object context = null)
    {
        UnityEngine.Debug.LogWarning($"<color={ColorOrange}>[WARNING] {message}</color>", context);
    }

    /// <summary>
    /// 오류 로그
    /// 게임 진행이 불가능한 치명적 오류 발생 시 사용
    /// 외부 로그 수집(Crashlytics 등)을 위해 모든 빌드에서 활성화
    /// </summary>
    public static void LogError(object message, Object context = null)
    {
        UnityEngine.Debug.LogError($"<color={ColorRed}>[ERROR] {message}</color>", context);
    }

    /// <summary>
    /// 예외(Exception) 로그
    /// try-catch 구문에서 발생한 예외 객체 기록
    /// 콜스택 추적에 최적화
    /// </summary>
    /// <param name="exception">발생한 예외 객체</param>
    /// <param name="context">관련 컨텍스트 오브젝트</param>
    public static void LogException(Exception exception, Object context = null)
    {
        UnityEngine.Debug.LogException(exception, context);
    }

    /// <summary>
    /// 논리 오류(Assertion) 로그
    /// 코드의 논리적 가정이 거짓(false)일 때 발생
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogAssertion(object message, Object context = null)
    {
        UnityEngine.Debug.LogAssertion($"[ASSERT] {message}", context);
    }

    /// <summary>
    /// 에디터 전용 시각적 강조 로그
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public static void EditorLog(object message, Object context = null)
    {
        UnityEngine.Debug.Log($"<color={ColorGreen}>[EDITOR] {message}</color>", context);
    }
}
