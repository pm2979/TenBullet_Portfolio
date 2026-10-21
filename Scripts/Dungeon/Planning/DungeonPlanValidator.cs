using System;
using System.Collections.Generic;

/// <summary>
/// DungeonBuildPlan의 경로·분기·중복·비율·점유·출입구 계약을 검증한다.
/// </summary>
public sealed class DungeonPlanValidator
{
    // 조립 전에 계획의 모든 생성 검증 항목을 판정한다.
    public DungeonPlanValidationResult Validate(DungeonBuildPlan plan)
    {
        DungeonPlanValidationResult result = new DungeonPlanValidationResult();

        if (plan == null || plan.Definition == null)
        {
            result.AddError("Dungeon 계획 또는 정의가 null입니다.");
            return result;
        }

        Dictionary<string, DungeonRoomPlan> rooms = BuildRoomMap(plan, result);

        if (rooms.Count != plan.Rooms.Count)
        {
            return result;
        }

        ValidateRolesAndCounts(plan, result);
        ValidateConnectionsAndReachability(plan, rooms, result);
        ValidateRoomUsage(plan, rooms, result);
        ValidatePlacement(plan, rooms, result);
        ValidatePortalTarget(plan, rooms, result);
        return result;
    }

    // Room ID와 필수 참조의 유효성을 검사하며 조회 표를 만든다.
    private static Dictionary<string, DungeonRoomPlan> BuildRoomMap(
        DungeonBuildPlan plan,
        DungeonPlanValidationResult result)
    {
        Dictionary<string, DungeonRoomPlan> rooms =
            new Dictionary<string, DungeonRoomPlan>(StringComparer.Ordinal);

        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            DungeonRoomPlan room = plan.Rooms[i];

            if (room == null || room.Archetype == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                result.AddError($"Rooms[{i}]의 필수 값이 비어 있습니다.");
                continue;
            }

            if (!rooms.TryAdd(room.RoomId, room))
            {
                result.AddError($"Room ID '{room.RoomId}'가 중복되었습니다.");
            }
        }

