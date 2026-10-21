using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// 입구·메인 경로·선택 분기의 불변 노드 그래프.
/// </summary>
public sealed class DungeonTopologyPlan
{
    private readonly ReadOnlyCollection<DungeonTopologyNode> _nodes;

    public int ContentRoomCount { get; }
    public int RequiredCombatRoomCount { get; }
    public int OptionalBranchCount { get; }
    public IReadOnlyList<DungeonTopologyNode> Nodes => _nodes;

    /// <summary>
    /// Boss Dungeon에서 Boss Portal이 활성화되는 마지막 Combat Node ID. Boss는 더 이상 물리 Graph의
    /// Node가 아니므로(dungeon_system-design.md 4.2a), Normal Dungeon에서는 항상 null이다.
    /// </summary>
    public string PortalTargetRoomId { get; }

    // Layout Planner 결과를 복사해 외부 변경을 차단한다.
    internal DungeonTopologyPlan(
        int contentRoomCount,
        int requiredCombatRoomCount,
        int optionalBranchCount,
        IList<DungeonTopologyNode> nodes,
        string portalTargetRoomId)
    {
        ContentRoomCount = contentRoomCount;
        RequiredCombatRoomCount = requiredCombatRoomCount;
        OptionalBranchCount = optionalBranchCount;
        _nodes = new ReadOnlyCollection<DungeonTopologyNode>(new List<DungeonTopologyNode>(nodes));
        PortalTargetRoomId = portalTargetRoomId;
    }
}
