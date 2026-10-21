using System;
using UnityEngine;

/// <summary>
/// 하나의 Shake 이벤트가 사용할 이동 진폭, 지속시간, 우선순위를 정의한다.
/// Z 이동과 회전 데이터는 기본 Shake 계약에 포함하지 않는다.
/// </summary>
[Serializable]
public sealed class CameraShakeProfile
{
    [Tooltip("X/Y 축 이동 Shake 진폭입니다. 플레이 테스트 전 임시값입니다.")]
    [SerializeField] private Vector2 _translationAmplitude;
    [Tooltip("Shake 지속 시간(초)입니다. 플레이 테스트 전 임시값입니다.")]
    [SerializeField, Min(0f)] private float _duration;
    [Tooltip("동시 Shake 요청의 우선순위입니다.")]
    [SerializeField] private CameraShakeStrength _priority;

    public Vector2 TranslationAmplitude => _translationAmplitude;
    public float Duration => _duration;
    public CameraShakeStrength Priority => _priority;

    public CameraShakeProfile(Vector2 translationAmplitude, float duration, CameraShakeStrength priority)
    {
        _translationAmplitude = new Vector2(
            Mathf.Max(0f, translationAmplitude.x),
            Mathf.Max(0f, translationAmplitude.y));
        _duration = Mathf.Max(0f, duration);
        _priority = priority;
    }
}
