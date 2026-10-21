using System.Collections.Generic;
using Edgar.Unity;
using UnityEngine;

/// <summary>
/// DungeonTopologyPlan을 Edgar Free 공간 생성기로 배치·연결한 뒤 DungeonBuildPlan으로 변환하는 유일한
/// Edgar 의존 경계. Edgar 타입(LevelGraph, Room, Connection, DungeonGeneratorGrid2D,
/// RoomInstanceGrid2D, DoorInstanceGrid2D 등)은 이 클래스 밖으로 반환하지 않는다.
/// </summary>
/// <remarks>
/// Edgar가 Instantiate한 Room GameObject는 폐기하지 않고 생성 Root째 결과에 담아 소유권을 넘긴다.
/// 받은 쪽(DungeonRuntimeBinder)이 RoomRuntime으로 Binding하고 정리 권한까지 가져간다. 변환 전에
/// 중단하는 경우에만 Adapter가 생성 Root를 직접 파괴한다.
/// </remarks>
public sealed class EdgarGenerationAdapter
{
    // Topology와 Dungeon 정의로 Edgar 공간 생성을 수행하고 결과를 DungeonBuildPlan으로 변환한다.
    public EdgarGenerationResult Generate(
        DungeonDefinitionSO definition,
        DungeonTopologyPlan topology,
        int seed,
        int generationVersion)
    {
        if (definition == null)
        {
            return EdgarGenerationResult.Failed(EdgarGenerationFailure.InvalidDefinition);
        }

        if (topology == null || topology.Nodes.Count == 0 || topology.Nodes[0].Role != RoomRole.Entrance)
        {
            return EdgarGenerationResult.Failed(EdgarGenerationFailure.InvalidTopology);
        }

        Dictionary<string, int> requiredConnectionCountByNodeId = BuildRequiredConnectionCounts(topology);

        if (!TryBuildCandidatesByNode(
                definition,
                topology,
                requiredConnectionCountByNodeId,
                out Dictionary<string, List<RoomArchetypeSO>> candidatesByNodeId,
                out EdgarGenerationFailure candidateFailure))
        {
            return EdgarGenerationResult.Failed(candidateFailure);
        }

        if (!TryBuildReverseTemplateMap(
                candidatesByNodeId,
                out Dictionary<GameObject, RoomArchetypeSO> templateToArchetype))
        {
            return EdgarGenerationResult.Failed(EdgarGenerationFailure.RoomTemplateMappingAmbiguous);
        }

        int edgarSeed = DungeonPlanRules.CombineSeed(seed, "edgar_spatial", generationVersion);

        LevelGraph levelGraph = null;
        GameObject generatorObject = null;
        GameObject generatedRoot = null;
        bool succeeded = false;

        try
        {
            Dictionary<string, Room> edgarRoomsByNodeId = new Dictionary<string, Room>();
            levelGraph = BuildLevelGraph(topology, candidatesByNodeId, edgarRoomsByNodeId);

            // DungeonGeneratorGrid2D.GenerateOn 기본값은 Awake이고, Play Mode에서는 AddComponent가 Awake를
            // 동기 호출한다. 설정 전에 즉시 활성화하면 미구성 상태(UseRandomSeed/FixedLevelGraphConfig 기본값)로
            // 조기 자동 생성이 한 번 더 실행되어 원인 불명의 NullReferenceException을 남긴다. 비활성 상태로
            // 추가해 Awake를 지연시키고, GenerateOn=Manually까지 구성을 끝낸 뒤에만 활성화한다.
            generatorObject = new GameObject($"EdgarGenerationAdapter_{definition.ContentId}");
            generatorObject.SetActive(false);
            DungeonGeneratorGrid2D generator = generatorObject.AddComponent<DungeonGeneratorGrid2D>();
            generatedRoot = new GameObject($"EdgarGeneratedRoot_{definition.ContentId}");
            ConfigureGenerator(generator, levelGraph, edgarSeed, generatedRoot);
            generatorObject.SetActive(true);

            DungeonGeneratorPayloadGrid2D payload;

            try
            {
                payload = generator.Generate() as DungeonGeneratorPayloadGrid2D;
            }
            catch (System.Exception)
            {
                return EdgarGenerationResult.Failed(EdgarGenerationFailure.GenerationFailed);
            }

            if (payload?.GeneratedLevel == null)
            {
                return EdgarGenerationResult.Failed(EdgarGenerationFailure.GenerationFailed);
            }

            DungeonGeneratorLevelGrid2D level = payload.GeneratedLevel;

            if (!TryConvertRooms(
                    definition,
                    seed,
                    generationVersion,
                    topology,
                    edgarRoomsByNodeId,
                    level,
                    templateToArchetype,
                    requiredConnectionCountByNodeId,
                    out List<DungeonRoomPlan> rooms,
                    out Dictionary<string, GameObject> roomInstancesByRoomId,
                    out EdgarGenerationFailure roomFailure))
            {
                return EdgarGenerationResult.Failed(roomFailure);
            }

            if (!TryConvertConnections(
                    topology,
                    edgarRoomsByNodeId,
                    level,
                    candidatesByNodeId,
                    out List<DungeonConnectionPlan> connections,
                    out EdgarGenerationFailure connectionFailure))
            {
                return EdgarGenerationResult.Failed(connectionFailure);
            }

            DungeonBuildPlan plan = new DungeonBuildPlan(
                definition,
                seed,
                generationVersion,
                topology,
                rooms,
                connections);

            succeeded = true;
            return EdgarGenerationResult.Success(plan, roomInstancesByRoomId, generatedRoot);
        }
        finally
        {
            // 성공 시 생성 Instance와 전용 Root는 실제 Room 콘텐츠이므로 파괴하지 않는다 — 소유권이
            // 호출자(결국 DungeonRuntimeBinder/DungeonAssembly)로 넘어간다. Generator 컴포넌트와 임시
            // LevelGraph는 생성 보조 도구일 뿐이라 성공·실패 무관하게 항상 정리한다.
            if (!succeeded)
            {
                DestroyImmediateOrRuntime(generatedRoot);
            }

            DestroyImmediateOrRuntime(generatorObject);
            DestroyLevelGraph(levelGraph);
        }
    }

