using System.Collections.Generic;

/// <summary>
/// Seed로 Entrance에서 시작해 Combat이 실제 분기점이 되는 물리 Topology Graph를 만든다.
/// Normal/Boss 모두 물리 Graph는 Entrance·Combat·Corridor만으로 구성하며(dungeon_system-design.md
/// 4.1, 4.3), 모든 Entrance→Combat·Combat→Combat Edge에는 Corridor를 강제로 삽입한다. Boss는 물리
/// Graph의 Node가 아니라 Boss Run의 마지막 Combat을 가리키는 <see cref="DungeonTopologyPlan.PortalTargetRoomId"/>로만
/// 기록한다(4.2a). RoomPool에 Corridor 후보가 없으면 이 Node들은 Edgar 변환 단계(EdgarGenerationAdapter)에서
/// TemplateCandidateUnavailable로 자연스럽게 생성 실패한다.
/// </summary>
public sealed class DungeonTopologyPlanner
{
    // Dungeon 정의 범위 안에서 결정적인 Layout을 만든다.
    public DungeonTopologyPlan Create(DungeonDefinitionSO definition, int seed)
    {
        bool isBoss = definition.DungeonType == DungeonType.Boss;
        DungeonPlanRandom countRandom = new DungeonPlanRandom(
            DungeonPlanRules.CombineSeed(seed, definition.ContentId, definition.GenerationVersion));
        int combatCount = definition.MinimumContentRoomCount +
            countRandom.Next(definition.MaximumContentRoomCount - definition.MinimumContentRoomCount + 1);
        int branchBudget = System.Math.Min(CountThreeDoorCombatCandidates(definition), combatCount);

        List<DungeonTopologyNode> nodes = new List<DungeonTopologyNode>(combatCount * 2 + 1);
        DungeonTopologyNode entrance = new DungeonTopologyNode("room_00", RoomRole.Entrance, null, false);
        nodes.Add(entrance);

        List<DungeonTopologyNode> combatNodes = BuildCombatTree(nodes, entrance.NodeId, combatCount, seed, branchBudget);
        InsertCorridorsBeforeCombats(nodes, combatNodes);

        string portalTargetRoomId = isBoss ? combatNodes[combatNodes.Count - 1].NodeId : null;
        return new DungeonTopologyPlan(combatCount, combatCount, 0, nodes, portalTargetRoomId);
    }

    // Entrance 아래에 Combat Node를 Seed 기반으로 무작위 부모에 붙여 1~3차수 Tree를 만든다.
    // 각 Combat은 기본 1개 자식(2-Door)까지 허용하고, branchBudget이 남아 있을 때만 2개 자식(3-Door)까지 허용한다.
    private static List<DungeonTopologyNode> BuildCombatTree(
        List<DungeonTopologyNode> nodes,
        string entranceNodeId,
        int combatCount,
        int seed,
        int branchBudget)
    {
        DungeonPlanRandom treeRandom = new DungeonPlanRandom(DungeonPlanRules.CombineSeed(seed, "combat_tree"));
        List<DungeonTopologyNode> combatNodes = new List<DungeonTopologyNode>(combatCount);
        List<DungeonTopologyNode> openParents = new List<DungeonTopologyNode>(combatCount);
        Dictionary<string, int> childCountByNodeId = new Dictionary<string, int>(combatCount);

        DungeonTopologyNode root = new DungeonTopologyNode($"room_{nodes.Count:00}", RoomRole.Combat, entranceNodeId, false);
        nodes.Add(root);
        combatNodes.Add(root);
        openParents.Add(root);
        childCountByNodeId[root.NodeId] = 0;

        for (int i = 1; i < combatCount; i++)
        {
            List<DungeonTopologyNode> candidates = new List<DungeonTopologyNode>(openParents.Count);

            for (int p = 0; p < openParents.Count; p++)
            {
                int childCount = childCountByNodeId[openParents[p].NodeId];

                if (childCount < 1 || (childCount < 2 && branchBudget > 0))
                {
                    candidates.Add(openParents[p]);
                }
            }

            DungeonTopologyNode parent = candidates[treeRandom.Next(candidates.Count)];
            int parentChildCount = childCountByNodeId[parent.NodeId];

            if (parentChildCount == 1)
            {
                branchBudget--;
            }

            DungeonTopologyNode combat = new DungeonTopologyNode($"room_{nodes.Count:00}", RoomRole.Combat, parent.NodeId, false);
            nodes.Add(combat);
            combatNodes.Add(combat);
            childCountByNodeId[parent.NodeId] = parentChildCount + 1;
            childCountByNodeId[combat.NodeId] = 0;
            openParents.Add(combat);

            if (childCountByNodeId[parent.NodeId] >= 2)
            {
                openParents.Remove(parent);
            }
        }

        return combatNodes;
    }

    // 모든 Combat 앞에 Corridor Node를 하나씩 끼워 넣는다. 부모가 Entrance든 다른 Combat이든 구분하지
    // 않고 전부 대상이므로, Entrance→첫 Combat Edge를 포함한 모든 물리 Combat Door의 반대편은 Corridor가
    // 된다(dungeon_system-design.md 4.3). RoomPool에 실제 Corridor 후보가 있는지는 여기서 확인하지 않는다
    // — 없으면 EdgarGenerationAdapter가 Corridor Node에 대해 TemplateCandidateUnavailable로 생성을 실패시킨다.
    private static void InsertCorridorsBeforeCombats(List<DungeonTopologyNode> nodes, List<DungeonTopologyNode> combatNodes)
    {
        // combatNodes 생성 순서대로 순회해 같은 Seed에서 삽입 대상·Node ID가 항상 고정되게 한다.
        for (int i = 0; i < combatNodes.Count; i++)
        {
            DungeonTopologyNode combat = combatNodes[i];
            DungeonTopologyNode corridor = new DungeonTopologyNode(
                $"room_{nodes.Count:00}",
                RoomRole.Corridor,
                combat.ParentNodeId,
                false);
            nodes.Add(corridor);
            ReparentNode(nodes, combat, corridor.NodeId);
        }
    }

    // 노드의 Id·역할·필수 여부는 유지한 채 부모만 교체한다(DungeonTopologyNode는 불변이라 자리 교체로 적용한다).
    private static void ReparentNode(List<DungeonTopologyNode> nodes, DungeonTopologyNode node, string newParentNodeId)
    {
        int index = nodes.IndexOf(node);
        nodes[index] = new DungeonTopologyNode(node.NodeId, node.Role, newParentNodeId, node.IsOptional);
    }

    // RoomPool에서 출입구가 3개인 Combat 후보 수를 센다. 분기(3-Door) Node 수의 상한으로 쓰인다.
    private static int CountThreeDoorCombatCandidates(DungeonDefinitionSO definition)
    {
        int count = 0;

        for (int i = 0; i < definition.RoomPool.Count; i++)
        {
            RoomArchetypeSO room = definition.RoomPool[i];

            if (room != null && room.HasRole(RoomRole.Combat) && room.Connections != null && room.Connections.Count == 3)
            {
                count++;
            }
        }

        return count;
    }
}
