using UnityEngine;

/// <summary>Sensor가 한 번의 갱신에서 확정한 Target 상태와 공간 정보를 전달</summary>
// Decision과 Attack이 동일한 거리·시야 결과를 공유하도록 최초 감지와 추적 조건을 분리
public readonly struct TargetSnapshot
{
    public readonly Transform Target;
    public readonly Vector2 Position;
    public readonly Vector2 Velocity;
    public readonly Vector2 Direction;
    public readonly float Distance;
    /// <summary>최초 탐지 거리 안에서 확보한 시야 여부</summary>
    public readonly bool HasLineOfSight;

    /// <summary>감지 후 공격·추적에 사용할 추적 시야 여부</summary>
    public readonly bool HasTrackingLineOfSight;

    public readonly bool IsValid;

    /// <summary>거리와 무관한 Target 참조·활성·생존 유효성</summary>
    public readonly bool IsAlive;

    public static readonly TargetSnapshot None = new TargetSnapshot(null, Vector2.zero, Vector2.zero, Vector2.zero, float.PositiveInfinity, false, false, isAlive: false);

    public TargetSnapshot(
        Transform target, Vector2 position, Vector2 velocity, Vector2 direction, float distance,
        bool hasLineOfSight, bool isValid, bool isAlive = true)
        : this(
            target, position, velocity, direction, distance,
            hasLineOfSight, hasLineOfSight, isValid, isAlive)
    {
    }

    public TargetSnapshot(
        Transform target, Vector2 position, Vector2 velocity, Vector2 direction, float distance,
        bool hasLineOfSight, bool hasTrackingLineOfSight, bool isValid, bool isAlive)
    {
        Target = target;
        Position = position;
        Velocity = velocity;
        Direction = direction;
        Distance = distance;
        HasLineOfSight = hasLineOfSight;
        HasTrackingLineOfSight = hasTrackingLineOfSight;
        IsValid = isValid;
        IsAlive = isAlive;
    }
}
