using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 사운드의 Clip 후보와 재생 정책을 정의하는 읽기 전용 콘텐츠 자산이다.
/// </summary>
[CreateAssetMenu(fileName = "SoundData_", menuName = "Scriptable Objects/Audio/SoundData")]
public sealed class SoundDataSO : ScriptableObject
{
    private const float MinimumPitch = 0.01f;
    private const float MinimumDistanceGap = 0.01f;

    [Header("분류와 Clip")]
    [SerializeField] private SoundCategory _category = SoundCategory.Sfx;
    [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();

    [Space]
    [Header("기본 재생 설정")]
    [SerializeField, Range(0f, 1f)] private float _baseVolume = 1f;
    [Tooltip("SFX/UI에서 무작위로 선택할 Pitch 최솟값. BGM은 항상 1을 사용한다")]
    [SerializeField, Min(MinimumPitch)] private float _minPitch = 0.95f;
    [Tooltip("SFX/UI에서 무작위로 선택할 Pitch 최댓값. BGM은 항상 1을 사용한다")]
    [SerializeField, Min(MinimumPitch)] private float _maxPitch = 1.05f;
    [Tooltip("중요 경고 SFX가 예약 Voice를 사용할 수 있게 한다. UI는 설정과 무관하게 보호된다")]
    [SerializeField] private bool _isCritical;
    [Tooltip("0이면 이 사운드의 개별 동시 재생 수를 제한하지 않는다")]
    [SerializeField, Min(0)] private int _maxConcurrent;

    [Space]
    [Header("적·폭발 SFX 거리 감쇠")]
    [SerializeField] private bool _useDistanceAttenuation;
    [SerializeField, Min(0f)] private float _minDistance = 3f;
    [SerializeField, Min(MinimumDistanceGap)] private float _maxDistance = 18f;

    public SoundCategory Category => _category;
    public IReadOnlyList<AudioClip> Clips => _clips;
    public float BaseVolume => _baseVolume;
    public Vector2 PitchRange => new Vector2(_minPitch, _maxPitch);
    public bool IsCritical => _isCritical;
    public int MaxConcurrent => _maxConcurrent;
    public bool UseDistanceAttenuation => _useDistanceAttenuation;
    public float MinDistance => _minDistance;
    public float MaxDistance => _maxDistance;

    // Inspector 입력을 계약 범위로 보정하고 자동 복구할 수 없는 Clip 구성을 경고한다.
    private void OnValidate()
    {
        _clips ??= Array.Empty<AudioClip>();
        _baseVolume = Mathf.Clamp01(_baseVolume);
        _maxConcurrent = Mathf.Max(0, _maxConcurrent);
        _minDistance = Mathf.Max(0f, _minDistance);

        if (_maxDistance <= _minDistance)
        {
            _maxDistance = _minDistance + MinimumDistanceGap;
        }

        if (_category == SoundCategory.Bgm)
        {
            _minPitch = 1f;
            _maxPitch = 1f;
            _isCritical = false;
            _useDistanceAttenuation = false;
        }
        else
        {
            _minPitch = Mathf.Max(MinimumPitch, _minPitch);
            _maxPitch = Mathf.Max(_minPitch, _maxPitch);

            if (_category != SoundCategory.Sfx)
            {
                _useDistanceAttenuation = false;
            }
        }

        string validationWarning = GetValidationWarning();
        if (!string.IsNullOrEmpty(validationWarning))
        {
            Logger.LogWarning($"SoundDataSO '{name}': {validationWarning}", this);
        }
    }

    // 런타임 요청이 자산 하나만 안전하게 거절할 수 있도록 현재 데이터 오류를 반환한다.
    internal bool TryValidate(out string validationWarning)
    {
        validationWarning = GetValidationWarning();
        return string.IsNullOrEmpty(validationWarning);
    }

    private string GetValidationWarning()
    {
        if (_clips == null || _clips.Length == 0)
        {
            return "유효한 AudioClip을 1개 이상 지정해야 합니다.";
        }

        if (_category == SoundCategory.Bgm && _clips.Length != 1)
        {
            return "BGM은 AudioClip을 정확히 1개 지정해야 합니다.";
        }

        for (int i = 0; i < _clips.Length; i++)
        {
            if (_clips[i] == null)
            {
                return $"Clips의 {i}번 항목이 비어 있습니다.";
            }
        }

        if (_baseVolume < 0f || _baseVolume > 1f)
        {
            return "BaseVolume은 0 이상 1 이하여야 합니다.";
        }

        if (_category != SoundCategory.Bgm &&
            (_minPitch <= 0f || _maxPitch < _minPitch))
        {
            return "Pitch 범위는 양수이며 최솟값이 최댓값 이하여야 합니다.";
        }

        if (_minDistance < 0f || _maxDistance <= _minDistance)
        {
            return "거리 감쇠 범위는 0 ≤ MinDistance < MaxDistance여야 합니다.";
        }

        if (_maxConcurrent < 0)
        {
            return "MaxConcurrent는 0 이상이어야 합니다.";
        }

        return null;
    }
}
