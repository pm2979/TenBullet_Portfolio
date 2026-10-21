using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edgar가 이미 Instantiate한 Room GameObject를 DungeonBuildPlan과 결합해 RoomRuntime으로 Binding한다.
/// </summary>
/// <remarks>
/// Prefab을 새로 Instantiate하지 않는다 — Edgar 생성 결과가 이미 올바른 위치·회전의 실제 Room
/// Instance이므로, 여기서는 검증과 RoomRuntime 결합만 수행한다.
/// </remarks>
public sealed class DungeonRuntimeBinder
{
    // 계획의 모든 Room을 Edgar 생성 Instance와 결합하고 실패 시 생성 Root 전체를 롤백한다.
    public DungeonAssemblyResult Bind(
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, GameObject> roomInstancesByRoomId,
        GameObject generatedRoot,
        Transform parent)
    {
        if (plan == null || plan.Rooms == null || plan.Rooms.Count == 0)
        {
            return DungeonAssemblyResult.Failed(
                DungeonAssemblyFailure.InvalidPlan,
                "DungeonBuildPlan에 Binding할 Room이 없습니다.");
        }

        if (roomInstancesByRoomId == null || generatedRoot == null)
        {
            return DungeonAssemblyResult.Failed(
                DungeonAssemblyFailure.InvalidPlan,
                "Edgar 생성 Instance 또는 생성 Root가 없습니다.");
        }

        generatedRoot.transform.SetParent(parent, true);

        List<RoomRuntime> rooms = new List<RoomRuntime>(plan.Rooms.Count);
        Dictionary<string, RoomRuntime> roomsByRoomId = new Dictionary<string, RoomRuntime>(plan.Rooms.Count);

        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            DungeonRoomPlan roomPlan = plan.Rooms[i];

            if (!roomInstancesByRoomId.TryGetValue(roomPlan.RoomId, out GameObject instance) ||
                instance == null)
            {
                Rollback(rooms, generatedRoot);
                return DungeonAssemblyResult.Failed(
                    DungeonAssemblyFailure.MissingRoomInstance,
                    $"Room '{roomPlan.RoomId}'의 Edgar 생성 Instance를 찾지 못했습니다.");
            }

            RoomView view = instance.GetComponent<RoomView>();

            if (view == null)
            {
                Rollback(rooms, generatedRoot);
                return DungeonAssemblyResult.Failed(
                    DungeonAssemblyFailure.MissingRoomView,
                    $"Room '{roomPlan.RoomId}' Instance 루트에 RoomView가 없습니다.");
            }

            if (!view.TryInitialize(roomPlan.RoomId, roomPlan.Role, out string failureReason))
            {
                // Rollback이 generatedRoot 전체를 즉시 파괴하므로, 그 하위에 있는 instance의 이름은
                // Rollback 호출 전에 미리 읽어 둔다(그렇지 않으면 파괴된 Object 접근 예외가 발생한다).
                string archetypeId = roomPlan.Archetype != null ? roomPlan.Archetype.ContentId : "알 수 없음";
                string instanceName = instance.name;
                Rollback(rooms, generatedRoot);
                return DungeonAssemblyResult.Failed(
                    DungeonAssemblyFailure.InvalidRoomView,
                    $"Room '{roomPlan.RoomId}'(Role: {roomPlan.Role}, Archetype: {archetypeId}, " +
                    $"Prefab: {instanceName})의 RoomView 구성이 유효하지 않습니다 - {failureReason}");
            }

            RoomRuntime roomRuntime = new RoomRuntime(roomPlan, view);
            rooms.Add(roomRuntime);
            roomsByRoomId.Add(roomPlan.RoomId, roomRuntime);

            if (!instance.activeSelf)
            {
                instance.SetActive(true);
            }
        }

        if (!TryValidateConnectionDoors(plan, roomsByRoomId, out string connectionFailureReason))
        {
            Rollback(rooms, generatedRoot);
            return DungeonAssemblyResult.Failed(DungeonAssemblyFailure.ConnectionDoorMismatch, connectionFailureReason);
        }

        return DungeonAssemblyResult.Success(new DungeonAssembly(rooms, generatedRoot));
    }

    // Plan Connection의 ID가 실제 Room View/AccessController의 물리 Door와 정확히 대응하는지 확인한다.
    // 보정 없이 하나라도 어긋나면 전체 조립을 실패시킨다.
    private static bool TryValidateConnectionDoors(
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, RoomRuntime> roomsByRoomId,
        out string failureReason)
    {
        for (int i = 0; i < plan.Connections.Count; i++)
        {
            DungeonConnectionPlan connection = plan.Connections[i];

            if (!roomsByRoomId.TryGetValue(connection.ParentRoomId, out RoomRuntime parentRoom) ||
                !parentRoom.View.AccessController.HasDoor(connection.ParentConnectionId))
            {
                failureReason =
                    $"Room '{connection.ParentRoomId}'에 Connection '{connection.ParentConnectionId}'에 대응하는 물리 Door가 없습니다.";
                return false;
            }

            if (!roomsByRoomId.TryGetValue(connection.ChildRoomId, out RoomRuntime childRoom) ||
                !childRoom.View.AccessController.HasDoor(connection.ChildConnectionId))
            {
                failureReason =
                    $"Room '{connection.ChildRoomId}'에 Connection '{connection.ChildConnectionId}'에 대응하는 물리 Door가 없습니다.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    // 실패한 Binding에서 생성 Root 전체를 한 번에 파괴한다. RoomRuntime은 계획·View 참조만 들고 있어
    // 따로 정리할 상태가 없고, Root 파괴로 View GameObject까지 함께 사라진다.
    private static void Rollback(IReadOnlyList<RoomRuntime> rooms, GameObject generatedRoot)
    {
        DungeonAssembly.DestroyInstance(generatedRoot);
    }
}
