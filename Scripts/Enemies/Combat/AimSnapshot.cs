using UnityEngine;

/// <summary>Aim Lock 시점의 방향·위치·속도를 공격 종료까지 고정하는 조준 스냅샷</summary>
// Telegraph와 실제 출력을 동일한 조준값으로 일치시키기 위한 불변 데이터
public readonly struct AimSnapshot
{
    public readonly Vector2 Direction;
    public readonly Vector2 WorldPosition;
    public readonly Vector2 TargetVelocity;
    public readonly float CreatedAtTime;

    public AimSnapshot(Vector2 direction, Vector2 worldPosition, Vector2 targetVelocity, float createdAtTime)
    {
        Direction = direction;
        WorldPosition = worldPosition;
        TargetVelocity = targetVelocity;
        CreatedAtTime = createdAtTime;
    }
}