    // Topology Node별 목표 차수(부모 Edge 1개 + 자식 Edge 수)를 계산한다. Room 후보 필터와 사후 대조 양쪽에 쓰인다.
    private static Dictionary<string, int> BuildRequiredConnectionCounts(DungeonTopologyPlan topology)
    {
        Dictionary<string, int> requiredCounts = new Dictionary<string, int>(topology.Nodes.Count);

        for (int i = 0; i < topology.Nodes.Count; i++)
        {
            requiredCounts[topology.Nodes[i].NodeId] = 0;
        }

        for (int i = 0; i < topology.Nodes.Count; i++)
        {
            DungeonTopologyNode node = topology.Nodes[i];

            if (string.IsNullOrEmpty(node.ParentNodeId))
            {
                continue;
            }

            requiredCounts[node.NodeId]++;
            requiredCounts[node.ParentNodeId]++;
        }

        return requiredCounts;
    }

    // RoomRole별로 목표 차수를 만족하는 Content ID 기준 안정 정렬된 RoomArchetypeSO 후보를 모은다.
    // Prefab이 없는 후보는 제외한다.
    private static bool TryBuildCandidatesByNode(
        DungeonDefinitionSO definition,
        DungeonTopologyPlan topology,
        IReadOnlyDictionary<string, int> requiredConnectionCountByNodeId,
        out Dictionary<string, List<RoomArchetypeSO>> candidatesByNodeId,
        out EdgarGenerationFailure failure)
    {
        candidatesByNodeId = new Dictionary<string, List<RoomArchetypeSO>>();

        for (int i = 0; i < topology.Nodes.Count; i++)
        {
            DungeonTopologyNode node = topology.Nodes[i];
            List<RoomArchetypeSO> candidates = BuildSortedTemplateCandidates(
                definition, node.Role, requiredConnectionCountByNodeId[node.NodeId]);

            if (candidates.Count == 0)
            {
                failure = EdgarGenerationFailure.TemplateCandidateUnavailable;
                return false;
            }

            candidatesByNodeId.Add(node.NodeId, candidates);
        }

        failure = EdgarGenerationFailure.None;
        return true;
    }

