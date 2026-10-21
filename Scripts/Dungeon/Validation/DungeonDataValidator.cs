using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dungeon·Room 정적 정의의 ID, 참조, 역할 후보와 연결 계약을 검증한다.
/// </summary>
public static class DungeonDataValidator
{
    private const int MaximumContentRoomCountCap = 12;

    // Addressable 루트가 제공하는 Dungeon Authoring 정적 데이터 전체를 검증한다.
    public static DungeonDataValidationResult Validate(GameDatabaseSO database)
    {
        if (database == null)
        {
            DungeonDataValidationResult result = new DungeonDataValidationResult();
            result.AddError("GameDatabase가 null입니다.");
            return result;
        }

        return ValidateCollection(
            database.DungeonDefinitions,
            database.RoomArchetypes);
    }

    // GameDatabase가 보유할 Dungeon Authoring 정적 데이터 전체를 검증한다.
    public static DungeonDataValidationResult ValidateCollection(
        IReadOnlyList<DungeonDefinitionSO> dungeons,
        IReadOnlyList<RoomArchetypeSO> rooms)
    {
        DungeonDataValidationResult result = new DungeonDataValidationResult();
        HashSet<RoomArchetypeSO> roomCatalog = BuildRoomCatalog(rooms, result);

        ValidateRooms(rooms, result);
        ValidateDungeons(dungeons, roomCatalog, result);

        return result;
    }

    // Room 하나의 출입구·Spawn 지점 구성만 독립적으로 검증한다. GameDatabase 카탈로그가 필요한
    // 검증은 포함하지 않는다. Room Archetype Inspector의 요약·오류 표시가 사용한다.
    public static DungeonDataValidationResult ValidateRoomDoorsAndSpawnPoints(RoomArchetypeSO room)
    {
        DungeonDataValidationResult result = new DungeonDataValidationResult();

        if (room == null)
        {
            return result;
        }

        ValidateConnections(room, result);
        ValidateSpawnPoints(room, result);
        ValidateSpawnPointKinds(room, result);
        ValidateSpawnPointPrefabBinding(room, result);

        return result;
    }

    // null·중복 Room 콘텐츠 ID를 검사하며 유효한 Room 참조 집합을 만든다.
    private static HashSet<RoomArchetypeSO> BuildRoomCatalog(
        IReadOnlyList<RoomArchetypeSO> rooms,
        DungeonDataValidationResult result)
    {
        HashSet<RoomArchetypeSO> catalog = new HashSet<RoomArchetypeSO>();
        Dictionary<string, int> idOccurrences = new Dictionary<string, int>();

        if (rooms == null)
        {
            result.AddError("GameDatabase의 RoomArchetype 목록이 null입니다.");
            return catalog;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomArchetypeSO room = rooms[i];

            if (room == null)
            {
                result.AddError($"RoomArchetypes[{i}]가 null입니다.");
                continue;
            }

            catalog.Add(room);
            CountContentId(room.ContentId, idOccurrences);

            if (string.IsNullOrWhiteSpace(room.ContentId))
            {
                result.AddError($"'{room.name}'의 콘텐츠 ID가 비어 있습니다.", room);
            }
        }

        AddDuplicateIdErrors("Room", idOccurrences, result);
        return catalog;
    }

    // Room의 Prefab·크기·역할·출입구·Spawn 참조를 검증한다.
    private static void ValidateRooms(
        IReadOnlyList<RoomArchetypeSO> rooms,
        DungeonDataValidationResult result)
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomArchetypeSO room = rooms[i];

            if (room == null)
            {
                continue;
            }

            if (room.RoomPrefab == null)
            {
                result.AddError($"'{room.name}'의 Room Prefab이 비어 있습니다.", room);
            }

            if (room.MaximumUsesPerDungeon <= 0)
            {
                result.AddError($"'{room.name}'의 던전당 최대 사용 횟수는 0보다 커야 합니다.", room);
            }

