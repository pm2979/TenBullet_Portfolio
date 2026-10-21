using UnityEngine;

/// <summary>경로 상태 없이 목표 방향을 즉시 반환하는 직선 Navigation 구현</summary>
// 장애물 우회가 필요 없는 테스트 또는 명시적 주입 환경에만 사용
public sealed class DirectEnemyNavigation : IEnemyNavigation
{
    public EnemyNavigationResult GetSteeringResult(Vector2 selfPosition, Vector2 targetPosition)
    {
        Vector2 direction = (targetPosition - selfPosition).normalized;
        return EnemyNavigationResult.Success(direction);
    }

    // 보관하는 경로 상태가 없어 취소 처리 생략
    public void CancelCurrentPath()
    {
    }

    // Graph가 없으므로 요청 위치를 그대로 이동 가능 지점으로 반환
    public bool TryGetNearestWalkablePosition(Vector2 desiredPosition, out Vector2 walkablePosition)
    {
        walkablePosition = desiredPosition;
        return true;
    }

    // 대여 수명 상태가 없어 초기화 처리 생략
    public void ResetRuntimeState()
    {
    }
}
