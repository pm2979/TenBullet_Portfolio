using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// A* Seeker 결과를 Enemy가 소비하는 상태형 조향 결과로 변환한다.
/// </summary>
public sealed class AstarEnemyNavigation : IEnemyNavigation, IDisposable
{
    private readonly Seeker _seeker;
    private readonly float _targetMoveThresholdSquared;
    private readonly float _waypointDistanceSquared;
    private readonly float _destinationDistanceSquared;
    private readonly float _bodyClearanceRadius;
    private readonly List<Vector2> _waypoints = new List<Vector2>();
    private readonly DungeonNavigationController _owner;
    private DungeonNavigationContext _context;
    private Pathfinding.Path _activePath;
    private Vector2 _requestedTarget;
    private int _waypointIndex;
    private bool _hasRequestedPath;
    private bool _hasValidPath;
    private bool _lastPathFailed;
    private bool _disposed;

    // 재탐색·Waypoint·도착 거리 정책과 경로 끝점 보정에 쓸 Body 여유 반경을 호출자가 확정한 값으로 받고,
    // Pool 반환 시 자신을 해제할 Controller를 기억한다. bodyClearanceRadius가 0 이하면 끝점을 보정하지 않는다
    internal AstarEnemyNavigation(
        Seeker seeker,
        float targetMoveThreshold,
        float waypointDistance,
        float destinationDistance,
        float bodyClearanceRadius,
        DungeonNavigationController owner)
    {
        _seeker = seeker != null ? seeker : throw new ArgumentNullException(nameof(seeker));
        _owner = owner;

        if (targetMoveThreshold <= 0f || waypointDistance <= 0f || destinationDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetMoveThreshold),
                "Navigation 거리 설정은 모두 0보다 커야 합니다.");
        }

        _targetMoveThresholdSquared = targetMoveThreshold * targetMoveThreshold;
        _waypointDistanceSquared = waypointDistance * waypointDistance;
        _destinationDistanceSquared = destinationDistance * destinationDistance;
        _bodyClearanceRadius = Mathf.Max(0f, bodyClearanceRadius);
    }

    // 현재 위치에서 캐시된 Path의 다음 Waypoint 방향 또는 명시적 실패 상태를 반환한다.
    public EnemyNavigationResult GetSteeringResult(Vector2 selfPosition, Vector2 targetPosition)
    {
        if (_disposed || _context == null || !_context.IsReady || _context.IsDisposed || _seeker == null)
        {
            return EnemyNavigationResult.NotReady;
        }

        if (!IsFinite(selfPosition) || !IsFinite(targetPosition))
        {
            return EnemyNavigationResult.InvalidTarget;
        }

        if ((targetPosition - selfPosition).sqrMagnitude <= _destinationDistanceSquared)
        {
            CancelPathRequest();
            ClearPathState();
            return EnemyNavigationResult.DestinationReached;
        }

        if (!_hasRequestedPath)
        {
            return RequestPath(selfPosition, targetPosition);
        }

        if (_activePath != null)
        {
            // 새 경로를 계산하는 동안에도 기존에 쓰던 유효한 경로가 남아 있으면 그 조향을 계속 돌려준다.
            // 새 Callback이 도착하면 HandlePathCompleted가 Waypoint를 원자적으로 교체한다
            return SteerAlongCurrentWaypoints(selfPosition, allowDestinationReached: false);
        }

        if ((targetPosition - _requestedTarget).sqrMagnitude >= _targetMoveThresholdSquared)
        {
            return RequestPath(selfPosition, targetPosition);
        }

        if (_lastPathFailed)
        {
            return EnemyNavigationResult.NoPath;
        }

        return SteerAlongCurrentWaypoints(selfPosition, allowDestinationReached: true);
    }

    // 목표 전환용 경량 취소. 진행 중 요청과 경로 캐시만 비우고 Context·Controller 등록은 유지한다.
    // CancelPathRequest가 _activePath를 비우므로 이후 도착하는 이전 요청의 Callback은 무시된다.
    public void CancelCurrentPath()
    {
        CancelPathRequest();
        ClearPathState();
    }

    // Graph에서 요청 위치에 가장 가까운 Walkable 지점을 찾는다. A* 타입은 이 경계 밖으로 내보내지 않는다.
    public bool TryGetNearestWalkablePosition(Vector2 desiredPosition, out Vector2 walkablePosition)
    {
        walkablePosition = desiredPosition;

        if (_disposed || _context == null || !_context.IsReady || _context.IsDisposed || AstarPath.active == null)
        {
            return false;
        }

        if (!IsFinite(desiredPosition))
        {
            return false;
        }

        NNInfo nearest = AstarPath.active.GetNearest(desiredPosition, NNConstraint.Default);

        if (nearest.node == null || !nearest.node.Walkable)
        {
            return false;
        }

        walkablePosition = new Vector2(nearest.position.x, nearest.position.y);
        return true;
    }

    // Pool 반환 경계에서 요청·경로·Dungeon Context를 모두 제거하고 Controller 등록 집합에서도 자신을 제거한다.
    public void ResetRuntimeState()
    {
        CancelCurrentPath();
        _context = null;
        _owner?.ReleaseEnemyNavigation(this);
    }

    // Dungeon 정리 뒤 재사용할 수 없도록 어댑터를 종료한다.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ResetRuntimeState();
        _disposed = true;
    }

    // Controller가 소유권을 확인한 Context만 활성 어댑터에 연결한다.
    internal bool Bind(DungeonNavigationContext context)
    {
        if (_disposed || context == null || context.IsDisposed)
        {
            return false;
        }

        CancelPathRequest();
        ClearPathState();
        _context = context;
        return true;
    }

    // 새 ABPath 요청을 시작한다. 기존에 쓰던 유효한 경로(_waypoints·_hasValidPath)는 건드리지 않고
    // 새 Callback이 도착할 때까지 계속 조향에 쓰이도록 남겨둔다. 처음 요청하는 경우에는 이 값들이
    // 이미 초기 상태이므로 그대로 둬도 동일하게 동작한다.
    private EnemyNavigationResult RequestPath(Vector2 selfPosition, Vector2 targetPosition)
    {
        CancelPathRequest();
        _hasRequestedPath = true;
        _lastPathFailed = false;
        _requestedTarget = targetPosition;

        ABPath path = ABPath.Construct(selfPosition, targetPosition, null);
        _activePath = path;

        try
        {
            _seeker.StartPath(path, HandlePathCompleted);
        }
        catch
        {
            _activePath = null;
            _lastPathFailed = true;
            return EnemyNavigationResult.NoPath;
        }

        // 요청을 건 바로 이번 프레임에도 기존 유효 경로가 남아 있으면 그 조향을 바로 돌려줘
        // 재탐색을 시작한 프레임 한 번만큼도 멈추지 않게 한다. 최초 요청은 돌려줄 경로가 없어 PathPending이다
        return SteerAlongCurrentWaypoints(selfPosition, allowDestinationReached: false);
    }

    // 현재 요청과 Context가 여전히 유효할 때만 A* 결과를 자체 Waypoint로 복사한다.
    private void HandlePathCompleted(Pathfinding.Path completedPath)
    {
        if (_disposed || _context == null || !_context.IsReady || _context.IsDisposed ||
            !ReferenceEquals(completedPath, _activePath))
        {
            return;
        }

        _activePath = null;
        _waypoints.Clear();
        _waypointIndex = 0;

        if (completedPath.error || completedPath.vectorPath == null || completedPath.vectorPath.Count == 0)
        {
            _hasValidPath = false;
            _lastPathFailed = true;
            return;
        }

        for (int i = 0; i < completedPath.vectorPath.Count; i++)
        {
            Vector3 waypoint = completedPath.vectorPath[i];
            _waypoints.Add(new Vector2(waypoint.x, waypoint.y));
        }

        ClampFinalWaypointToBodyClearance(completedPath);

        _hasValidPath = true;
        _lastPathFailed = false;
    }

    // A*는 경로 끝점을 끝 Node 사각형의 표면까지만 보정한다(ABPath.endPoint = endNode.ClosestPointOnNode).
    // 그 좌표는 벽면과 그대로 맞닿을 수 있어, 반경을 가진 Body는 끝점에 도달하지 못하고 벽을 밀며 갈린다.
    // 끝점을 끝 Node 중심 기준 ±(nodeSize/2 - 여유 반경) 안으로 당겨 언제나 도달 가능한 지점으로 만든다.
    // 중간 Waypoint는 Node 중심이라 이미 여유가 있으므로 건드리지 않는다
    private void ClampFinalWaypointToBodyClearance(Pathfinding.Path completedPath)
    {
        if (_bodyClearanceRadius <= 0f || _waypoints.Count == 0)
        {
            return;
        }

        if (!(completedPath is ABPath abPath) || abPath.path == null || abPath.path.Count == 0)
        {
            return;
        }

        GraphNode endNode = abPath.path[abPath.path.Count - 1];

        if (endNode == null || !(endNode.Graph is GridGraph gridGraph))
        {
            return;
        }

        float halfExtent = Mathf.Max(0f, gridGraph.nodeSize * 0.5f - _bodyClearanceRadius);
        Vector3 nodeCenter = (Vector3)endNode.position;
        int lastIndex = _waypoints.Count - 1;
        Vector2 endWaypoint = _waypoints[lastIndex];

        _waypoints[lastIndex] = new Vector2(
            Mathf.Clamp(endWaypoint.x, nodeCenter.x - halfExtent, nodeCenter.x + halfExtent),
            Mathf.Clamp(endWaypoint.y, nodeCenter.y - halfExtent, nodeCenter.y + halfExtent));
    }

    // 캐시된 현재 경로(_waypoints)를 기준으로 조향한다. 재탐색 중(allowDestinationReached=false)에는
    // 경로를 다 소진해도 실제 목표에 도달한 것이 아니므로 DestinationReached 대신 PathPending으로 멈춘다.
    private EnemyNavigationResult SteerAlongCurrentWaypoints(Vector2 selfPosition, bool allowDestinationReached)
    {
        if (!_hasValidPath || _waypoints.Count == 0)
        {
            return EnemyNavigationResult.PathPending;
        }

        AdvanceWaypoint(selfPosition);

        if (_waypointIndex >= _waypoints.Count)
        {
            return allowDestinationReached ? EnemyNavigationResult.DestinationReached : EnemyNavigationResult.PathPending;
        }

        Vector2 direction = _waypoints[_waypointIndex] - selfPosition;

        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return allowDestinationReached ? EnemyNavigationResult.DestinationReached : EnemyNavigationResult.PathPending;
        }

        return EnemyNavigationResult.Success(direction.normalized);
    }

    // 현재 위치와 겹쳤거나 진행 방향상 이미 지나친 Waypoint를 건너뛰어 실제 진행 방향을 선택한다.
    private void AdvanceWaypoint(Vector2 selfPosition)
    {
        while (_waypointIndex < _waypoints.Count && ShouldConsumeWaypoint(selfPosition))
        {
            _waypointIndex++;
        }
    }

    // 경로 계산은 비동기라 Callback이 도착한 시점에는 경로 시작점(vectorPath[0] = 요청 당시 자기 위치)이
    // 이미 뒤에 남아 있을 수 있다. 거리 조건만으로는 그 Waypoint가 소비되지 않아 한두 스텝 동안 뒤로
    // 조향하고 바라보기까지 반전되므로, 구간 진행 방향 기준으로 이미 지나친 Waypoint도 함께 소비한다.
    // 마지막 Waypoint는 진행 방향을 정의할 다음 구간이 없고 도착·끝점 보정 계약이 걸려 있어 거리로만 판정한다
    private bool ShouldConsumeWaypoint(Vector2 selfPosition)
    {
        Vector2 toWaypoint = _waypoints[_waypointIndex] - selfPosition;

        if (toWaypoint.sqrMagnitude <= _waypointDistanceSquared)
        {
            return true;
        }

        if (_waypointIndex + 1 >= _waypoints.Count)
        {
            return false;
        }

        Vector2 segment = _waypoints[_waypointIndex + 1] - _waypoints[_waypointIndex];

        return segment.sqrMagnitude > Mathf.Epsilon && Vector2.Dot(toWaypoint, segment) < 0f;
    }

    // 진행 중 요청을 취소해 이전 대여의 Callback이 새 상태를 덮어쓰지 못하게 한다.
    private void CancelPathRequest()
    {
        if (_activePath != null && _seeker != null)
        {
            _seeker.CancelCurrentPathRequest();
            _activePath = null;
        }
    }

    // Target과 Waypoint 캐시를 초기 상태로 되돌린다.
    private void ClearPathState()
    {
        _waypoints.Clear();
        _requestedTarget = Vector2.zero;
        _waypointIndex = 0;
        _hasRequestedPath = false;
        _hasValidPath = false;
        _lastPathFailed = false;
    }

    // A*에 전달할 좌표가 유한한 값인지 확인한다.
    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
