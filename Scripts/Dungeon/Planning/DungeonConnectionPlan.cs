/// <summary>
/// 부모·자식 Room의 호환 출입구를 잇는 불변 연결 한 건.
/// </summary>
public sealed class DungeonConnectionPlan
{
    public string ParentRoomId { get; }
    public string ParentConnectionId { get; }
    public string ChildRoomId { get; }
    public string ChildConnectionId { get; }

    // 연결 양끝의 Room과 출입구 ID를 보관한다.
    internal DungeonConnectionPlan(
        string parentRoomId,
        string parentConnectionId,
        string childRoomId,
        string childConnectionId)
    {
        ParentRoomId = parentRoomId;
        ParentConnectionId = parentConnectionId;
        ChildRoomId = childRoomId;
        ChildConnectionId = childConnectionId;
    }
}