            ValidateRoomRoles(room, result);
            ValidateConnections(room, result);
            ValidateSpawnPoints(room, result);
            ValidateSpawnPointKinds(room, result);
            ValidateSpawnPointPrefabBinding(room, result);
        }
    }

    // Room 역할의 빈 값·중복을 검증한다.
    private static void ValidateRoomRoles(RoomArchetypeSO room, DungeonDataValidationResult result)
    {
        IReadOnlyList<RoomRole> roles = room.Roles;

        if (roles == null || roles.Count == 0)
        {
            result.AddError($"'{room.name}'의 역할이 비어 있습니다.", room);
            return;
        }

        HashSet<RoomRole> distinctRoles = new HashSet<RoomRole>();

        for (int i = 0; i < roles.Count; i++)
        {
            if (!Enum.IsDefined(typeof(RoomRole), roles[i]))
            {
                result.AddError($"'{room.name}'의 Roles[{i}] 값이 유효하지 않습니다.", room);
            }
            else if (!distinctRoles.Add(roles[i]))
            {
                result.AddError($"'{room.name}'의 역할 '{roles[i]}'이(가) 중복되었습니다.", room);
            }
        }
    }

    // Room 출입구의 ID·방향·타입·통로 폭과 중복을 검증한다. Boss Arena는 물리 Graph 밖의 별도 Stage라
    // 출입구가 없어도 된다(dungeon_system-design.md 4.1, 4.2a).
    private static void ValidateConnections(RoomArchetypeSO room, DungeonDataValidationResult result)
    {
        IReadOnlyList<RoomConnectionDefinition> connections = room.Connections;

        if (connections == null || connections.Count == 0)
        {
            if (!room.HasRole(RoomRole.Boss))
            {
                result.AddError($"'{room.name}'의 출입구가 비어 있습니다.", room);
            }

            return;
        }

        HashSet<string> connectionIds = new HashSet<string>();

        for (int i = 0; i < connections.Count; i++)
        {
            RoomConnectionDefinition connection = connections[i];

            if (connection == null)
            {
                result.AddError($"'{room.name}'의 Connections[{i}]가 null입니다.", room);
                continue;
            }

            if (string.IsNullOrWhiteSpace(connection.ConnectionId))
            {
                result.AddError($"'{room.name}'의 Connections[{i}] ID가 비어 있습니다.", room);
            }
            else if (!connectionIds.Add(connection.ConnectionId))
            {
                result.AddError($"'{room.name}'의 출입구 ID '{connection.ConnectionId}'이(가) 중복되었습니다.", room);
            }

            if (!Enum.IsDefined(typeof(RoomConnectionDirection), connection.Direction))
            {
                result.AddError($"'{room.name}'의 출입구 '{connection.ConnectionId}' 방향이 유효하지 않습니다.", room);
            }

            if (!string.Equals(
                    connection.ConnectionTypeId,
                    RoomConnectionDefinition.StandardConnectionTypeId,
                    StringComparison.Ordinal))
            {
                result.AddError(
                    $"'{room.name}'의 출입구 '{connection.ConnectionId}' 타입은 '{RoomConnectionDefinition.StandardConnectionTypeId}'만 지원하는데 '{connection.ConnectionTypeId}'입니다.",
                    room);
            }

            if (connection.PassageWidthInCells != RoomConnectionDefinition.StandardPassageWidthInCells)
            {
                result.AddError(
                    $"'{room.name}'의 출입구 '{connection.ConnectionId}' 통로 폭은 {RoomConnectionDefinition.StandardPassageWidthInCells}여야 하는데 {connection.PassageWidthInCells}입니다.",
                    room);
            }
        }
    }

    // Spawn 지점의 null·빈 ID·중복 ID를 검증한다.
    private static void ValidateSpawnPoints(RoomArchetypeSO room, DungeonDataValidationResult result)
    {
        IReadOnlyList<RoomSpawnPointDefinition> spawnPoints = room.SpawnPoints;
        bool requiresSpawnPoint = room.HasRole(RoomRole.Combat) || room.HasRole(RoomRole.Boss);

        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            if (requiresSpawnPoint)
            {
                result.AddError($"전투 역할 Room '{room.name}'의 Spawn 지점이 비어 있습니다.", room);
            }

            return;
        }

        HashSet<string> spawnPointIds = new HashSet<string>();

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            RoomSpawnPointDefinition spawnPoint = spawnPoints[i];

            if (spawnPoint == null)
            {
                result.AddError($"'{room.name}'의 SpawnPoints[{i}]가 null입니다.", room);
                continue;
            }

            if (string.IsNullOrWhiteSpace(spawnPoint.SpawnPointId))
            {
                result.AddError($"'{room.name}'의 SpawnPoints[{i}] ID가 비어 있습니다.", room);
            }
            else if (!spawnPointIds.Add(spawnPoint.SpawnPointId))
            {
                result.AddError($"'{room.name}'의 Spawn 지점 ID '{spawnPoint.SpawnPointId}'이(가) 중복되었습니다.", room);
            }
        }
    }

    // Room 역할별로 요구되는 Spawn Point Kind 구성(Entrance·Boss=Player 1개, Combat=RegularEnemy 1개
    // 이상, Corridor=Player·Boss 금지, Boss=Boss 1개)을 검증한다. Boss Arena는 Portal 전환으로
    // Player가 직접 배치되므로 Entrance와 동일하게 자체 Player Spawn 지점이 필요하다(4.2a, 7.1).
    private static void ValidateSpawnPointKinds(RoomArchetypeSO room, DungeonDataValidationResult result)
    {
        IReadOnlyList<RoomSpawnPointDefinition> spawnPoints = room.SpawnPoints;

        if (room.HasRole(RoomRole.Entrance) || room.HasRole(RoomRole.Boss))
        {
            int playerPointCount = CountSpawnPointsOfKind(spawnPoints, DungeonSpawnPointKind.Player);

            if (playerPointCount != 1)
            {
                result.AddError(
                    $"'{room.name}'은(는) Player Spawn 지점을 정확히 1개 가져야 하는데 {playerPointCount}개입니다.",
                    room);
            }
        }

        if (room.HasRole(RoomRole.Combat))
        {
            int regularEnemyPointCount = CountSpawnPointsOfKind(spawnPoints, DungeonSpawnPointKind.RegularEnemy);

            if (regularEnemyPointCount < 1)
            {
                result.AddError(
                    $"Combat Room '{room.name}'은(는) RegularEnemy Spawn 지점이 1개 이상이어야 합니다.",
                    room);
            }
        }

        if (room.HasRole(RoomRole.Corridor) &&
            (CountSpawnPointsOfKind(spawnPoints, DungeonSpawnPointKind.Player) > 0 ||
             CountSpawnPointsOfKind(spawnPoints, DungeonSpawnPointKind.Boss) > 0))
        {
            result.AddError($"Corridor Room '{room.name}'은(는) Player·Boss Spawn 지점을 가질 수 없습니다.", room);
        }

        if (room.HasRole(RoomRole.Boss))
        {
            int bossPointCount = CountSpawnPointsOfKind(spawnPoints, DungeonSpawnPointKind.Boss);

            if (bossPointCount != 1)
            {
                result.AddError(
                    $"Boss Room '{room.name}'은(는) Boss Spawn 지점을 정확히 1개 가져야 하는데 {bossPointCount}개입니다.",
                    room);
            }
        }
    }

    // 지정 Kind를 가진 Spawn 지점 수를 센다.
    private static int CountSpawnPointsOfKind(IReadOnlyList<RoomSpawnPointDefinition> spawnPoints, DungeonSpawnPointKind kind)
    {
        if (spawnPoints == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] != null && spawnPoints[i].Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    // Room Prefab의 RoomSpawnPointView 구성이 Definition의 ID·Kind와 정확히 일치하는지 검증한다.
    private static void ValidateSpawnPointPrefabBinding(RoomArchetypeSO room, DungeonDataValidationResult result)
    {
        if (room.RoomPrefab == null)
        {
            return;
        }

        RoomSpawnPointView[] views = room.RoomPrefab.GetComponentsInChildren<RoomSpawnPointView>(true);
        Dictionary<string, DungeonSpawnPointKind> viewKindsById = new Dictionary<string, DungeonSpawnPointKind>();

        for (int i = 0; i < views.Length; i++)
        {
            RoomSpawnPointView view = views[i];

            if (view == null || string.IsNullOrWhiteSpace(view.SpawnPointId))
            {
                continue;
            }

            if (!viewKindsById.TryAdd(view.SpawnPointId, view.Kind))
            {
                result.AddError($"'{room.name}'의 Prefab Spawn View ID '{view.SpawnPointId}'이(가) 중복되었습니다.", room);
            }
        }

        IReadOnlyList<RoomSpawnPointDefinition> definitions = room.SpawnPoints;
        HashSet<string> matchedIds = new HashSet<string>();

        if (definitions != null)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                RoomSpawnPointDefinition definition = definitions[i];

                if (definition == null || string.IsNullOrWhiteSpace(definition.SpawnPointId))
                {
                    continue;
                }

                matchedIds.Add(definition.SpawnPointId);

                if (!viewKindsById.TryGetValue(definition.SpawnPointId, out DungeonSpawnPointKind viewKind))
                {
                    result.AddError(
                        $"'{room.name}'의 Spawn 지점 '{definition.SpawnPointId}'에 대응하는 Prefab View가 없습니다.",
                        room);
                    continue;
                }

                if (viewKind != definition.Kind)
                {
                    result.AddError(
                        $"'{room.name}'의 Spawn 지점 '{definition.SpawnPointId}' Kind가 Definition '{definition.Kind}'과 View '{viewKind}'로 다릅니다.",
                        room);
                }
            }
        }

        foreach (KeyValuePair<string, DungeonSpawnPointKind> viewEntry in viewKindsById)
        {
            if (!matchedIds.Contains(viewEntry.Key))
            {
                result.AddError(
                    $"'{room.name}'의 Prefab View '{viewEntry.Key}'에 대응하는 Spawn Point Definition이 없습니다.",
                    room);
            }
        }
    }

    // Dungeon ID·생성 범위·Room Pool·필수 역할 후보와 연결 호환성을 검증한다.
    private static void ValidateDungeons(
        IReadOnlyList<DungeonDefinitionSO> dungeons,
        HashSet<RoomArchetypeSO> roomCatalog,
        DungeonDataValidationResult result)
    {
        if (dungeons == null)
        {
            result.AddError("GameDatabase의 DungeonDefinition 목록이 null입니다.");
            return;
        }

        Dictionary<string, int> idOccurrences = new Dictionary<string, int>();

        for (int i = 0; i < dungeons.Count; i++)
        {
            DungeonDefinitionSO dungeon = dungeons[i];

            if (dungeon == null)
            {
                result.AddError($"DungeonDefinitions[{i}]가 null입니다.");
                continue;
            }

            CountContentId(dungeon.ContentId, idOccurrences);

            if (string.IsNullOrWhiteSpace(dungeon.ContentId))
            {
                result.AddError($"'{dungeon.name}'의 콘텐츠 ID가 비어 있습니다.", dungeon);
            }

            ValidateDungeon(dungeon, roomCatalog, result);
        }

        AddDuplicateIdErrors("Dungeon", idOccurrences, result);
    }

    // Dungeon 하나의 수치 범위와 Room 후보 구성을 검증한다.
    private static void ValidateDungeon(
        DungeonDefinitionSO dungeon,
        HashSet<RoomArchetypeSO> roomCatalog,
        DungeonDataValidationResult result)
    {
        if (dungeon.GenerationVersion <= 0)
        {
            result.AddError($"'{dungeon.name}'의 생성 버전은 0보다 커야 합니다.", dungeon);
        }

        if (dungeon.MinimumContentRoomCount <= 0 ||
            dungeon.MaximumContentRoomCount < dungeon.MinimumContentRoomCount)
        {
            result.AddError($"'{dungeon.name}'의 콘텐츠 Room 수 범위가 유효하지 않습니다.", dungeon);
        }

        if (dungeon.DungeonType == DungeonType.Normal &&
            dungeon.MaximumContentRoomCount > MaximumContentRoomCountCap)
        {
            result.AddError(
                $"'{dungeon.name}'의 콘텐츠 Room 수 상한은 {MaximumContentRoomCountCap}을 넘을 수 없습니다.",
                dungeon);
        }

        HashSet<RoomArchetypeSO> roomPool = ValidateRoomPool(dungeon, roomCatalog, result);

        ValidateRequiredRoleCandidates(dungeon, roomPool, result);
        ValidateBossRooms(dungeon, roomCatalog, result);
        ValidateConnectionCompatibility(dungeon, roomPool, result);
    }

    // Dungeon Room Pool의 null·중복·카탈로그 소속을 검증한다.
    private static HashSet<RoomArchetypeSO> ValidateRoomPool(
        DungeonDefinitionSO dungeon,
        HashSet<RoomArchetypeSO> roomCatalog,
        DungeonDataValidationResult result)
    {
        HashSet<RoomArchetypeSO> roomPool = new HashSet<RoomArchetypeSO>();

        if (dungeon.RoomPool == null || dungeon.RoomPool.Count == 0)
        {
            result.AddError($"'{dungeon.name}'의 Room Pool이 비어 있습니다.", dungeon);
            return roomPool;
        }

        for (int i = 0; i < dungeon.RoomPool.Count; i++)
        {
            RoomArchetypeSO room = dungeon.RoomPool[i];

            if (room == null)
            {
                result.AddError($"'{dungeon.name}'의 RoomPool[{i}]가 null입니다.", dungeon);
            }
            else if (!roomPool.Add(room))
            {
                result.AddError($"'{dungeon.name}'의 Room Pool에 '{room.name}'이(가) 중복 등록되었습니다.", dungeon);
            }
            else if (!roomCatalog.Contains(room))
            {
                result.AddError($"'{dungeon.name}'이 GameDatabase에 없는 Room '{room.name}'을 참조합니다.", dungeon);
            }
        }

        return roomPool;
    }

    // 일반·보스 Dungeon별로 반드시 필요한 Room 역할 후보가 존재하는지 검증한다.
    private static void ValidateRequiredRoleCandidates(
        DungeonDefinitionSO dungeon,
        HashSet<RoomArchetypeSO> roomPool,
        DungeonDataValidationResult result)
    {
        RequireRole(dungeon, roomPool, RoomRole.Entrance, result);
        RequireRole(dungeon, roomPool, RoomRole.Combat, result);

        if (dungeon.DungeonType != DungeonType.Normal && dungeon.DungeonType != DungeonType.Boss)
        {
            result.AddError($"'{dungeon.name}'의 DungeonType 값이 유효하지 않습니다.", dungeon);
        }
    }

    // 보스 Dungeon의 고정 Arena Room이 참조·역할·카탈로그 계약을 만족하는지 검증한다.
    private static void ValidateBossRooms(
        DungeonDefinitionSO dungeon,
        HashSet<RoomArchetypeSO> roomCatalog,
        DungeonDataValidationResult result)
    {
        if (dungeon.DungeonType != DungeonType.Boss)
        {
            return;
        }

        if (dungeon.MinimumContentRoomCount < 2 || dungeon.MaximumContentRoomCount > 4)
        {
            result.AddError($"보스 Dungeon '{dungeon.name}'의 1층 전투 Room 수 범위는 2~4여야 합니다.", dungeon);
        }

        ValidateFixedBossRoom(dungeon, dungeon.BossArenaRoom, RoomRole.Boss, "Arena", roomCatalog, result);
    }

    private static void ValidateFixedBossRoom(
        DungeonDefinitionSO dungeon,
        RoomArchetypeSO room,
        RoomRole requiredRole,
        string label,
        HashSet<RoomArchetypeSO> roomCatalog,
        DungeonDataValidationResult result)
    {
        if (room == null)
        {
            result.AddError($"보스 Dungeon '{dungeon.name}'의 고정 {label} Room이 비어 있습니다.", dungeon);
            return;
        }

        if (!roomCatalog.Contains(room))
        {
            result.AddError($"보스 Dungeon '{dungeon.name}'의 고정 {label} Room이 GameDatabase에 없습니다.", dungeon);
        }

        if (!room.HasRole(requiredRole))
        {
            result.AddError($"보스 Dungeon '{dungeon.name}'의 고정 {label} Room에 '{requiredRole}' 역할이 없습니다.", dungeon);
        }
    }

    // Room Pool에 지정 역할 후보가 하나 이상 있는지 검증한다.
    private static void RequireRole(
        DungeonDefinitionSO dungeon,
        HashSet<RoomArchetypeSO> roomPool,
        RoomRole role,
        DungeonDataValidationResult result)
    {
        foreach (RoomArchetypeSO room in roomPool)
        {
            if (room.HasRole(role))
            {
                return;
            }
        }

        result.AddError($"'{dungeon.name}'의 Room Pool에 필수 역할 '{role}' 후보가 없습니다.", dungeon);
    }

    // 각 Room 출입구에 반대 방향·같은 타입·같은 폭의 상대 출입구가 존재하는지 검증한다.
    private static void ValidateConnectionCompatibility(
        DungeonDefinitionSO dungeon,
        HashSet<RoomArchetypeSO> roomPool,
        DungeonDataValidationResult result)
    {
        foreach (RoomArchetypeSO room in roomPool)
        {
            if (room.Connections == null)
            {
                continue;
            }

            for (int i = 0; i < room.Connections.Count; i++)
            {
                RoomConnectionDefinition connection = room.Connections[i];

                if (connection == null || HasCompatibleConnection(room, i, connection, roomPool))
                {
                    continue;
                }

                result.AddError(
                    $"'{dungeon.name}'에서 Room '{room.name}'의 출입구 '{connection.ConnectionId}'과 호환되는 상대 출입구가 없습니다.",
                    room);
            }
        }
    }

    // Pool에서 지정 출입구와 호환되는 상대 출입구를 찾는다.
    // 같은 Archetype을 던전당 여러 번 배치할 수 있을 때(MaximumUsesPerDungeon > 1)만 자기 자신과의 연결(다른 출입구 항목 한정)을 허용한다.
    private static bool HasCompatibleConnection(
        RoomArchetypeSO sourceRoom,
        int sourceConnectionIndex,
        RoomConnectionDefinition sourceConnection,
        HashSet<RoomArchetypeSO> roomPool)
    {
        RoomConnectionDirection oppositeDirection = DungeonPlanRules.GetOppositeDirection(sourceConnection.Direction);

        foreach (RoomArchetypeSO candidateRoom in roomPool)
        {
            bool isSelf = candidateRoom == sourceRoom;

            if (isSelf && sourceRoom.MaximumUsesPerDungeon <= 1)
            {
                continue;
            }

            if (candidateRoom.Connections == null)
            {
                continue;
            }

            for (int i = 0; i < candidateRoom.Connections.Count; i++)
            {
                if (isSelf && i == sourceConnectionIndex)
                {
                    continue;
                }

                RoomConnectionDefinition candidate = candidateRoom.Connections[i];

                if (candidate != null &&
                    candidate.Direction == oppositeDirection &&
                    candidate.PassageWidthInCells == sourceConnection.PassageWidthInCells &&
                    string.Equals(candidate.ConnectionTypeId, sourceConnection.ConnectionTypeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // 콘텐츠 ID가 비어 있지 않으면 발생 횟수를 누적한다.
    private static void CountContentId(string contentId, Dictionary<string, int> occurrences)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return;
        }

        occurrences.TryGetValue(contentId, out int count);
        occurrences[contentId] = count + 1;
    }

    // 두 번 이상 사용된 콘텐츠 ID를 오류로 추가한다.
    private static void AddDuplicateIdErrors(
        string label,
        Dictionary<string, int> occurrences,
        DungeonDataValidationResult result)
    {
        foreach (KeyValuePair<string, int> occurrence in occurrences)
        {
            if (occurrence.Value > 1)
            {
                result.AddError($"{label} 콘텐츠 ID '{occurrence.Key}'가 {occurrence.Value}개에서 중복 사용되었습니다.");
            }
        }
    }
}