    // 역할·목표 차수(Door 수)·던전당 사용 횟수를 모두 만족하고 Template으로 쓸 RoomPrefab이 있는 Room
    // 후보를 Content ID 순서로 만든다. 정렬은 같은 Seed가 같은 배치를 내는 결정성의 전제다.
    // Boss Arena는 물리 Graph의 Node가 아니므로(dungeon_system-design.md 4.2a) Topology가 role=Boss를
    // 요청하는 일이 없고, 여기서는 RoomPool만 대상으로 한다.
    internal static List<RoomArchetypeSO> BuildSortedTemplateCandidates(
        DungeonDefinitionSO definition,
        RoomRole role,
        int requiredConnectionCount)
    {
        List<RoomArchetypeSO> candidates = new List<RoomArchetypeSO>();

        for (int i = 0; i < definition.RoomPool.Count; i++)
        {
            RoomArchetypeSO room = definition.RoomPool[i];

            if (room != null &&
                room.RoomPrefab != null &&
                room.HasRole(role) &&
                room.MaximumUsesPerDungeon > 0 &&
                room.Connections != null &&
                room.Connections.Count == requiredConnectionCount)
            {
                candidates.Add(room);
            }
        }

        candidates.Sort((left, right) => string.CompareOrdinal(left.ContentId, right.ContentId));
        return candidates;
    }

    // 후보 집합 전체에서 Template Prefab → RoomArchetypeSO 역매핑이 유일한지 확인한다.
    private static bool TryBuildReverseTemplateMap(
        Dictionary<string, List<RoomArchetypeSO>> candidatesByNodeId,
        out Dictionary<GameObject, RoomArchetypeSO> templateToArchetype)
    {
        templateToArchetype = new Dictionary<GameObject, RoomArchetypeSO>();

        foreach (KeyValuePair<string, List<RoomArchetypeSO>> entry in candidatesByNodeId)
        {
            for (int i = 0; i < entry.Value.Count; i++)
            {
                RoomArchetypeSO archetype = entry.Value[i];

                if (templateToArchetype.TryGetValue(archetype.RoomPrefab, out RoomArchetypeSO existing))
                {
                    if (existing != archetype)
                    {
                        return false;
                    }

                    continue;
                }

                templateToArchetype.Add(archetype.RoomPrefab, archetype);
            }
        }

        return true;
    }

    // Topology 노드를 Edgar Room으로, 부모-자식 관계를 Edgar Connection으로 변환한 임시 LevelGraph를 만든다.
    private static LevelGraph BuildLevelGraph(
        DungeonTopologyPlan topology,
        Dictionary<string, List<RoomArchetypeSO>> candidatesByNodeId,
        Dictionary<string, Room> edgarRoomsByNodeId)
    {
        LevelGraph levelGraph = ScriptableObject.CreateInstance<LevelGraph>();

        for (int i = 0; i < topology.Nodes.Count; i++)
        {
            DungeonTopologyNode node = topology.Nodes[i];
            Room room = ScriptableObject.CreateInstance<Room>();
            room.Name = node.NodeId;
            room.IndividualRoomTemplates = new List<GameObject>();

            List<RoomArchetypeSO> candidates = candidatesByNodeId[node.NodeId];

            for (int c = 0; c < candidates.Count; c++)
            {
                room.IndividualRoomTemplates.Add(candidates[c].RoomPrefab);
            }

            levelGraph.Rooms.Add(room);
            edgarRoomsByNodeId.Add(node.NodeId, room);
        }

        for (int i = 0; i < topology.Nodes.Count; i++)
        {
            DungeonTopologyNode node = topology.Nodes[i];

            if (string.IsNullOrEmpty(node.ParentNodeId))
            {
                continue;
            }

            Connection connection = ScriptableObject.CreateInstance<Connection>();
            connection.From = edgarRoomsByNodeId[node.ParentNodeId];
            connection.To = edgarRoomsByNodeId[node.NodeId];
            levelGraph.Connections.Add(connection);
        }

        return levelGraph;
    }

    // Generator를 수동 생성, 고정 파생 Seed, 전용 Root로 구성한다.
    private static void ConfigureGenerator(
        DungeonGeneratorGrid2D generator,
        LevelGraph levelGraph,
        int edgarSeed,
        GameObject generatedRoot)
    {
        generator.FixedLevelGraphConfig = new FixedLevelGraphConfigGrid2D
        {
            LevelGraph = levelGraph,
            UseCorridors = false,
        };
        generator.UseRandomSeed = false;
        generator.RandomGeneratorSeed = edgarSeed;
        generator.GenerateOn = GenerateOn.Manually;
        generator.GeneratorConfig = new DungeonGeneratorConfigGrid2D
        {
            RootGameObject = generatedRoot,
        };

        // 프로젝트는 Room별 개별 Collider(Door Blocker, EntryTrigger, WallCollision)에 의존하므로
        // PostProcessing의 기본 Collider/Renderer 비활성화를 끈다. 공유 Tilemap 병합은 아직 사용하지 않는다.
        generator.PostProcessConfig = new PostProcessingConfigGrid2D
        {
            InitializeSharedTilemaps = false,
            CopyTilesToSharedTilemaps = false,
            CenterGrid = false,
            DisableRoomTemplatesRenderers = false,
            DisableRoomTemplatesColliders = false,
            AnalyzeLevelStructure = false,
        };
    }

