using UnityEngine;

/// <summary>
/// 회피의 진행 상태와 목표 위치를 관리한다.
/// </summary>
public sealed class DodgeState
{
    public bool IsRolling { get; private set; }
    public Vector2 TargetPosition { get; private set; }

    public bool CanStart => !IsRolling;

    /// <summary>
    /// 유효한 방향과 거리로 회피 목표를 확정하며, 진행 중이거나 입력이 유효하지 않으면 거부한다.
    /// </summary>
    public bool TryStart(Vector2 startPosition, Vector2 direction, float distance)
    {
        if (!CanStart || direction.sqrMagnitude <= 0f || float.IsNaN(direction.x) || float.IsNaN(direction.y) ||
            float.IsInfinity(direction.x) || float.IsInfinity(direction.y) || distance <= 0f ||
            float.IsNaN(distance) || float.IsInfinity(distance))
        {
            return false;
        }

        IsRolling = true;
        TargetPosition = startPosition + direction.normalized * distance;
        return true;
    }

    /// <summary>
    /// 현재 위치에서 확정된 목표까지의 거리를 반환한다.
    /// </summary>
    public float GetRemainingDistance(Vector2 currentPosition)
    {
        return Vector2.Distance(currentPosition, TargetPosition);
    }

    /// <summary>
    /// 목표 도달로 진행 중인 회피를 종료한다.
    /// </summary>
    public void Stop()
    {
        if (!IsRolling) return;

        IsRolling = false;
    }

    /// <summary>
    /// 외부 사유로 진행 중인 회피를 즉시 중단한다.
    /// </summary>
    public void Cancel()
    {
        if (!IsRolling) return;

        IsRolling = false;
    }
}
