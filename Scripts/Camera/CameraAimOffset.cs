using UnityEngine;

/// <summary>
/// 화면상 조준 거리로부터 상한이 있는 월드 오프셋을 계산하고 평활화 상태를 유지한다.
/// 카메라·Transform을 참조하지 않는 순수 계산 객체다.
/// </summary>
public sealed class CameraAimOffset
{
    private const float MinimumScreenHeight = 0.0001f;

    private readonly float _maxOffsetScreenRatio;
    private readonly float _saturationScreenRatio;
    private readonly float _smoothTime;

    private Vector2 _currentOffset;
    private Vector2 _velocity;

    public CameraAimOffset(float maxOffsetScreenRatio, float saturationScreenRatio, float smoothTime)
    {
        _maxOffsetScreenRatio = Mathf.Max(0f, maxOffsetScreenRatio);
        _saturationScreenRatio = Mathf.Max(0f, saturationScreenRatio);
        _smoothTime = Mathf.Max(0f, smoothTime);
    }

    /// <summary>
    /// 화면상 커서 거리와 현재 화면의 월드 높이를 사용해 평활화된 조준 오프셋을 반환한다.
    /// </summary>
    public Vector2 Update(
        Vector2 playerScreenPosition,
        Vector2 aimScreenPosition,
        float screenHeight,
        float worldScreenHeight,
        Vector2 aimWorldDirection,
        float deltaTime)
    {
        if (screenHeight <= MinimumScreenHeight || worldScreenHeight <= 0f ||
            _saturationScreenRatio <= 0f || aimWorldDirection.sqrMagnitude <= 0f)
        {
            Reset();
            return Vector2.zero;
        }

        float screenDistanceRatio = Vector2.Distance(playerScreenPosition, aimScreenPosition) / screenHeight;

        if (screenDistanceRatio <= 0f)
        {
            Reset();
            return Vector2.zero;
        }

        float saturationRatio = Mathf.Clamp01(screenDistanceRatio / _saturationScreenRatio);
        float maxOffsetWorld = _maxOffsetScreenRatio * worldScreenHeight;
        Vector2 targetOffset = aimWorldDirection.normalized * (saturationRatio * maxOffsetWorld);

        _currentOffset = Vector2.SmoothDamp(_currentOffset, targetOffset, ref _velocity, _smoothTime, Mathf.Infinity, deltaTime);
        return _currentOffset;
    }

    /// <summary>
    /// 공간 전환이나 바인딩 해제 시 평활화 중인 상태를 제거한다.
    /// </summary>
    public void Reset()
    {
        _currentOffset = Vector2.zero;
        _velocity = Vector2.zero;
    }
}
