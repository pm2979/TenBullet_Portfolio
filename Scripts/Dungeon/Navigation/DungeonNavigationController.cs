using System;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// 조립된 Dungeon의 단일 GridGraph Scan·Enemy Navigation 수명주기를 소유한다.
/// </summary>
public sealed class DungeonNavigationController : IDisposable
{
    // 경로 끝점을 Body 반경만큼 당길 때 더하는 여유. EnemyMovementController의 Cast Skin과 같은 값이라
    // 보정된 끝점이 Cast에 막히지 않는다(두 곳 모두 설계가 고정한 상수라 Inspector에 노출하지 않는다)
    private const float BodyClearanceSkin = 0.02f;

    private readonly AstarPath _astarPath;
    private readonly HashSet<AstarEnemyNavigation> _enemyNavigations =
        new HashSet<AstarEnemyNavigation>();
    private GridGraph _gridGraph;
    private bool _disposed;
    private readonly HashSet<string> _warnedOversizedBodies = new HashSet<string>();

    public DungeonNavigationContext Context { get; }

    // Scene 소유 AstarPath와 BuildPlan을 한 Dungeon Navigation 수명에 결합한다.
    public DungeonNavigationController(AstarPath astarPath, DungeonBuildPlan plan)
    {
        _astarPath = astarPath;

        if (plan == null || plan.Definition == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        Context = new DungeonNavigationContext(plan);
    }

    // 조립된 Room을 확인한 뒤 단일 GridGraph를 동기 Scan하고 요청을 허용한다. 실패하면 중단 사유를
    // failureReason으로 돌려주며, 호출자는 이를 그대로 Bootstrap 실패 메시지에 담는다.
    public bool TryInitialize(IReadOnlyList<RoomRuntime> rooms, out string failureReason)
    {
        if (_disposed || Context.IsReady)
        {
            failureReason = "이미 초기화됐거나 정리된 Dungeon Navigation입니다.";
            return false;
        }

        if (!HasValidRooms(rooms))
        {
            failureReason = "조립된 RoomRuntime과 출입 통제 참조가 필요합니다.";
            return false;
        }

        if (_astarPath == null || _astarPath.data == null || AstarPath.active != _astarPath)
        {
            failureReason = "활성 AstarPath를 찾을 수 없습니다.";
            return false;
        }

        if (!TryGetSingleGridGraph(_astarPath.data.graphs, out GridGraph gridGraph))
        {
            failureReason = "Dungeon Navigation에는 단일 GridGraph가 필요합니다.";
            return false;
        }

        try
        {
            Physics2D.SyncTransforms();
            _astarPath.Scan(gridGraph);
        }
        catch (Exception exception)
        {
            failureReason = $"GridGraph Scan에 실패했습니다: {exception.Message}";
            return false;
        }

        if (gridGraph.nodes == null || gridGraph.nodes.Length == 0)
        {
            failureReason = "GridGraph Scan 결과에 Node가 없습니다.";
            return false;
        }

        // 조립된 Dungeon에는 반드시 벽이 있다. Unwalkable이 하나도 없다면 벽 Collider가 Scan 질의에
        // 잡히지 않는다는 뜻이고(예: Composite Geometry Type이 면적 없는 Outlines), 그대로 두면 Enemy가
        // 벽을 무시하고 직선으로 움직인다. 조용히 잘못 동작하지 않도록 준비 단계에서 끊는다
        if (!HasAnyUnwalkableNode(gridGraph))
        {
            failureReason = "GridGraph Scan이 벽을 하나도 찾지 못했습니다. 벽 Collider가 물리 월드에 올라왔는지 확인하세요.";
            return false;
        }

        _gridGraph = gridGraph;
        Context.MarkReady();
        failureReason = string.Empty;
        return true;
    }

    // Enemy 한 개의 Seeker를 프로젝트 Navigation 결과 계약에 연결한다.
    public AstarEnemyNavigation CreateEnemyNavigation(
        Seeker seeker,
        float targetMoveThreshold,
        float waypointDistance,
        float destinationDistance)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DungeonNavigationController));
        }

        WarnIfBodyExceedsGraphClearance(seeker);

        AstarEnemyNavigation navigation = new AstarEnemyNavigation(
            seeker,
            targetMoveThreshold,
            waypointDistance,
            destinationDistance,
            ResolveBodyClearanceRadius(seeker),
            this);
        navigation.Bind(Context);
        _enemyNavigations.Add(navigation);
        return navigation;
    }

    // 경로 끝점 보정에 쓸 Body 여유 반경을 구한다. 반경을 판단할 수 없는 Collider 구성(Polygon 등)은
    // 0을 돌려줘 끝점을 보정하지 않고 기존 동작을 그대로 유지한다
    private static float ResolveBodyClearanceRadius(Seeker seeker)
    {
        if (seeker == null || !TryGetBodyRadius(seeker.gameObject, out float bodyRadius))
        {
            return 0f;
        }

        return bodyRadius + BodyClearanceSkin;
    }

    // GridGraph는 Scan 시점의 고정된 여유 반경 하나로 모든 Enemy의 경로를 만든다. Body Collider가
    // 그보다 크면 경로가 통과 가능하다고 보고한 곳에서 실제로는 벽에 낀다. 몬스터마다 Body 크기가
    // 달라지므로, 코드에 기준값을 복제하지 않고 Graph 설정에서 직접 읽어 Spawn 시 경고로 드러낸다.
    private void WarnIfBodyExceedsGraphClearance(Seeker seeker)
    {
        if (_gridGraph == null || seeker == null)
        {
            return;
        }

        if (!TryGetBodyRadius(seeker.gameObject, out float bodyRadius))
        {
            return;
        }

        float clearanceRadius = _gridGraph.collision.diameter * _gridGraph.nodeSize * 0.5f;

        if (bodyRadius <= clearanceRadius)
        {
            return;
        }

        string key = seeker.gameObject.name;

        if (!_warnedOversizedBodies.Add(key))
        {
            return;
        }

        Debug.LogWarning(
            $"[DungeonNavigation] {key}의 Body Collider 반경 {bodyRadius:F3}이 GridGraph가 보장하는 " +
            $"여유 반경 {clearanceRadius:F3}보다 큽니다. 좁은 통로·모서리에서 벽에 낄 수 있습니다. " +
            "Body를 줄이거나 Grid Graph의 Collision testing Diameter를 올리세요.",
            seeker);
    }

    // Root의 첫 non-trigger Collider2D에서 경로 여유와 비교할 반경을 구한다. Box·Capsule은 모서리가
    // 걸리므로 반대각선을 쓰고, 판단 기준이 없는 Polygon 등은 경고 대상에서 제외한다.
    private static bool TryGetBodyRadius(GameObject owner, out float bodyRadius)
    {
        bodyRadius = 0f;
        Collider2D[] colliders = owner.GetComponents<Collider2D>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];

            if (collider.isTrigger)
            {
                continue;
            }

            Vector3 scale = owner.transform.lossyScale;
            float uniformScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

            switch (collider)
            {
                case CircleCollider2D circle:
                    bodyRadius = circle.radius * uniformScale;
                    return true;

                case BoxCollider2D box:
                    bodyRadius = (box.size * 0.5f * uniformScale).magnitude + box.edgeRadius * uniformScale;
                    return true;

                case CapsuleCollider2D capsule:
                    bodyRadius = (capsule.size * 0.5f * uniformScale).magnitude;
                    return true;

                default:
                    return false;
            }
        }

        return false;
    }

    // Pool에서 다시 대여한 기존 어댑터를 현재 Dungeon Context에 재연결한다.
    public bool TryRebind(AstarEnemyNavigation navigation)
    {
        if (_disposed || navigation == null || !_enemyNavigations.Contains(navigation))
        {
            return false;
        }

        return navigation.Bind(Context);
    }

    // Pool 반환 경계에서 어댑터 자신이 호출해 등록 집합에서 제거한다. 반복 대여로 등록 수가 누적되지 않게 한다.
    public bool ReleaseEnemyNavigation(AstarEnemyNavigation navigation)
    {
        if (_disposed || navigation == null)
        {
            return false;
        }

        return _enemyNavigations.Remove(navigation);
    }

    // 새 요청을 막고 Enemy·Room 구독·대기 Graph 작업 순서로 정리한다.
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Context.MarkDisposed();

        // Dispose가 ResetRuntimeState를 거쳐 ReleaseEnemyNavigation으로 되돌아와 이 집합을 수정하므로
        // 먼저 스냅샷을 뜨고 비운 뒤 반복해 열거 중 수정 예외를 피한다.
        AstarEnemyNavigation[] navigations = new AstarEnemyNavigation[_enemyNavigations.Count];
        _enemyNavigations.CopyTo(navigations);
        _enemyNavigations.Clear();

        for (int i = 0; i < navigations.Length; i++)
        {
            navigations[i].Dispose();
        }

        if (_astarPath != null && AstarPath.active == _astarPath)
        {
            _astarPath.FlushGraphUpdates();
        }

        _gridGraph = null;
    }

    // Scan이 장애물을 하나라도 인식했는지 확인한다. 전부 통과 가능한 Graph는 조립 실패를 뜻한다
    private static bool HasAnyUnwalkableNode(GridGraph gridGraph)
    {
        bool hasUnwalkable = false;

        gridGraph.GetNodes(node =>
        {
            if (!node.Walkable)
            {
                hasUnwalkable = true;
            }
        });

        return hasUnwalkable;
    }

    // 조립 결과가 Navigation 구독에 필요한 참조를 모두 제공하는지 확인한다. Boss Arena는 물리 Door 계약
    // 밖의 별도 Stage이므로 AccessController를 요구하지 않는다(dungeon_system-design.md 4.2a).
    private static bool HasValidRooms(IReadOnlyList<RoomRuntime> rooms)
    {
        if (rooms == null || rooms.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomRuntime room = rooms[i];

            if (room == null || room.View == null)
            {
                return false;
            }

            if (room.Role != RoomRole.Boss && room.View.AccessController == null)
            {
                return false;
            }
        }

        return true;
    }

    // null 항목을 제외한 Graph 구성이 정확히 하나의 GridGraph인지 확인한다. Dungeon Navigation의
    // 단일 GridGraph 전제를 쓰는 곳이 모두 이 구현을 호출한다.
    internal static bool TryGetSingleGridGraph(NavGraph[] graphs, out GridGraph gridGraph)
    {
        gridGraph = null;
        int graphCount = 0;

        if (graphs != null)
        {
            for (int i = 0; i < graphs.Length; i++)
            {
                if (graphs[i] == null)
                {
                    continue;
                }

                graphCount++;
                gridGraph = graphs[i] as GridGraph;
            }
        }

        return graphCount == 1 && gridGraph != null;
    }

}