    // Edgar 결과를 RoomId 기준으로 조회해 DungeonRoomPlan을 만든다. RoomInstances 열거 순서에 의존하지 않는다.
    private static bool TryConvertRooms(
        DungeonDefinitionSO definition,
        int seed,
        int generationVersion,
        DungeonTopologyPlan topology,
        Dictionary<string, Room> edgarRoomsByNodeId,
        DungeonGeneratorLevelGrid2D level,
        Dictionary<GameObject, RoomArchetypeSO> templateToArchetype,
        IReadOnlyDictionary<string, int> requiredConnectionCountByNodeId,
        out List<DungeonRoomPlan> rooms,
        out Dictionary<string, GameObject> roomInstancesByRoomId,
        out EdgarGenerationFailure failure)
    {
        rooms = new List<DungeonRoomPlan>(topology.Nodes.Count);
        roomInstancesByRoomId = new Dictionary<string, GameObject>(topology.Nodes.Count);

        for (int i = 0; i < topology.Nodes.Count; i++)
        {
            DungeonTopologyNode node = topology.Nodes[i];
            Room edgarRoom = edgarRoomsByNodeId[node.NodeId];
            RoomInstanceGrid2D instance;

            try
            {
                instance = level.GetRoomInstance(edgarRoom);
            }
            catch (KeyNotFoundException)
            {
                rooms = null;
                roomInstancesByRoomId = null;
                failure = EdgarGenerationFailure.RoomMappingFailed;
                return false;
            }

            if (instance?.RoomTemplatePrefab == null ||
                instance.RoomTemplateInstance == null ||
                !templateToArchetype.TryGetValue(instance.RoomTemplatePrefab, out RoomArchetypeSO archetype))
            {
                rooms = null;
                roomInstancesByRoomId = null;
                failure = EdgarGenerationFailure.RoomMappingFailed;
                return false;
            }

            // 후보 필터(TryBuildCandidatesByNode)가 이미 목표 차수와 일치하는 후보만 제공했으므로 이 대조는
            // 항상 통과해야 한다. 그럼에도 어긋나면 "정확 일치" 계약이 깨진 것이므로 보정 없이 실패시킨다.
            if (archetype.Connections == null ||
                archetype.Connections.Count != requiredConnectionCountByNodeId[node.NodeId])
            {
                rooms = null;
                roomInstancesByRoomId = null;
                failure = EdgarGenerationFailure.RoomMappingFailed;
                return false;
            }

            Vector3Int position = instance.Position;
            rooms.Add(new DungeonRoomPlan(
                node, archetype, new Vector2Int(position.x, position.y)));
            roomInstancesByRoomId.Add(node.NodeId, instance.RoomTemplateInstance);
        }

        rooms.Sort((left, right) => string.CompareOrdinal(left.RoomId, right.RoomId));
        failure = EdgarGenerationFailure.None;
        return true;
    }

