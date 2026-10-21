/// <summary>
/// Room 선택 전의 역할·부모·필수 여부를 담는 Dungeon 토폴로지 노드.
/// </summary>
public sealed class DungeonTopologyNode
{
    public string NodeId { get; }
    public RoomRole Role { get; }
    public string ParentNodeId { get; }
    public bool IsOptional { get; }

    // Layout Planner가 확정한 노드 계약을 보관한다.
    internal DungeonTopologyNode(string nodeId, RoomRole role, string parentNodeId, bool isOptional)
    {
        NodeId = nodeId;
        Role = role;
        ParentNodeId = parentNodeId;
        IsOptional = isOptional;
    }
}
