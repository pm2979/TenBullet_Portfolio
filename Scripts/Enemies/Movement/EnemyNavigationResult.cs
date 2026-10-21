using UnityEngine;

/// <summary>Navigation 내부 구현을 숨기고 조향 상태와 적용 가능한 방향만 전달</summary>
// Success가 아닌 상태는 영벡터를 사용해 Movement가 Motor를 정지하도록 보장
public readonly struct EnemyNavigationResult
{
    public readonly EnemyNavigationStatus Status;
    public readonly Vector2 DesiredDirection;

    public static readonly EnemyNavigationResult PathPending = new EnemyNavigationResult(EnemyNavigationStatus.PathPending, Vector2.zero);
    public static readonly EnemyNavigationResult DestinationReached = new EnemyNavigationResult(EnemyNavigationStatus.DestinationReached, Vector2.zero);
    public static readonly EnemyNavigationResult NoPath = new EnemyNavigationResult(EnemyNavigationStatus.NoPath, Vector2.zero);
    public static readonly EnemyNavigationResult InvalidTarget = new EnemyNavigationResult(EnemyNavigationStatus.InvalidTarget, Vector2.zero);
    public static readonly EnemyNavigationResult NotReady = new EnemyNavigationResult(EnemyNavigationStatus.NotReady, Vector2.zero);

    private EnemyNavigationResult(EnemyNavigationStatus status, Vector2 desiredDirection)
    {
        Status = status;
        DesiredDirection = desiredDirection;
    }

    /// <summary>정규화된 조향 방향을 포함하는 성공 결과 생성</summary>
    public static EnemyNavigationResult Success(Vector2 desiredDirection)
    {
        return new EnemyNavigationResult(EnemyNavigationStatus.Success, desiredDirection);
    }
}
