using System.Collections.Generic;
using Pathfinding;
using UnityEngine;

/// <summary>
/// 조립된 Room과 단일 GridGraph가 Session 공개 전에 요구하는 Scene 구성(Player 전용 진입 Layer,
/// Room Bounds의 Graph 범위 포함 여부)을 확인한다.
/// </summary>
public static class DungeonSceneValidationGate
{
    // 조립된 Room 전체와 단일 GridGraph를 대상으로 구조화된 오류 목록을 만든다.
    public static DungeonDataValidationResult Validate(
        IReadOnlyList<RoomRuntime> rooms,
        AstarPath astarPath,
        int playerLayer)
    {
        DungeonDataValidationResult result = new DungeonDataValidationResult();

        if (rooms == null || rooms.Count == 0)
        {
            result.AddError("검증할 조립된 Room이 없습니다.");
            return result;
        }

        if (!TryGetSingleGridGraph(astarPath, out GridGraph gridGraph))
        {
            result.AddError("Dungeon Navigation에는 단일 GridGraph가 필요합니다.");
            return result;
        }

        Bounds graphBounds = CalculateGraphBounds(gridGraph);
        int expectedLayerMask = 1 << playerLayer;

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomRuntime room = rooms[i];

            if (room == null || room.View == null)
            {
                continue;
            }

            ValidateEntrantLayer(room, expectedLayerMask, result);
            ValidateRoomBoundsWithinGraph(room, graphBounds, result);
        }

        return result;
    }

    // 진입 Trigger가 Player Layer 하나만 허용하는지 확인한다.
    private static void ValidateEntrantLayer(
        RoomRuntime room,
        int expectedLayerMask,
        DungeonDataValidationResult result)
    {
        if (room.View.EntrantLayers.value != expectedLayerMask)
        {
            result.AddError(
                $"Room '{room.RoomId}'의 진입 Trigger Layer가 Player 전용이 아닙니다.",
                room.View);
        }
    }

    // Room의 전체 물리 Collider Bounds가 GridGraph 범위 안에 있는지 확인한다.
    private static void ValidateRoomBoundsWithinGraph(
        RoomRuntime room,
        Bounds graphBounds,
        DungeonDataValidationResult result)
    {
        IReadOnlyList<Collider2D> roomColliders = room.View.RoomColliders;

        if (roomColliders == null || roomColliders.Count == 0)
        {
            return;
        }

        Bounds? roomBounds = null;

        for (int i = 0; i < roomColliders.Count; i++)
        {
            Collider2D roomCollider = roomColliders[i];

            if (roomCollider == null)
            {
                continue;
            }

            if (roomBounds == null)
            {
                roomBounds = roomCollider.bounds;
            }
            else
            {
                Bounds encapsulated = roomBounds.Value;
                encapsulated.Encapsulate(roomCollider.bounds);
                roomBounds = encapsulated;
            }
        }

        if (roomBounds == null)
        {
            return;
        }

        // 2D Room·Graph의 평면(X·Y)만 비교한다. Z는 Collider·Graph 변환에서 미세하게 어긋날 수 있어
        // 포함 판정에 사용하지 않는다.
        Bounds bounds = roomBounds.Value;
        bool withinX = bounds.min.x >= graphBounds.min.x && bounds.max.x <= graphBounds.max.x;
        bool withinY = bounds.min.y >= graphBounds.min.y && bounds.max.y <= graphBounds.max.y;

        if (!withinX || !withinY)
        {
            result.AddError(
                $"Room '{room.RoomId}'의 전체 Bounds가 GridGraph 범위를 벗어났습니다.",
                room.View);
        }
    }

    // 활성 AstarPath인지 먼저 확인한 뒤 Graph 구성 판정은 Navigation 쪽 단일 구현에 맡긴다.
    private static bool TryGetSingleGridGraph(AstarPath astarPath, out GridGraph gridGraph)
    {
        gridGraph = null;

        if (astarPath == null || astarPath.data == null || AstarPath.active != astarPath)
        {
            return false;
        }

        return DungeonNavigationController.TryGetSingleGridGraph(astarPath.data.graphs, out gridGraph);
    }

    // Grid 지역 좌표계를 Graph 변환으로 World Bounds로 변환한다(회전·2D 변환을 모두 반영).
    // GridGraph.transform 필드는 Scan 시점에만 갱신되므로, Scan 전에도 현재 설정값으로 직접
    // 계산하는 CalculateTransform()을 사용한다. 이 변환은 Node 개수 단위 좌표를 받아 내부적으로
    // nodeSize 배율을 적용하므로, World 단위로 미리 곱하지 않고 Node 개수 그대로 넘긴다.
    private static Bounds CalculateGraphBounds(GridGraph gridGraph)
    {
        Vector3 size = new Vector3(gridGraph.width, 0f, gridGraph.depth);
        Bounds localBounds = new Bounds(size * 0.5f, size);
        return gridGraph.CalculateTransform().Transform(localBounds);
    }
}
