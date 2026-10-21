using UnityEngine;

/// <summary>현재 위치에서 목표 위치까지의 조향 결과와 경로 수명주기를 제공하는 Navigation 계약</summary>
// A* 내부 타입을 노출하지 않고 Spawn마다 명시적으로 주입되는 교체 경계
public interface IEnemyNavigation
{
    /// <summary>현재 위치와 목표 위치를 기준으로 적용 가능한 조향 상태 계산</summary>
    EnemyNavigationResult GetSteeringResult(Vector2 selfPosition, Vector2 targetPosition);

    /// <summary>대여 수명 자원은 유지하고 현재 경로 요청과 진행 상태만 취소</summary>
    void CancelCurrentPath();

    /// <summary>요청 위치와 가장 가까운 이동 가능 지점 탐색</summary>
    bool TryGetNearestWalkablePosition(Vector2 desiredPosition, out Vector2 walkablePosition);

    /// <summary>Pool 반환을 위해 경로 상태와 대여 수명 자원을 모두 초기화</summary>
    void ResetRuntimeState();
}
