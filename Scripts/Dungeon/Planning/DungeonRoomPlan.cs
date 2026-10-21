using UnityEngine;

/// <summary>
/// Room의 역할·Archetype·좌표를 담는 불변 계획.
/// </summary>
public sealed class DungeonRoomPlan
{
    public string RoomId { get; }
    public RoomRole Role { get; }
    public string ParentRoomId { get; }
    public bool IsOptional { get; }
    public RoomArchetypeSO Archetype { get; }
    public Vector2Int Origin { get; }

    // Topology 노드, 선택된 Archetype과 확정 원점을 그대로 보관한다.
    internal DungeonRoomPlan(
        DungeonTopologyNode node,
        RoomArchetypeSO archetype,
        Vector2Int origin)
    {
        RoomId = node.NodeId;
        Role = node.Role;
        ParentRoomId = node.ParentNodeId;
        IsOptional = node.IsOptional;
        Archetype = archetype;
        Origin = origin;
    }

    // Boss Arena처럼 Topology Node 없이 별도 Portal Stage로 준비되는 Room의 계획을 만든다(부모·원점 없음).
    internal DungeonRoomPlan(string roomId, RoomRole role, RoomArchetypeSO archetype)
    {
        RoomId = roomId;
        Role = role;
        ParentRoomId = null;
        IsOptional = false;
        Archetype = archetype;
        Origin = Vector2Int.zero;
    }
}
