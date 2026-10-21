using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Shake 이벤트 프로필의 우선순위와 축별 진폭 상한을 적용한 뒤 Cinemachine Impulse를 발행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraShakeDirector : MonoBehaviour
{
    [Tooltip("Shake 진폭과 지속시간을 가진 단일 CameraSettingsSO")]
    [SerializeField] private CameraSettingsSO _settings;
    [Tooltip("같은 GameObject의 CinemachineImpulseSource")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    private CameraShakeStrength _activeStrength;
    private float _activeShakeEndTime;
    private float _userIntensityScale = 1f;
    private bool _hasActiveShake;

    private void Awake()
    {
        if (_settings == null || _impulseSource == null)
        {
            Logger.LogError(
                $"{nameof(CameraShakeDirector)}: {nameof(_settings)} 또는 {nameof(_impulseSource)}가 연결되지 않아 컴포넌트를 비활성화합니다.",
                this);
            enabled = false;
        }
    }

    /// <summary>
    /// 지정 이벤트의 Shake를 요청한다. 약한 요청, 일시정지, 사용자 강도 0은 조용히 무시한다.
    /// </summary>
    public void RequestShake(CameraShakeEvent shakeEvent)
    {
        if (_settings == null || _impulseSource == null || Time.timeScale <= 0f)
        {
            return;
        }

        if (!_settings.TryGetShakeProfile(shakeEvent, out CameraShakeProfile profile))
        {
            return;
        }

        TryApplyShake(profile);
    }

    /// <summary>
    /// 호출자가 이미 들고 있는 Shake Profile(예: 총기별 WeaponDataSO.FireShakeProfile)을 그대로 요청한다.
    /// 우선순위·축 상한·사용자 강도 적용은 <see cref="RequestShake(CameraShakeEvent)"/>와 같은 경로를 공유한다
    /// (camera-system-design.md Decision 006).
    /// </summary>
    public void RequestShake(CameraShakeProfile profile)
    {
        if (_settings == null || _impulseSource == null || Time.timeScale <= 0f || profile == null)
        {
            return;
        }

        TryApplyShake(profile);
    }

    private void TryApplyShake(CameraShakeProfile profile)
    {
        ClearExpiredShakeState();

        if (!ShouldAcceptRequest(_hasActiveShake, _activeStrength, profile.Priority))
        {
            return;
        }

        Vector2 translationAmplitude = CalculateTranslationAmplitude(
            profile.TranslationAmplitude,
            _userIntensityScale,
            _settings.MaxShakeTranslationAmplitude);

        // 0 진폭 Impulse를 발행해 기존 연출을 대체하지 않는다.
        if (translationAmplitude.sqrMagnitude <= 0f)
        {
            return;
        }

        _impulseSource.ImpulseDefinition.ImpulseDuration = profile.Duration;
        _impulseSource.GenerateImpulseWithVelocity(CreateImpulseVelocity(translationAmplitude));

        _activeStrength = profile.Priority;
        _activeShakeEndTime = Time.time + profile.Duration;
        _hasActiveShake = true;
    }

    /// <summary>
    /// Settings 시스템이 제공하는 사용자 Shake 강도를 설정한다.
    /// </summary>
    public void SetUserIntensityScale(float scale01)
    {
        _userIntensityScale = Mathf.Clamp01(scale01);
    }

    /// <summary>
    /// 현재 카메라 Shake 상태를 정리한다. STEP-06의 공간 전환 직후 호출용이다.
    /// </summary>
    public void ClearActiveShake()
    {
        _hasActiveShake = false;
        _activeShakeEndTime = 0f;
        CinemachineImpulseManager.Instance.Clear();
    }

    /// <summary>
    /// 순수 우선순위 규칙이다. 테스트와 런타임이 같은 판정을 사용한다.
    /// </summary>
    public static bool ShouldAcceptRequest(
        bool hasActiveShake,
        CameraShakeStrength activeStrength,
        CameraShakeStrength requestedStrength)
    {
        return !hasActiveShake || requestedStrength >= activeStrength;
    }

    /// <summary>
    /// 사용자 강도와 X/Y 축별 상한을 적용한 실제 이동 진폭을 계산한다.
    /// </summary>
    public static Vector2 CalculateTranslationAmplitude(
        Vector2 translationAmplitude,
        float userIntensityScale,
        Vector2 maxTranslationAmplitude)
    {
        float intensityScale = Mathf.Clamp01(userIntensityScale);
        float maximumX = Mathf.Max(0f, maxTranslationAmplitude.x);
        float maximumY = Mathf.Max(0f, maxTranslationAmplitude.y);

        return new Vector2(
            Mathf.Min(Mathf.Max(0f, translationAmplitude.x) * intensityScale, maximumX),
            Mathf.Min(Mathf.Max(0f, translationAmplitude.y) * intensityScale, maximumY));
    }

    /// <summary>
    /// 기본 Shake의 Cinemachine Velocity를 만든다. Z 이동과 회전은 사용하지 않는다.
    /// </summary>
    public static Vector3 CreateImpulseVelocity(Vector2 translationAmplitude)
    {
        return new Vector3(translationAmplitude.x, translationAmplitude.y, 0f);
    }

    private void ClearExpiredShakeState()
    {
        if (_hasActiveShake && Time.time >= _activeShakeEndTime)
        {
            _hasActiveShake = false;
        }
    }
}