        return rooms;
    }

    // 금지된 레거시 역할(Rest/선택 Room)과 Boss(물리 Graph 밖의 별도 Portal Stage)가 물리 생성 결과에
    // 남아 있는지 검사한다(dungeon_system-design.md 4.1, 4.2a).
    private static readonly RoomRole[] ForbiddenPhysicalRoles =
    {
        RoomRole.Boss,
        RoomRole.Rest,
        RoomRole.Treasure,
        RoomRole.Shop,
        RoomRole.Cursed,
        RoomRole.Secret,
    };

    // 입구·전투 Room 수가 정의에서 파생된 값과 맞는지, 금지된 역할이 없는지 검사한다.
    private static void ValidateRolesAndCounts(
        DungeonBuildPlan plan,
        DungeonPlanValidationResult result)
    {
        int entranceCount = 0;
        int combatCount = 0;
        int forbiddenCount = 0;

        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            DungeonRoomPlan room = plan.Rooms[i];

            if (room.Role == RoomRole.Entrance)
            {
                entranceCount++;
            }
            else if (room.Role == RoomRole.Combat)
            {
                combatCount++;
            }

            for (int roleIndex = 0; roleIndex < ForbiddenPhysicalRoles.Length; roleIndex++)
            {
                if (room.Role == ForbiddenPhysicalRoles[roleIndex])
                {
                    forbiddenCount++;
                    break;
                }
            }
        }

        if (plan.GenerationVersion != plan.Definition.GenerationVersion)
        {
            result.AddError("계획 생성 버전이 Dungeon 정의와 다릅니다.");
        }

        if (plan.ContentRoomCount < plan.Definition.MinimumContentRoomCount ||
            plan.ContentRoomCount > plan.Definition.MaximumContentRoomCount)
        {
            result.AddError("Combat Room 수가 Dungeon 정의 범위를 벗어났습니다.");
        }

        if (entranceCount != 1)
        {
            result.AddError("Entrance Room은 정확히 1개여야 합니다.");
        }

        if (forbiddenCount > 0)
        {
            result.AddError($"물리 생성 결과에 허용되지 않는 역할 Room이 {forbiddenCount}개 있습니다.");
        }

        if (combatCount != plan.ContentRoomCount || combatCount != plan.RequiredCombatRoomCount)
        {
            result.AddError("Combat Room 수가 계획의 집계값과 맞지 않습니다.");
        }
    }

    // 부모 연결 수·입구 도달성·Node 차수(Combat 1~3, Corridor 2·non-terminal)와 인접 Role(Combat의
    // 부모는 Corridor, Entrance/Corridor의 자식은 각각 Corridor/Combat)을 검사한다.
    private static void ValidateConnectionsAndReachability(
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, DungeonRoomPlan> rooms,
        DungeonPlanValidationResult result)
    {
        Dictionary<string, List<string>> children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        Dictionary<string, int> parentCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string roomId in rooms.Keys)
        {
            children.Add(roomId, new List<string>());
            parentCounts.Add(roomId, 0);
        }

        for (int i = 0; i < plan.Connections.Count; i++)
        {
            DungeonConnectionPlan connection = plan.Connections[i];

            if (connection == null ||
                !rooms.ContainsKey(connection.ParentRoomId) ||
                !rooms.ContainsKey(connection.ChildRoomId))
            {
                result.AddError($"Connections[{i}]가 존재하지 않는 Room을 참조합니다.");
                continue;
            }

            children[connection.ParentRoomId].Add(connection.ChildRoomId);
            parentCounts[connection.ChildRoomId]++;

            if (!string.Equals(
                rooms[connection.ChildRoomId].ParentRoomId,
                connection.ParentRoomId,
                StringComparison.Ordinal))
            {
                result.AddError($"Room '{connection.ChildRoomId}'의 부모 계약이 연결과 다릅니다.");
            }
        }

        DungeonRoomPlan entrance = FindRole(plan.Rooms, RoomRole.Entrance);

        if (entrance == null)
        {
            return;
        }

        if (parentCounts[entrance.RoomId] != 0)
        {
            result.AddError("입구 Room은 부모 연결을 가질 수 없습니다.");
        }

        foreach (KeyValuePair<string, int> entry in parentCounts)
        {
            if (entry.Key != entrance.RoomId && entry.Value != 1)
            {
                result.AddError($"Room '{entry.Key}'는 부모 연결을 정확히 하나 가져야 합니다.");
            }
        }

        HashSet<string> reachable = CollectReachable(entrance.RoomId, children);

        if (reachable.Count != rooms.Count)
        {
            result.AddError("입구에서 도달할 수 없는 Room이 있습니다(고립 Node 또는 순환 연결).");
        }

        ValidateNodeDegrees(plan.Rooms, children, result);
        ValidateAdjacentRoles(plan.Rooms, rooms, children, result);
    }

    // Role별 차수 불변식(Combat 1~3, Corridor 정확히 2·non-terminal, Entrance 자식 1개 이상)을 검사한다.
    // Boss는 물리 Graph의 Node가 아니므로(4.2a) 이 차수 검사 대상이 아니다.
    private static void ValidateNodeDegrees(
        IReadOnlyList<DungeonRoomPlan> rooms,
        IReadOnlyDictionary<string, List<string>> children,
        DungeonPlanValidationResult result)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            DungeonRoomPlan room = rooms[i];

            if (!children.TryGetValue(room.RoomId, out List<string> childIds))
            {
                continue;
            }

            int childCount = childIds.Count;

            switch (room.Role)
            {
                case RoomRole.Entrance:
                    if (childCount < 1)
                    {
                        result.AddError($"Entrance Room '{room.RoomId}'은(는) 자식 Room이 1개 이상이어야 합니다.");
                    }

                    break;

                case RoomRole.Combat:
                    if (childCount > 2)
                    {
                        result.AddError($"Combat Room '{room.RoomId}'의 차수가 3을 넘습니다.");
                    }

                    break;

                case RoomRole.Corridor:
                    if (childCount != 1)
                    {
                        result.AddError($"Corridor Room '{room.RoomId}'은(는) 자식 Room을 정확히 1개 가져야 합니다.");
                    }

                    break;
            }
        }
    }

    // 모든 물리 Combat Door의 반대편이 Corridor인지, Corridor는 Entrance/Combat과 Combat만 연결하는지
    // 검사한다(dungeon_system-design.md 4.3, Decision 007). Entrance나 Combat끼리 직접 연결되는 Edge는
    // 이 단계에서 거부한다.
    private static void ValidateAdjacentRoles(
        IReadOnlyList<DungeonRoomPlan> roomList,
        IReadOnlyDictionary<string, DungeonRoomPlan> roomsById,
        IReadOnlyDictionary<string, List<string>> children,
        DungeonPlanValidationResult result)
    {
        for (int i = 0; i < roomList.Count; i++)
        {
            DungeonRoomPlan room = roomList[i];

            if (room.Role == RoomRole.Combat)
            {
                if (!string.IsNullOrEmpty(room.ParentRoomId) &&
                    roomsById.TryGetValue(room.ParentRoomId, out DungeonRoomPlan parent) &&
                    parent.Role != RoomRole.Corridor)
                {
                    result.AddError(
                        $"Combat Room '{room.RoomId}'의 부모 Room '{room.ParentRoomId}'(Role: {parent.Role})이 " +
                        "Corridor가 아닙니다. 모든 물리 Combat Door의 반대편은 Corridor여야 합니다.");
                }
            }
            else if (room.Role == RoomRole.Entrance || room.Role == RoomRole.Corridor)
            {
                if (!children.TryGetValue(room.RoomId, out List<string> childIds))
                {
                    continue;
                }

                RoomRole expectedChildRole = room.Role == RoomRole.Entrance ? RoomRole.Corridor : RoomRole.Combat;

                for (int c = 0; c < childIds.Count; c++)
                {
                    if (roomsById.TryGetValue(childIds[c], out DungeonRoomPlan child) &&
                        child.Role != expectedChildRole)
                    {
                        result.AddError(
                            $"{room.Role} Room '{room.RoomId}'의 자식 Room '{childIds[c]}'(Role: {child.Role})이 " +
                            $"{expectedChildRole}가 아닙니다.");
                    }
                }
            }
        }
    }

    // Boss Dungeon은 Portal 대상 Combat Room ID가 정확히 하나 있어야 하고 실제 Combat Room을 가리켜야
    // 한다. Normal Dungeon은 Portal 대상이 없어야 한다(dungeon_system-design.md 4.2a).
    private static void ValidatePortalTarget(
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, DungeonRoomPlan> rooms,
        DungeonPlanValidationResult result)
    {
        bool isBossDungeon = plan.Definition.DungeonType == DungeonType.Boss;

        if (!isBossDungeon)
        {
            if (!string.IsNullOrEmpty(plan.PortalTargetRoomId))
            {
                result.AddError("Normal Dungeon은 Boss Portal 대상 Room을 가질 수 없습니다.");
            }

            return;
        }

        if (string.IsNullOrEmpty(plan.PortalTargetRoomId))
        {
            result.AddError("Boss Dungeon은 Boss Portal 대상 Combat Room이 정확히 하나 필요합니다.");
            return;
        }

        if (!rooms.TryGetValue(plan.PortalTargetRoomId, out DungeonRoomPlan portalTarget) ||
            portalTarget.Role != RoomRole.Combat)
        {
            result.AddError($"Boss Portal 대상 Room '{plan.PortalTargetRoomId}'이(가) 존재하는 Combat Room이 아닙니다.");
        }
    }

    // Room 최대 사용 횟수와 부모·자식 연속 중복 금지를 검사한다.
    private static void ValidateRoomUsage(
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, DungeonRoomPlan> rooms,
        DungeonPlanValidationResult result)
    {
        Dictionary<RoomArchetypeSO, int> useCounts = new Dictionary<RoomArchetypeSO, int>();

        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            DungeonRoomPlan room = plan.Rooms[i];
            useCounts.TryGetValue(room.Archetype, out int useCount);
            useCounts[room.Archetype] = useCount + 1;

            if (!string.IsNullOrEmpty(room.ParentRoomId) &&
                rooms.TryGetValue(room.ParentRoomId, out DungeonRoomPlan parent) &&
                parent.Archetype == room.Archetype &&
                room.Archetype.MaximumUsesPerDungeon <= 1)
            {
                result.AddError($"Room '{room.RoomId}'가 부모와 같은 Archetype을 연속 사용합니다.");
            }
        }

        foreach (KeyValuePair<RoomArchetypeSO, int> entry in useCounts)
        {
            if (entry.Value > entry.Key.MaximumUsesPerDungeon)
            {
                result.AddError($"Room '{entry.Key.ContentId}'가 최대 사용 횟수를 넘었습니다.");
            }
        }
    }

    // 연결 출입구의 방향·타입·폭 계약과 중복 사용 여부를 검사한다. 실제 셀 점유 겹침과 출입구 좌표
    // 정렬은 공간 생성기(Edgar)가 보장하는 영역이라 여기서는 좌표 자체를 다시 계산하지 않는다.
    private static void ValidatePlacement(
        DungeonBuildPlan plan,
        IReadOnlyDictionary<string, DungeonRoomPlan> rooms,
        DungeonPlanValidationResult result)
    {
        HashSet<string> usedPorts = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < plan.Connections.Count; i++)
        {
            DungeonConnectionPlan connection = plan.Connections[i];

            if (connection == null ||
                !rooms.TryGetValue(connection.ParentRoomId, out DungeonRoomPlan parent) ||
                !rooms.TryGetValue(connection.ChildRoomId, out DungeonRoomPlan child))
            {
                continue;
            }

            RoomConnectionDefinition parentPort = FindPort(parent.Archetype, connection.ParentConnectionId);
            RoomConnectionDefinition childPort = FindPort(child.Archetype, connection.ChildConnectionId);

            if (parentPort == null || childPort == null ||
                !DungeonPlanRules.AreOpposite(parentPort.Direction, childPort.Direction) ||
                !string.Equals(parentPort.ConnectionTypeId, childPort.ConnectionTypeId, StringComparison.Ordinal) ||
                parentPort.PassageWidthInCells != childPort.PassageWidthInCells)
            {
                result.AddError($"Connections[{i}]의 출입구 계약이 맞지 않습니다.");
            }

            if (!usedPorts.Add($"{parent.RoomId}:{connection.ParentConnectionId}") ||
                !usedPorts.Add($"{child.RoomId}:{connection.ChildConnectionId}"))
            {
                result.AddError($"Connections[{i}]가 이미 사용된 출입구를 다시 사용합니다.");
            }
        }
    }

    // 지정 역할의 첫 Room을 찾는다.
    private static DungeonRoomPlan FindRole(IReadOnlyList<DungeonRoomPlan> rooms, RoomRole role)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Role == role)
            {
                return rooms[i];
            }
        }

        return null;
    }

    // 부모에서 자식 방향으로 도달 가능한 Room ID 집합을 만든다.
    private static HashSet<string> CollectReachable(
        string entranceId,
        IReadOnlyDictionary<string, List<string>> children)
    {
        HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);
        Queue<string> queue = new Queue<string>();
        reachable.Add(entranceId);
        queue.Enqueue(entranceId);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            for (int i = 0; i < children[current].Count; i++)
            {
                string child = children[current][i];

                if (reachable.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }

        return reachable;
    }

    // Room에서 안정 ID가 일치하는 출입구를 찾는다.
    private static RoomConnectionDefinition FindPort(RoomArchetypeSO room, string connectionId)
    {
        for (int i = 0; i < room.Connections.Count; i++)
        {
            RoomConnectionDefinition port = room.Connections[i];

            if (port != null && string.Equals(port.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                return port;
            }
        }

        return null;
    }
}
