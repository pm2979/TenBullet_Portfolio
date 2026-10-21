using UnityEngine;

/// <summary>
/// 카메라 추적·조준 오프셋·Shake 수치의 단일 설정 데이터다.
/// 모든 기본값은 플레이 테스트 전 임시값이다.
/// </summary>
[CreateAssetMenu(fileName = "CameraSettings", menuName = "Scriptable Objects/Camera/Camera Settings")]
public sealed class CameraSettingsSO : ScriptableObject
{
    [Header("조준 오프셋")]
    [Tooltip("화면 높이에 대한 최대 조준 오프셋 비율입니다. 플레이 테스트 전 임시값입니다.")]
    [SerializeField, Range(0f, 0.5f)] private float _maxAimOffsetScreenRatio = 0.2f;
    [Tooltip("화면 높이에 대한 오프셋 최대 도달 거리 비율입니다. 플레이 테스트 전 임시값입니다.")]
    [SerializeField, Range(0.01f, 0.5f)] private float _offsetSaturationScreenRatio = 0.4f;
    [Tooltip("조준 오프셋이 목표값으로 평활화되는 시간(초)입니다. 플레이 테스트 전 임시값입니다.")]
    [SerializeField, Range(0f, 1f)] private float _offsetSmoothTime = 0.1f;

    [Header("Shake 이벤트 프로필")]
    [Tooltip("일반 명중은 짧고 약한 X/Y Shake를 사용합니다. 플레이 테스트 전 임시값입니다.")]
    [SerializeField] private CameraShakeProfile _normalHitShakeProfile = new(
        new Vector2(0.16f, 0.12f), 0.1f, CameraShakeStrength.Light);
    [Tooltip("약점 명중의 X/Y Shake입니다. 정확한 강도는 플레이 테스트에서 조정합니다.")]
    [SerializeField] private CameraShakeProfile _weakPointHitShakeProfile = new(
        new Vector2(0.4f, 0.3f), 0.2f, CameraShakeStrength.Medium);
    [Tooltip("Player 피해는 일반 명중보다 강한 X/Y Shake를 사용합니다.")]
    [SerializeField] private CameraShakeProfile _playerDamagedShakeProfile = new(
        new Vector2(0.8f, 0.6f), 0.35f, CameraShakeStrength.Heavy);
    [Tooltip("폭발은 Player 피해와 같은 강한 X/Y Shake를 사용합니다. 요청 경로는 후속 구현입니다.")]
    [SerializeField] private CameraShakeProfile _explosionShakeProfile = new(
        new Vector2(0.9f, 0.8f), 0.35f, CameraShakeStrength.Heavy);
    [Tooltip("지면 충격 등 특수 연출만 Y축 전용 Shake를 사용합니다. 요청 경로는 후속 구현입니다.")]
    [SerializeField] private CameraShakeProfile _groundImpactShakeProfile = new(
        new Vector2(0f, 0.5f), 0.2f, CameraShakeStrength.Medium);

    [Space(10)]
    [Tooltip("동시 Shake에 적용할 X/Y 축별 최대 이동 진폭입니다. 플레이 테스트 전 임시값입니다.")]
    [SerializeField] private Vector2 _maxShakeTranslationAmplitude = new(0.8f, 0.6f);

    public float MaxAimOffsetScreenRatio => _maxAimOffsetScreenRatio;
    public float OffsetSaturationScreenRatio => _offsetSaturationScreenRatio;
    public float OffsetSmoothTime => _offsetSmoothTime;
    public Vector2 MaxShakeTranslationAmplitude => _maxShakeTranslationAmplitude;

    /// <summary>
    /// 지정 이벤트의 Shake 프로필을 가져온다. 미등록 이벤트나 누락된 프로필은 false를 반환한다.
    /// </summary>
    public bool TryGetShakeProfile(CameraShakeEvent shakeEvent, out CameraShakeProfile profile)
    {
        profile = shakeEvent switch
        {
            CameraShakeEvent.NormalHit => _normalHitShakeProfile,
            CameraShakeEvent.WeakPointHit => _weakPointHitShakeProfile,
            CameraShakeEvent.PlayerDamaged => _playerDamagedShakeProfile,
            CameraShakeEvent.Explosion => _explosionShakeProfile,
            CameraShakeEvent.GroundImpact => _groundImpactShakeProfile,
            _ => null
        };

        return profile != null;
    }
}