    // 부모-자식 Room 사이 실제 사용 Door를 RoomArchetypeSO._connections와 매칭해 DungeonConnectionPlan으로 변환한다.
    private static bool TryConvertConnections(
        DungeonTopologyPlan topology,
        Dictionary<string, Room> edgarRoomsByNodeId,
        DungeonGeneratorLevelGrid2D level,
        Dictionary<string, List<RoomArchetypeSO>> candidatesByNodeId,
        out List<DungeonConnectionPlan> connections,
        out EdgarGenerationFailure failure)
    {
        connections = new List<DungeonConnectionPlan>();

        for (int i = 0; i < topology.Nodes.Count; i++)
        {
            DungeonTopologyNode childNode = topology.Nodes[i];

            if (string.IsNullOrEmpty(childNode.ParentNodeId))
            {
                continue;
            }

            Room parentRoom = edgarRoomsByNodeId[childNode.ParentNodeId];
            Room childRoom = edgarRoomsByNodeId[childNode.NodeId];
            RoomInstanceGrid2D parentInstance = level.GetRoomInstance(parentRoom);
            RoomInstanceGrid2D childInstance = level.GetRoomInstance(childRoom);

            DoorInstanceGrid2D childDoor = FindDoorTo(childInstance, parentRoom);
            DoorInstanceGrid2D parentDoor = FindDoorTo(parentInstance, childRoom);

            if (childDoor == null || parentDoor == null)
            {
                connections = null;
                failure = EdgarGenerationFailure.ConnectionMappingFailed;
                return false;
            }

            RoomArchetypeSO childArchetype = FindArchetypeByPrefab(
                candidatesByNodeId[childNode.NodeId],
                childInstance.RoomTemplatePrefab);
            RoomArchetypeSO parentArchetype = FindArchetypeByPrefab(
                candidatesByNodeId[childNode.ParentNodeId],
                parentInstance.RoomTemplatePrefab);

            if (!TryResolveConnectionId(childArchetype, childDoor, out string childConnectionId) ||
                !TryResolveConnectionId(parentArchetype, parentDoor, out string parentConnectionId))
            {
                connections = null;
                failure = EdgarGenerationFailure.ConnectionMappingFailed;
                return false;
            }

            connections.Add(new DungeonConnectionPlan(
                childNode.ParentNodeId,
                parentConnectionId,
                childNode.NodeId,
                childConnectionId));
        }

        connections.Sort((left, right) =>
        {
            int parentCompare = string.CompareOrdinal(left.ParentRoomId, right.ParentRoomId);
            return parentCompare != 0 ? parentCompare : string.CompareOrdinal(left.ChildRoomId, right.ChildRoomId);
        });
        failure = EdgarGenerationFailure.None;
        return true;
    }

    private static DoorInstanceGrid2D FindDoorTo(RoomInstanceGrid2D from, RoomBase to)
    {
        if (from?.Doors == null)
        {
            return null;
        }

        for (int i = 0; i < from.Doors.Count; i++)
        {
            if (from.Doors[i].ConnectedRoom == to)
            {
                return from.Doors[i];
            }
        }

        return null;
    }

    private static RoomArchetypeSO FindArchetypeByPrefab(
        List<RoomArchetypeSO> candidates,
        GameObject prefab)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].RoomPrefab == prefab)
            {
                return candidates[i];
            }
        }

        return null;
    }

    // RoomArchetypeSO._connections의 Cell+Direction+PassageWidthInCells로 계산한 Door Line이 Edgar
    // Door의 실제 Door Line과 정확히 일치하는 ConnectionId를 찾는다. SO 선언과 실제 Room Template의
    // Manual Door Line이 어긋나면(폭 Drift 등) 매칭에 실패해 생성이 ConnectionMappingFailed로 끝난다.
    private static bool TryResolveConnectionId(
        RoomArchetypeSO archetype,
        DoorInstanceGrid2D door,
        out string connectionId)
    {
        connectionId = null;

        if (archetype?.Connections == null || door == null)
        {
            return false;
        }

        (Vector2Int min, Vector2Int max) actualLine = NormalizeLine(door.DoorLine);

        for (int i = 0; i < archetype.Connections.Count; i++)
        {
            RoomConnectionDefinition connection = archetype.Connections[i];

            if (connection == null)
            {
                continue;
            }

            (Vector2Int min, Vector2Int max) expectedLine =
                NormalizeLine(connection.Cell, connection.GetDoorLineEndCell());

            if (expectedLine.min == actualLine.min && expectedLine.max == actualLine.max)
            {
                connectionId = connection.ConnectionId;
                return true;
            }
        }

        return false;
    }

    private static (Vector2Int min, Vector2Int max) NormalizeLine(OrthogonalLine doorLine)
    {
        return NormalizeLine(
            new Vector2Int(doorLine.From.x, doorLine.From.y),
            new Vector2Int(doorLine.To.x, doorLine.To.y));
    }

    private static (Vector2Int min, Vector2Int max) NormalizeLine(Vector2Int a, Vector2Int b)
    {
        Vector2Int min = new Vector2Int(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        Vector2Int max = new Vector2Int(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        return (min, max);
    }

    private static void DestroyLevelGraph(LevelGraph levelGraph)
    {
        if (levelGraph == null)
        {
            return;
        }

        for (int i = 0; i < levelGraph.Rooms.Count; i++)
        {
            DestroyImmediateOrRuntime(levelGraph.Rooms[i]);
        }

        for (int i = 0; i < levelGraph.Connections.Count; i++)
        {
            DestroyImmediateOrRuntime(levelGraph.Connections[i]);
        }

        DestroyImmediateOrRuntime(levelGraph);
    }

    // Edit Mode(Application.isPlaying == false)와 Play Mode/Player Build 모두에서 안전하게 정리한다.
    private static void DestroyImmediateOrRuntime(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}
